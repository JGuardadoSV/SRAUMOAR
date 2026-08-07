using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record HaciendaRespuesta(bool Exito, string? SelloRecepcion, string Respuesta, string? Error = null);

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
        => PostAsync(configuracion.Entidad.UrlRecepcion ?? Base(configuracion) + "/fesv/recepciondte", token, new
        {
            ambiente = configuracion.Entidad.AmbienteFiscal,
            idEnvio = 1,
            version,
            tipoDte,
            documento = jsonFirmado,
            codigoGeneracion
        }, cancellationToken);

    public Task<HaciendaRespuesta> EnviarInvalidacionAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, string codigoGeneracion, CancellationToken cancellationToken = default)
        => PostAsync(configuracion.Entidad.UrlAnulacion ?? Base(configuracion) + "/fesv/anulardte", token, new
        {
            ambiente = configuracion.Entidad.AmbienteFiscal,
            idEnvio = 1,
            version = 3,
            documento = jsonFirmado,
            codigoGeneracion
        }, cancellationToken);

    public Task<HaciendaRespuesta> EnviarContingenciaAsync(ConfiguracionFeActiva configuracion, string token, string jsonFirmado, CancellationToken cancellationToken = default)
        => PostAsync(configuracion.Entidad.UrlContingencia ?? Base(configuracion) + "/fesv/contingencia", token, new
        {
            nit = NormalizarNit(configuracion.Entidad.NitEmisor),
            documento = jsonFirmado
        }, cancellationToken);

    private async Task<HaciendaRespuesta> PostAsync(string url, string token, object payload, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("HaciendaDte");
        client.Timeout = TimeSpan.FromSeconds(120);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new HaciendaRespuesta(false, null, body, $"Hacienda devolvió {(int)response.StatusCode}.");

        string? sello = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("selloRecibido", out var node)) sello = node.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        return new HaciendaRespuesta(!string.IsNullOrWhiteSpace(sello), sello, body, sello is null ? "Hacienda no devolvió sello de recepción." : null);
    }

    private static string Base(ConfiguracionFeActiva configuracion)
        => configuracion.Entidad.AmbienteFiscal == "01" ? "https://api.dtes.mh.gob.sv" : "https://apitest.dtes.mh.gob.sv";

    private static string NormalizarNit(string? nit)
        => (nit ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
}
