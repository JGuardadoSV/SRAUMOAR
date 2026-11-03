using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record DteConsultaResultado(bool Aceptado, string? Sello, string Respuesta, string Mensaje);

public interface IDteConsultaClient
{
    Task<DteConsultaResultado> ConsultarAsync(ConfiguracionFeActiva configuracion, string nit, string tipo,
        string codigo, CancellationToken cancellationToken = default);
}

/// <summary>Consulta la identidad original: nunca firma ni transmite un nuevo documento.</summary>
public sealed class DteConsultaClient(IHttpClientFactory http, ITokenHaciendaService tokens) : IDteConsultaClient
{
    public async Task<DteConsultaResultado> ConsultarAsync(ConfiguracionFeActiva configuracion, string nit, string tipo,
        string codigo, CancellationToken cancellationToken = default)
    {
        var entidad = configuracion.Entidad;
        var externa = entidad.UsarApiExterna;
        var url = externa
            ? (entidad.UrlApiExterna ?? throw new InvalidOperationException("Falta la URL de la API externa.")).TrimEnd('/') + "/api/consulta-dte"
            : entidad.UrlConsulta ?? (entidad.AmbienteFiscal == "01" ? "https://api.dtes.mh.gob.sv" : "https://apitest.dtes.mh.gob.sv") + "/fesv/recepcion/consultadte/";
        var destino = HaciendaDteClient.ValidarUrl(url, entidad.AmbienteFiscal);
        var token = externa ? null : await tokens.ObtenerAsync(configuracion, cancellationToken);
        for (var intento = 0; intento < 2; intento++)
        {
            using var client = http.CreateClient(externa ? "ApiExternaDte" : "HaciendaDte");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(entidad.TimeoutSegundos, 10, 300));
            using var request = new HttpRequestMessage(HttpMethod.Post, destino);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!externa) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenHaciendaService.NormalizarToken(token!));
            object payload = externa
                ? new { nitEmisor = nit, tdte = tipo, codigoGeneracion = codigo, usuario = entidad.UsuarioHacienda,
                    password = configuracion.CredencialApi, ambiente = entidad.AmbienteFiscal }
                : new { nitEmisor = nit, tdte = tipo, codigoGeneracion = codigo };
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(client.Timeout);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            var body = await response.Content.ReadAsStringAsync(limite.Token);
            if (!externa && response.StatusCode == HttpStatusCode.Unauthorized && intento == 0)
            {
                tokens.Invalidar(entidad.AmbienteFiscal, entidad.UsuarioHacienda);
                token = await tokens.ObtenerAsync(configuracion, cancellationToken);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                return new(false, null, body, $"La consulta respondió HTTP {(int)response.StatusCode}; se conserva el estado original.");
            return Interpretar(body, codigo, entidad.AmbienteFiscal);
        }
        throw new InvalidOperationException("No pudo completarse la consulta fiscal.");
    }

    public static DteConsultaResultado Interpretar(string body, string codigo, string ambiente)
    {
        try
        {
            var root = JObject.Parse(body);
            var respuesta = root["resultado"] as JObject ?? root;
            static string? Texto(JObject objeto, string campo) => objeto[campo]?.Type == JTokenType.String ? objeto.Value<string>(campo) : null;
            var sello = Texto(respuesta, "selloRecibido") ?? Texto(respuesta, "selloRecepcion");
            // RECHAZADO en una consulta puede describir la consulta, no el documento.
            // Solo evidencia positiva, ligada al mismo código y ambiente, acredita aceptación.
            if (string.Equals(Texto(respuesta, "estado"), "PROCESADO", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Texto(respuesta, "codigoGeneracion"), codigo, StringComparison.OrdinalIgnoreCase)
                && Texto(respuesta, "ambiente") == ambiente && SelloValido(sello))
                return new(true, sello!.Trim(), body, "Recepción fiscal confirmada para el documento original.");
        }
        catch (JsonException) { }
        return new(false, null, body, "La consulta no acredita aceptación de esta identidad y ambiente. No se reenvió el documento ni se liberó su reserva.");
    }

    public static bool SelloValido(string? sello) => !string.IsNullOrWhiteSpace(sello)
        && !new[] { "null", "undefined", "EVENTO_EXISTENTE" }.Contains(sello.Trim(), StringComparer.OrdinalIgnoreCase);
}
