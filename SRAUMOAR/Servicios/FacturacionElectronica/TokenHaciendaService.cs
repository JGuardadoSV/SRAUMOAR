using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface ITokenHaciendaService
{
    Task<string> ObtenerAsync(ConfiguracionFeActiva configuracion, CancellationToken cancellationToken = default);
    void Invalidar(string ambienteFiscal, string? usuario = null);
}

public sealed class TokenHaciendaService : ITokenHaciendaService
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public TokenHaciendaService(IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ObtenerAsync(ConfiguracionFeActiva configuracion, CancellationToken cancellationToken = default)
    {
        var usuario = configuracion.Entidad.UsuarioHacienda;
        var password = configuracion.CredencialApi;
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Falta usuario o credencial de Hacienda para {configuracion.Entidad.AmbienteFiscal}.");

        var key = $"SRAUMOAR:DTE:TOKEN:{configuracion.Entidad.AmbienteFiscal}:{usuario}";
        if (_cache.TryGetValue(key, out string? token) && TokenValido(token)) return token;

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(key, out token) && TokenValido(token)) return token;

            var url = configuracion.Entidad.UrlAutenticacion;
            if (string.IsNullOrWhiteSpace(url))
                url = configuracion.Entidad.AmbienteFiscal == "01"
                    ? "https://api.dtes.mh.gob.sv/seguridad/auth"
                    : "https://apitest.dtes.mh.gob.sv/seguridad/auth";

            using var client = _httpClientFactory.CreateClient("HaciendaDte");
            using var response = await client.PostAsync(url, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", usuario),
                new KeyValuePair<string, string>("pwd", password)
            }), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Hacienda rechazó la autenticación ({(int)response.StatusCode}).");

            using var document = JsonDocument.Parse(body);
            var bodyNode = Propiedad(document.RootElement, "body");
            var tokenNode = bodyNode.HasValue ? Propiedad(bodyNode.Value, "token") : null;
            var rootToken = Propiedad(document.RootElement, "token");
            var recibido = tokenNode?.GetString() ?? rootToken?.GetString();
            if (string.IsNullOrWhiteSpace(recibido) || !TokenValido(recibido))
                throw new InvalidOperationException("Hacienda no devolvió un token JWT válido.");

            var expiracion = LeerExpiracion(recibido);
            var duracion = expiracion - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
            if (duracion < TimeSpan.FromMinutes(1)) duracion = TimeSpan.FromMinutes(1);
            _cache.Set(key, recibido, duracion);
            return recibido;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Invalidar(string ambienteFiscal, string? usuario = null)
        => _cache.Remove($"SRAUMOAR:DTE:TOKEN:{ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal)}:{usuario}");

    private static bool TokenValido(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try { return LeerExpiracion(token) > DateTimeOffset.UtcNow.AddSeconds(30); }
        catch { return false; }
    }

    private static DateTimeOffset LeerExpiracion(string token)
    {
        var partes = token.Split('.');
        if (partes.Length != 3) throw new InvalidOperationException("Token JWT inválido.");
        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (!json.RootElement.TryGetProperty("exp", out var exp)) throw new InvalidOperationException("Token sin expiración.");
        return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
    }

    private static JsonElement? Propiedad(JsonElement elemento, string nombre)
    {
        foreach (var propiedad in elemento.EnumerateObject())
            if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase)) return propiedad.Value;
        return null;
    }
}
