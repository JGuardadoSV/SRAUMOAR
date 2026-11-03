using System.Data.Common;
using System.Reflection;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Linq;
using QuestPDF.Infrastructure;
using SRAUMOAR.Controllers;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

public sealed class UbicacionFiscalAlumnoTests
{
    [Fact]
    public void FuenteContieneLos14Departamentos44MunicipiosY262DistritosMasExtranjero()
    {
        Assert.Equal(15, CatalogoTerritorialFiscal.Departamentos.Count());
        Assert.Equal(45, CatalogoTerritorialFiscal.Entradas.Count(e => e.Catalogo == "013"));
        Assert.Equal(263, CatalogoTerritorialFiscal.Entradas.Count(e => e.Catalogo == "008"));
        foreach (var distrito in CatalogoTerritorialFiscal.Entradas.Where(e => e.Catalogo == "008"))
        {
            Assert.True(CatalogoTerritorialFiscal.EsValida(distrito.Ambito, distrito.MunicipioCodigo, distrito.Codigo));
            Assert.Single(CatalogoTerritorialFiscal.Municipios(distrito.Ambito).Where(e => e.Codigo == distrito.MunicipioCodigo));
        }
    }

    [Theory]
    [InlineData("05", "28", "11", true)] // La Libertad Sur / Santa Tecla
    [InlineData("05", "26", "11", false)] // mismo departamento, otro municipio
    [InlineData("09", "11", "03", true)] // Cabañas Oeste / Ilobasco
    [InlineData("05", "11", "03", false)] // municipio de otro departamento
    [InlineData("04", "36", "07", true)]
    [InlineData("00", "00", "00", true)]
    [InlineData("05", "28", "00", false)]
    [InlineData("5", "28", "11", true)]
    [InlineData("05", "28", null, false)]
    [InlineData(null, null, null, false)]
    public void ValidaLaJerarquiaCompletaSinAdivinarIds(string? departamento, string? municipio, string? distrito, bool esperado)
        => Assert.Equal(esperado, CatalogoTerritorialFiscal.EsValida(departamento, municipio, distrito));

    [Fact]
    public void CodigosDeDistritoRepetidosNoMezclanDepartamentos()
    {
        var ahuachapan = CatalogoTerritorialFiscal.Distritos("01", "14").Single(e => e.Codigo == "01");
        var otro = CatalogoTerritorialFiscal.Entradas.First(e => e.Catalogo == "008" && e.Codigo == "01" && e.Ambito != "01");
        Assert.Equal("AHUACHAPÁN", ahuachapan.Valor);
        Assert.NotEqual(ahuachapan.Valor, otro.Valor);
        Assert.NotEqual(CatalogoTerritorialFiscal.Formatear("01", "14", "01", null),
            CatalogoTerritorialFiscal.Formatear(otro.Ambito, otro.MunicipioCodigo, "01", null));
    }

    [Fact]
    public void MunicipioLegacyPorSiSoloNoCompletaElPerfilFiscal()
    {
        var alumno = new Alumno { MunicipioId = 123, DireccionDeResidencia = "Residencia de prueba" };
        Assert.False(UbicacionAlumnoFiscal.EsCompleta(alumno));
        alumno.CodigoDepartamentoFiscal = "05";
        alumno.CodigoMunicipioFiscal = "28";
        alumno.CodigoDistritoFiscal = "11";
        Assert.True(UbicacionAlumnoFiscal.EsCompleta(alumno));
        alumno.DireccionDeResidencia = "";
        Assert.False(UbicacionAlumnoFiscal.EsCompleta(alumno));
    }

    [Fact]
    public void PdfMuestraNombresYCodigosDelMunicipioYDistritoExactos()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var direccion = new JObject { ["departamento"] = "05", ["municipio"] = "28", ["distrito"] = "11", ["complemento"] = "Residencia de prueba" };
        var json = new JObject
        {
            ["identificacion"] = new JObject { ["tipoDte"] = "01", ["ambiente"] = "00", ["codigoGeneracion"] = Guid.NewGuid().ToString(), ["fecEmi"] = "2026-11-02" },
            ["emisor"] = new JObject { ["nombre"] = "Emisor sintético", ["direccion"] = direccion.DeepClone() },
            ["receptor"] = new JObject { ["nombre"] = "Alumno sintético", ["direccion"] = direccion.DeepClone() }
        };
        var pdf = new DtePdfService().Generar("01", json.ToString(), "SELLO-SINTETICO");
        using var lector = new PdfReader(new MemoryStream(pdf));
        using var documento = new PdfDocument(lector);
        var texto = string.Join("\n", Enumerable.Range(1, documento.GetNumberOfPages()).Select(i => PdfTextExtractor.GetTextFromPage(documento.GetPage(i))));
        Assert.Contains("Santa Tecla (11)", texto);
        Assert.Contains("La Libertad Sur (28)", texto);
        Assert.Contains("La Libertad (05)", texto);
    }

    [Fact]
    public void PdfHistoricoSinDistritoNoReinterpretaElMunicipioAntiguo()
    {
        var texto = CatalogoTerritorialFiscal.Formatear("05", "11", null, "Dirección histórica");
        Assert.Contains("Municipio 11 (sin correspondencia fiscal confirmada)", texto);
        Assert.DoesNotContain("Santa Tecla", texto);
    }

    [Theory]
    [InlineData("05", "26", "11", "Residencia de prueba")]
    [InlineData("05", "28", "11", "")]
    public async Task ActualizacionInvalidaNoAbreConexion(string departamento, string municipio, string distrito, string direccion)
    {
        var bloqueo = new SinConexiones();
        await using var contexto = ContextoAislado(bloqueo);
        var resultado = await new UbicacionFiscalController(contexto).Guardar(23,
            new(departamento, municipio, distrito, direccion, null), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, bloqueo.Intentos);
    }

    [Fact]
    public void ActualizacionEstaProtegidaPorRolesYAntiforgery()
    {
        var roles = typeof(UbicacionFiscalController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        Assert.Contains("Contabilidad", roles);
        Assert.DoesNotContain("Estudiantes", roles);
        Assert.NotNull(typeof(UbicacionFiscalController).GetMethod(nameof(UbicacionFiscalController.Guardar))!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void MigracionAgregaSoloTresColumnasSinConvertirDatosNiConectarse()
    {
        var bloqueo = new SinConexiones();
        using var contexto = ContextoAislado(bloqueo);
        var sql = contexto.GetService<IMigrator>().GenerateScript("20260904025147_EndurecerJerarquiaFiscalYERET", "20261102220000_UbicacionFiscalAlumno");
        foreach (var nombre in new[] { "CodigoDepartamentoFiscal", "CodigoMunicipioFiscal", "CodigoDistritoFiscal" })
        {
            Assert.Contains($"ALTER TABLE [Alumno] ADD [{nombre}] nvarchar(2) NULL", sql);
            var propiedad = contexto.Model.FindEntityType(typeof(Alumno))!.FindProperty(nombre)!;
            Assert.True(propiedad.IsNullable);
            Assert.Equal(2, propiedad.GetMaxLength());
            var destino = new SRAUMOAR.Migrations.UbicacionFiscalAlumno().TargetModel.FindEntityType(typeof(Alumno).FullName!)!.FindProperty(nombre)!;
            Assert.True(destino.IsNullable);
            Assert.Equal(2, destino.GetMaxLength());
        }
        Assert.DoesNotContain("UPDATE [Alumno]", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.Equal(0, bloqueo.Intentos);
    }

    private static Contexto ContextoAislado(SinConexiones bloqueo) => new(new DbContextOptionsBuilder<Contexto>()
        .UseSqlServer("Server=NO-CONECTAR.invalid;Database=SinConexion;Integrated Security=true;TrustServerCertificate=true")
        .AddInterceptors(bloqueo).Options);

    private sealed class SinConexiones : DbConnectionInterceptor
    {
        public int Intentos;
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        { Intentos++; throw new InvalidOperationException("Esta prueba no permite abrir conexiones."); }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        { Intentos++; throw new InvalidOperationException("Esta prueba no permite abrir conexiones."); }
    }
}
