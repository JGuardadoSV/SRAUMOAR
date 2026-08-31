using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record HaciendaRespuesta(bool Exito, string? SelloRecepcion, string Respuesta, string? Error = null, bool RechazoFiscal = false);

// Sólo se usa cuando se sabe que el documento NO llegó a recepción fiscal.
internal sealed class DteEnvioNoRealizadoException : Exception
{
    public DteEnvioNoRealizadoException(Exception causa) : base(causa.Message, causa) { }
}

internal sealed class HaciendaNoAutorizadoException : Exception
{
    public HaciendaNoAutorizadoException() : base("Hacienda rechazó la autorización (401).") { }
}

public interface IHaciendaDteClient
{
    Task<HaciendaRespuesta> EnviarDteAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, string tipoDte, int version, string codigoGeneracion, CancellationToken cancellationToken = default);
    Task<HaciendaRespuesta> EnviarInvalidacionAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, string codigoGeneracion, CancellationToken cancellationToken = default);
    Task<HaciendaRespuesta> EnviarContingenciaAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, CancellationToken cancellationToken = default);
}

public sealed class HaciendaDteClient : IHaciendaDteClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HaciendaDteClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<HaciendaRespuesta> EnviarDteAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, string tipoDte, int version, string codigoGeneracion, CancellationToken cancellationToken = default)
        => PostAsync(configuracion, configuracion.Entidad.UrlRecepcion ?? Base(configuracion) + "/fesv/recepciondte", token, new
        {
            ambiente = configuracion.Entidad.AmbienteFiscal,
            idEnvio = 1,
            version,
            tipoDte,
            documento = jsonFirmado,
            codigoGeneracion
        }, cancellationToken);

    public Task<HaciendaRespuesta> EnviarInvalidacionAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, string codigoGeneracion, CancellationToken cancellationToken = default)
        => PostAsync(configuracion, configuracion.Entidad.UrlAnulacion ?? Base(configuracion) + "/fesv/anulardte", token, new
        {
            ambiente = configuracion.Entidad.AmbienteFiscal,
            idEnvio = 1,
            version = 3,
            documento = jsonFirmado,
            codigoGeneracion
        }, cancellationToken);

    public Task<HaciendaRespuesta> EnviarContingenciaAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, CancellationToken cancellationToken = default)
        => PostAsync(configuracion, configuracion.Entidad.UrlContingencia ?? Base(configuracion) + "/fesv/contingencia", token, new
        {
            nit = NormalizarNit(configuracion.Entidad.NitEmisor),
            documento = jsonFirmado
        }, cancellationToken);

    private async Task<HaciendaRespuesta> PostAsync(ConfiguracionFeActiva configuracion, string url, string token, object payload, CancellationToken cancellationToken)
    {
        HttpClient? client = null;
        HttpRequestMessage? request = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destino = ValidarUrl(url, configuracion.Entidad.AmbienteFiscal);
            client = _httpClientFactory.CreateClient("HaciendaDte");
            client.Timeout = TimeSpan.FromSeconds(120);
            request = new HttpRequestMessage(HttpMethod.Post, destino);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenHaciendaService.NormalizarToken(token));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        }
        catch (Exception ex)
        {
            request?.Dispose();
            client?.Dispose();
            throw new DteEnvioNoRealizadoException(ex);
        }
        using var clientLifetime = client;
        using var requestLifetime = request;
        using var envio = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        envio.CancelAfter(client.Timeout);
        // Desde SendAsync, un timeout/corte/cancelación NO prueba que no hubo envío.
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, envio.Token);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new HaciendaNoAutorizadoException();
        var body = await response.Content.ReadAsStringAsync(envio.Token);
        if (!response.IsSuccessStatusCode)
            return new HaciendaRespuesta(false, null, body, $"Hacienda devolvió {(int)response.StatusCode}.", EsRechazoFiscalExplicito(body));

        string? sello = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("selloRecibido", out var node) && node.ValueKind == JsonValueKind.String) sello = node.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        return new HaciendaRespuesta(!string.IsNullOrWhiteSpace(sello), sello, body,
            string.IsNullOrWhiteSpace(sello) ? "Hacienda no devolvió sello de recepción." : null,
            string.IsNullOrWhiteSpace(sello) && EsRechazoFiscalExplicito(body));
    }

    internal static bool EsRechazoFiscalExplicito(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (root.TryGetProperty("estado", out var estado) &&
                estado.ValueKind == JsonValueKind.String &&
                string.Equals(estado.GetString(), "RECHAZADO", StringComparison.OrdinalIgnoreCase)) return true;
            if (root.TryGetProperty("resultado", out var resultado) && resultado.ValueKind == JsonValueKind.Object &&
                resultado.TryGetProperty("estado", out estado) &&
                estado.ValueKind == JsonValueKind.String &&
                string.Equals(estado.GetString(), "RECHAZADO", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch (System.Text.Json.JsonException) { }
        return false;
    }

    internal static Uri ValidarUrl(string url, string? ambienteFiscal = null, bool permitirHttpLoopback = false)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("La URL de transporte debe ser HTTP(S) absoluta.");
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("La URL de transporte no admite credenciales ni fragmentos.");
        if (uri.Scheme != Uri.UriSchemeHttps && !(permitirHttpLoopback && uri.IsLoopback))
            throw new InvalidOperationException("El transporte externo requiere HTTPS; HTTP sólo se permite para el firmador loopback.");
        if (ambienteFiscal is not null)
        {
            var ambiente = ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal);
            var host = uri.IdnHost.TrimEnd('.');
            if ((host.Equals("api.dtes.mh.gob.sv", StringComparison.OrdinalIgnoreCase) && ambiente != "01") ||
                (host.Equals("apitest.dtes.mh.gob.sv", StringComparison.OrdinalIgnoreCase) && ambiente != "00"))
                throw new InvalidOperationException("El endpoint oficial de Hacienda no corresponde al ambiente del JSON.");
        }
        return uri;
    }

    private static string Base(ConfiguracionFeActiva configuracion)
        => configuracion.Entidad.AmbienteFiscal == "01" ? "https://api.dtes.mh.gob.sv" : "https://apitest.dtes.mh.gob.sv";

    private static string NormalizarNit(string? nit)
        => (nit ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
}
