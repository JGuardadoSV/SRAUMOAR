using System.Text.Json;
using SRAUMOAR.Entidades.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

internal sealed record TerritorioFiscalFuente(
    string Fuente,
    string Sha256Excel,
    string Descripcion,
    Dictionary<string, int[]> Municipios);

public static class TerritorioFiscalService
{
    private static readonly Lazy<TerritorioFiscalFuente> FuenteActual = new(Cargar);
    private static readonly string[] Departamentos = Enumerable.Range(1, 14).Select(x => x.ToString("D2")).ToArray();

    public static string Sha256Excel => FuenteActual.Value.Sha256Excel;

    public static void Enriquecer(IReadOnlyList<CatalogoFeItem> items)
    {
        var fuente = FuenteActual.Value;
        var municipios = items.Where(x => x.CodigoCatalogo == "CAT-013").ToList();
        var municipiosNacionales = municipios.Where(x => x.Codigo != "00").ToList();
        var clavesMunicipio = fuente.Municipios.Keys.ToList();
        if (municipiosNacionales.Count != clavesMunicipio.Count)
            throw new InvalidOperationException($"CAT-013 debe contener {clavesMunicipio.Count} municipios nacionales en el orden oficial.");

        var extranjeroMunicipio = municipios.SingleOrDefault(x => x.Codigo == "00");
        if (extranjeroMunicipio is not null)
        {
            extranjeroMunicipio.AmbitoDepartamento = "00";
            extranjeroMunicipio.AmbitoMunicipio = "00";
        }

        for (var i = 0; i < clavesMunicipio.Count; i++)
        {
            var partes = clavesMunicipio[i].Split('/');
            if (municipiosNacionales[i].Codigo != partes[1])
                throw new InvalidOperationException($"CAT-013 no conserva el orden oficial en la fila {municipiosNacionales[i].Orden + 1}.");
            municipiosNacionales[i].AmbitoDepartamento = partes[0];
            municipiosNacionales[i].AmbitoMunicipio = partes[1];
        }

        var distritoAMunicipio = fuente.Municipios
            .SelectMany(x => x.Value.Select(distrito => new
            {
                Departamento = x.Key[..2],
                Municipio = x.Key[3..],
                Distrito = distrito.ToString("D2")
            }))
            .ToDictionary(x => $"{x.Departamento}/{x.Distrito}", x => x.Municipio, StringComparer.Ordinal);

        var distritos = items.Where(x => x.CodigoCatalogo == "CAT-008").ToList();
        var extranjeroDistrito = distritos.SingleOrDefault(x => x.Codigo == "00");
        if (extranjeroDistrito is not null)
        {
            extranjeroDistrito.AmbitoDepartamento = "00";
            extranjeroDistrito.AmbitoMunicipio = "00";
        }

        var indiceDepartamento = -1;
        var usados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var distrito in distritos.Where(x => x.Codigo != "00"))
        {
            if (distrito.Codigo == "01") indiceDepartamento++;
            if (indiceDepartamento < 0 || indiceDepartamento >= Departamentos.Length)
                throw new InvalidOperationException("CAT-008 no conserva los bloques oficiales por departamento.");

            var departamento = Departamentos[indiceDepartamento];
            var clave = $"{departamento}/{distrito.Codigo}";
            if (!distritoAMunicipio.TryGetValue(clave, out var municipio))
                throw new InvalidOperationException($"El distrito {clave} no aparece en la relación territorial oficial.");
            distrito.AmbitoDepartamento = departamento;
            distrito.AmbitoMunicipio = municipio;
            usados.Add(clave);
        }

        if (usados.Count != distritoAMunicipio.Count)
            throw new InvalidOperationException("CAT-008 no contiene los 262 distritos de la relación territorial oficial.");
    }

    public static bool EsDireccionValida(string? departamento, string? municipio, string? distrito,
        IEnumerable<CatalogoFeItem> departamentos, IEnumerable<CatalogoFeItem> municipios,
        IEnumerable<CatalogoFeItem> distritos)
    {
        var dep = Normalizar(departamento);
        var mun = Normalizar(municipio);
        var dis = Normalizar(distrito);
        return departamentos.Any(x => EsActivo(x, "CAT-012") && x.Codigo == dep)
            && municipios.Any(x => EsActivo(x, "CAT-013") && x.Codigo == mun && x.AmbitoDepartamento == dep)
            && distritos.Any(x => EsActivo(x, "CAT-008") && x.Codigo == dis
                && x.AmbitoDepartamento == dep && x.AmbitoMunicipio == mun);
    }

    private static bool EsActivo(CatalogoFeItem item, string catalogo)
        => item.Activo && item.Version == "1.1" && item.CodigoCatalogo == catalogo;

    public static string Normalizar(string? codigo)
        => int.TryParse(codigo?.Trim(), out var numero) && numero is >= 0 and < 100
            ? numero.ToString("D2")
            : codigo?.Trim() ?? string.Empty;

    private static TerritorioFiscalFuente Cargar()
    {
        using var stream = typeof(TerritorioFiscalService).Assembly
            .GetManifestResourceStream("SRAUMOAR.TerritorioAsamblea.json")
            ?? throw new InvalidOperationException("No se encontró la relación territorial incorporada.");
        return JsonSerializer.Deserialize<TerritorioFiscalFuente>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("La relación territorial incorporada no es válida.");
    }
}
