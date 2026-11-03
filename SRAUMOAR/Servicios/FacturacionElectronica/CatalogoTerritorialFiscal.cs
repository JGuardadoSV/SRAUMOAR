using System.Globalization;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record EntradaTerritorial(string Catalogo, string Codigo, string Valor, string Ambito, string? MunicipioCodigo);

/// <summary>Fuente incorporada de FacturaME; códigos, no IDs de filas ni nombres inferidos del modelo antiguo.</summary>
public static class CatalogoTerritorialFiscal
{
    private static readonly Lazy<IReadOnlyList<EntradaTerritorial>> Fuente = new(() =>
    {
        using var stream = typeof(CatalogoTerritorialFiscal).Assembly.GetManifestResourceStream("SRAUMOAR.TerritorioFiscal.json")
            ?? throw new InvalidOperationException("No se encontró el catálogo territorial fiscal.");
        using var json = JsonDocument.Parse(stream);
        return JsonSerializer.Deserialize<List<EntradaTerritorial>>(json.RootElement.GetProperty("entradas").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.AsReadOnly();
    });

    public static IReadOnlyList<EntradaTerritorial> Entradas => Fuente.Value;
    public static IEnumerable<EntradaTerritorial> Departamentos => Entradas.Where(e => e.Catalogo == "012");
    public static IEnumerable<EntradaTerritorial> Municipios(string? departamento)
        => Entradas.Where(e => e.Catalogo == "013" && e.Ambito == Codigo(departamento));
    public static IEnumerable<EntradaTerritorial> Distritos(string? departamento, string? municipio)
        => Entradas.Where(e => e.Catalogo == "008" && e.Ambito == Codigo(departamento) && e.MunicipioCodigo == Codigo(municipio));
    public static string Codigo(string? codigo) => TerritorioFiscalService.Normalizar(codigo);

    public static bool EsValida(string? departamento, string? municipio, string? distrito)
        => Departamentos.Any(e => e.Codigo == Codigo(departamento))
            && Municipios(departamento).Any(e => e.Codigo == Codigo(municipio))
            && Distritos(departamento, municipio).Any(e => e.Codigo == Codigo(distrito));

    public static void Exigir(string? departamento, string? municipio, string? distrito)
    {
        if (!EsValida(departamento, municipio, distrito))
            throw new InvalidOperationException("Seleccione un departamento, municipio y distrito de la misma ubicación fiscal.");
    }

    public static string Formatear(string? departamento, string? municipio, string? distrito, string? complemento)
    {
        var dep = Codigo(departamento);
        var mun = Codigo(municipio);
        var dis = Codigo(distrito);
        // Los históricos sin distrito no se convierten al nuevo municipio por coincidencia numérica.
        var completa = EsValida(dep, mun, dis);
        var nombreDep = Departamentos.SingleOrDefault(e => e.Codigo == dep)?.Valor;
        var nombreMun = completa ? Municipios(dep).Single(e => e.Codigo == mun).Valor : null;
        var nombreDis = completa ? Distritos(dep, mun).Single(e => e.Codigo == dis).Valor : null;
        static string Mostrar(string etiqueta, string? nombre, string codigo)
            => string.IsNullOrWhiteSpace(codigo) ? "" : nombre is null
                ? $"{etiqueta} {codigo} (sin correspondencia fiscal confirmada)"
                : $"{CultureInfo.GetCultureInfo("es-SV").TextInfo.ToTitleCase(nombre.ToLower(CultureInfo.GetCultureInfo("es-SV")))} ({codigo})";
        return string.Join(", ", new[] { complemento?.Trim(), Mostrar("Distrito", nombreDis, dis),
            Mostrar("Municipio", nombreMun, mun), Mostrar("Departamento", nombreDep, dep) }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public static string Formatear(JObject direccion) => Formatear(direccion.Value<string>("departamento"),
        direccion.Value<string>("municipio"), direccion.Value<string>("distrito"), direccion.Value<string>("complemento"));
}
