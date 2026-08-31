using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
    // El servicio es scoped, pero el cache es compartido: los locks también deben serlo.
    private static readonly ConditionalWeakTable<IMemoryCache, ConcurrentDictionary<string, SemaphoreSlim>> Locks = new();

    public TokenHaciendaService(IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ObtenerAsync(ConfiguracionFeActiva configuracion, CancellationToken cancellationToken = default)
    {
        return await ObtenerORenovarAsync(configuracion, null, cancellationToken);
    }

    internal Task<string> RenovarTrasRechazoAsync(ConfiguracionFeActiva configuracion, string tokenRechazado, CancellationToken cancellationToken)
        => ObtenerORenovarAsync(configuracion, NormalizarToken(tokenRechazado), cancellationToken);

    private async Task<string> ObtenerORenovarAsync(ConfiguracionFeActiva configuracion, string? tokenRechazado, CancellationToken cancellationToken)
    {
        var usuario = configuracion.Entidad.UsuarioHacienda;
        var password = configuracion.CredencialApi;
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Falta usuario o credencial de Hacienda para {configuracion.Entidad.AmbienteFiscal}.");

        var key = Clave(configuracion.Entidad.AmbienteFiscal, usuario);
        var semaphore = Locks.GetValue(_cache, _ => new()).GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(key, out string? token) && TokenValido(token) &&
                (tokenRechazado is null || NormalizarToken(token!) != tokenRechazado)) return NormalizarToken(token!);
            _cache.Remove(key);

            var url = configuracion.Entidad.UrlAutenticacion;
            if (string.IsNullOrWhiteSpace(url))
                url = configuracion.Entidad.AmbienteFiscal == "01"
                    ? "https://api.dtes.mh.gob.sv/seguridad/auth"
                    : "https://apitest.dtes.mh.gob.sv/seguridad/auth";

            using var client = _httpClientFactory.CreateClient("HaciendaDte");
            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", usuario),
                new KeyValuePair<string, string>("pwd", password)
            });
            using var response = await client.PostAsync(HaciendaDteClient.ValidarUrl(url, configuracion.Entidad.AmbienteFiscal), content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Hacienda rechazó la autenticación ({(int)response.StatusCode}).");

            using var document = JsonDocument.Parse(body);
            var bodyNode = Propiedad(document.RootElement, "body");
            var tokenNode = bodyNode.HasValue ? Propiedad(bodyNode.Value, "token") : null;
            var rootToken = Propiedad(document.RootElement, "token");
            var recibido = tokenNode?.ValueKind == JsonValueKind.String ? tokenNode.Value.GetString()
                : rootToken?.ValueKind == JsonValueKind.String ? rootToken.Value.GetString() : null;
            if (string.IsNullOrWhiteSpace(recibido) || !TokenValido(recibido))
                throw new InvalidOperationException("Hacienda no devolvió un token JWT válido.");

            recibido = NormalizarToken(recibido);
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
    {
        var key = Clave(ambienteFiscal, usuario);
        var semaphore = Locks.GetValue(_cache, _ => new()).GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try { _cache.Remove(key); }
        finally { semaphore.Release(); }
    }

    private static string Clave(string ambiente, string? usuario)
        => $"SRAUMOAR:DTE:TOKEN:{ConfiguracionFeService.NormalizarAmbiente(ambiente)}:{usuario?.Trim()}";

    internal static string NormalizarToken(string token)
    {
        var normalizado = token.Trim();
        return normalizado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? normalizado[7..].Trim() : normalizado;
    }

    private static bool TokenValido(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try { return LeerExpiracion(token) > DateTimeOffset.UtcNow.AddSeconds(30); }
        catch { return false; }
    }

    private static DateTimeOffset LeerExpiracion(string token)
    {
        var partes = NormalizarToken(token).Split('.');
        if (partes.Length != 3) throw new InvalidOperationException("Token JWT inválido.");
        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (!json.RootElement.TryGetProperty("exp", out var exp)) throw new InvalidOperationException("Token sin expiración.");
        return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
    }

    private static JsonElement? Propiedad(JsonElement elemento, string nombre)
    {
        if (elemento.ValueKind != JsonValueKind.Object) return null;
        foreach (var propiedad in elemento.EnumerateObject())
            if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase)) return propiedad.Value;
        return null;
    }
}
