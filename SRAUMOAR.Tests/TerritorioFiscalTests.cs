using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using System.Text.Json;
using Xunit;

namespace SRAUMOAR.Tests;

public sealed class TerritorioFiscalTests
{
    [Fact]
    public void RelacionIncorporadaCubre44MunicipiosY262Distritos()
    {
        using var stream = typeof(TerritorioFiscalService).Assembly
            .GetManifestResourceStream("SRAUMOAR.TerritorioAsamblea.json")!;
        using var document = JsonDocument.Parse(stream);
        var relaciones = document.RootElement.GetProperty("municipios").EnumerateObject().ToArray();
        var municipios = relaciones.Select((x, i) =>
        {
            var partes = x.Name.Split('/');
            var item = Item("CAT-013", partes[1]);
            item.Orden = i;
            return item;
        }).ToList();
        municipios.Insert(0, Item("CAT-013", "00"));
        var distritos = new List<CatalogoFeItem> { Item("CAT-008", "00") };
        foreach (var departamento in Enumerable.Range(1, 14).Select(x => x.ToString("D2")))
            foreach (var distrito in relaciones.Where(x => x.Name.StartsWith(departamento + "/", StringComparison.Ordinal))
                         .SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetInt32()).Distinct().Order())
                distritos.Add(Item("CAT-008", distrito.ToString("D2")));

        TerritorioFiscalService.Enriquecer(municipios.Concat(distritos).ToList());

        Assert.Equal(44, municipios.Count - 1);
        Assert.Equal(262, distritos.Count - 1);
        Assert.All(municipios.Skip(1), x => Assert.NotEmpty(x.AmbitoDepartamento));
        Assert.All(distritos.Skip(1), x => Assert.NotEmpty(x.AmbitoMunicipio));
    }

    [Fact]
    public void CodigosRepetidosSonValidosSoloEnSuJerarquia()
    {
        var departamentos = new[] { Item("CAT-012", "01"), Item("CAT-012", "02") };
        var municipios = new[]
        {
            Item("CAT-013", "13", "01", "13"),
            Item("CAT-013", "17", "02", "17")
        };
        var distritos = new[]
        {
            Item("CAT-008", "03", "01", "13"),
            Item("CAT-008", "03", "02", "17")
        };

        Assert.True(TerritorioFiscalService.EsDireccionValida("01", "13", "03", departamentos, municipios, distritos));
        Assert.True(TerritorioFiscalService.EsDireccionValida("02", "17", "03", departamentos, municipios, distritos));
        Assert.False(TerritorioFiscalService.EsDireccionValida("01", "17", "03", departamentos, municipios, distritos));
        Assert.False(TerritorioFiscalService.EsDireccionValida("02", "13", "03", departamentos, municipios, distritos));
    }

    [Theory]
    [InlineData("1", "01")]
    [InlineData(" 03 ", "03")]
    [InlineData("00", "00")]
    public void CodigosSeNormalizanSinCambiarSuSignificado(string entrada, string esperado)
        => Assert.Equal(esperado, TerritorioFiscalService.Normalizar(entrada));

    private static CatalogoFeItem Item(string catalogo, string codigo,
        string departamento = "", string municipio = "") => new()
    {
        CodigoCatalogo = catalogo,
        Codigo = codigo,
        Descripcion = codigo,
        Version = "1.1",
        Activo = true,
        AmbitoDepartamento = departamento,
        AmbitoMunicipio = municipio
    };
}
