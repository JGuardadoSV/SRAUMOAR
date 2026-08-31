using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Pages.aranceles;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

// Pruebas puras: no se crea Contexto ni proveedor de BD, host, renderer PDF o SMTP.
// Reflection permite probar los helpers internos y los metadatos de autorización
// sin ampliar la API pública. No sustituye una prueba de integración de sp_getapplock.
public sealed class CorreoCorreccionesTests
{
    private static readonly DateTime Ahora = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Pendiente", null, null, true)]
    [InlineData("Pendiente", -1, -1, true)]
    [InlineData("Pendiente", 0, 0, true)]
    [InlineData("Pendiente", 1, -1, false)]
    [InlineData("Pendiente", -1, 1, false)]
    [InlineData("Procesando", -1, -1, true)]
    [InlineData("Procesando", 0, 1, true)]
    [InlineData("Procesando", null, 1, true)]
    [InlineData("Procesando", 1, -1, false)]
    [InlineData("Enviado", -1, -1, false)]
    [InlineData("Error", null, null, false)]
    [InlineData("Otro", null, null, false)]
    public void WorkerSeleccionaPendientesOProcesandoVencidos(
        string estado, int? leaseMinutos, int? proximoMinutos, bool esperado)
    {
        var correo = new ColaCorreoDte
        {
            Estado = estado,
            BloqueadoHasta = leaseMinutos.HasValue ? Ahora.AddMinutes(leaseMinutos.Value) : null,
            ProximoIntento = proximoMinutos.HasValue ? Ahora.AddMinutes(proximoMinutos.Value) : null
        };
        var filtro = Invocar<Expression<Func<ColaCorreoDte, bool>>>(
            typeof(DteEmailQueueService), "FiltroElegibles", Ahora).Compile();

        Assert.Equal(esperado, filtro(correo));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(null)]
    public void ReenvioRecuperaLeaseSinBorrarAuditoria(int? leaseMinutos)
    {
        var correo = CrearCorreoAuditable();
        correo.BloqueadoHasta = leaseMinutos.HasValue ? Ahora.AddMinutes(leaseMinutos.Value) : null;
        var antes = JObject.FromObject(correo);

        Assert.True(Recuperar(correo));
        Assert.Equal("Pendiente", correo.Estado);
        Assert.Equal(Ahora, correo.ProximoIntento);
        Assert.Null(correo.BloqueadoHasta);
        AssertAuditoriaIntacta(antes, correo);
        Assert.False(Recuperar(correo)); // Recuperar de nuevo no crea otra entrega.
    }

    [Theory]
    [InlineData("Procesando", 1)]
    [InlineData("Pendiente", -1)]
    [InlineData("Enviado", -1)]
    [InlineData("Error", -1)]
    public void ReenvioNoRecuperaLeaseVigenteNiModificaEstadosAjenos(string estado, int minutos)
    {
        var correo = CrearCorreoAuditable();
        correo.Estado = estado;
        correo.BloqueadoHasta = Ahora.AddMinutes(minutos);
        var antes = JObject.FromObject(correo);

        Assert.False(Recuperar(correo));
        Assert.True(JToken.DeepEquals(antes, JObject.FromObject(correo)));
    }

    [Fact]
    public void WorkerYReenviosDeLaMismaEntregaCompartenExclusion()
    {
        var clave = new string('A', 64);
        var recursoOriginal = Recurso(clave);
        Assert.Equal(recursoOriginal, Recurso(clave + ":" + new string('B', 32)));
        Assert.Equal(recursoOriginal, Recurso(clave + ":" + new string('C', 32)));
        Assert.NotEqual(recursoOriginal, Recurso(new string('D', 64)));
    }

    [Theory]
    [InlineData("{ 'ventaGravada': 0, 'ventaExenta': 12.50, 'ventaNoSuj': 0 }", "12.50")]
    [InlineData("{ 'ventaGravada': 0, 'ventaExenta': 0, 'ventaNoSuj': 7.25 }", "7.25")]
    [InlineData("{ 'ventaGravada': 10, 'ventaExenta': 20, 'ventaNoSuj': 30, 'compra': 40 }", "100")]
    [InlineData("{ 'ventaGravada': null, 'ventaExenta': 3.5 }", "3.5")]
    [InlineData("{ 'ventaGravada': 0, 'compra': 18.75 }", "18.75")]
    [InlineData("{ 'compra': 9 }", "9")]
    [InlineData("{ 'valor': 8.50 }", "8.50")]
    [InlineData("{ 'ventaGravada': null, 'valor': 8.50 }", "8.50")]
    [InlineData("{ 'ventaGravada': 0, 'valor': 99 }", "0")]
    [InlineData("{}", "0")]
    public void DetalleSumaClasificacionesSinTomarElPrimerCero(string json, string esperado)
    {
        var resultado = Invocar<string>(typeof(DtePdfService), "ObtenerMontoItem", JObject.Parse(json));
        Assert.Equal(decimal.Parse(esperado, CultureInfo.InvariantCulture),
            decimal.Parse(resultado, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CalculoDeDetalleEsIndependienteDeLaCultura()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
            Assert.Equal("3.75", Invocar<string>(typeof(DtePdfService), "ObtenerMontoItem",
                JObject.Parse("{ 'ventaGravada': 0, 'ventaExenta': 1.25, 'ventaNoSuj': 2.50 }")));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void HistorialDeclaraExactamenteLosTresRolesYNoPermiteAnonimos()
    {
        var tipo = typeof(HistorialPagosModel);
        var permiso = Assert.Single(tipo.GetCustomAttributes<AuthorizeAttribute>(true));
        Assert.Equal(new[] { "Administrador", "Administracion", "Contabilidad" }, permiso.Roles!.Split(','));
        Assert.Empty(tipo.GetCustomAttributes<AllowAnonymousAttribute>(true));
        foreach (var metodo in tipo.GetMethods().Where(x => x.Name.StartsWith("OnGet") || x.Name.StartsWith("OnPost")))
            Assert.Empty(metodo.GetCustomAttributes<AllowAnonymousAttribute>(true));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Alumno", true)]
    [InlineData("Docente", true)]
    [InlineData("Administrador", false)]
    public async Task TodosLosHandlersRechazanSinPermisosAntesDeAccederADatos(string? rol, bool autenticado)
    {
        var pagina = CrearPagina(rol, autenticado);
        pagina.AlumnoId = 123;
        pagina.FiltroAlumno = "no consultar";

        Assert.IsType<ForbidResult>(await pagina.OnGetAsync());
        Assert.IsType<ForbidResult>(await pagina.OnPostBuscarAlumnoAsync());
        Assert.IsType<ForbidResult>(await pagina.OnPostEliminarPagoAsync("cualquier-codigo", 123));
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Administracion")]
    [InlineData("Contabilidad")]
    public async Task RolesAutorizadosPuedenAbrirHistorialSinSeleccionarAlumno(string rol)
    {
        var pagina = CrearPagina(rol, true);
        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.IsType<PageResult>(await pagina.OnPostBuscarAlumnoAsync());
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Administracion")]
    [InlineData("Contabilidad")]
    public async Task NiUnRolAutorizadoPuedeBorrarPorCodigoNiEludirLaRutaFiscal(string rol)
    {
        var pagina = CrearPagina(rol, true);
        foreach (var codigo in new[] { null, "", " ", "legacy", "00000000-0000-0000-0000-000000000001" })
        foreach (var alumno in new int?[] { null, 0, 123 })
        {
            var resultado = Assert.IsType<BadRequestObjectResult>(
                await pagina.OnPostEliminarPagoAsync(codigo!, alumno));
            Assert.Contains("ruta fiscal", Assert.IsType<string>(resultado.Value));
        }
    }

    private static HistorialPagosModel CrearPagina(string? rol, bool autenticado)
    {
        var claims = rol is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, rol) };
        // null! es deliberado: cualquier acceso accidental a BD haría fallar el test.
        return new HistorialPagosModel(null!)
        {
            PageContext = new PageContext
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, autenticado ? "Prueba" : null))
                }
            }
        };
    }

    private static ColaCorreoDte CrearCorreoAuditable() => new()
    {
        Id = 123, FacturaId = 456, Estado = "Procesando", Intentos = 4,
        ClaveIdempotencia = new string('A', 64), Destinatario = "prueba@example.invalid",
        CodigoGeneracion = "00000000-0000-0000-0000-000000000001",
        JsonAdjunto = "{}", PdfAdjunto = new byte[] { 1, 2, 3 },
        UltimoError = "Error sintético anterior", FechaCreacion = Ahora.AddDays(-2),
        ProcesadoEn = Ahora.AddDays(-1), ProximoIntento = Ahora.AddHours(1),
        BloqueadoHasta = Ahora.AddMinutes(-1)
    };

    private static void AssertAuditoriaIntacta(JObject antes, ColaCorreoDte despues)
    {
        var actual = JObject.FromObject(despues);
        foreach (var campoOperativo in new[] { "Estado", "ProximoIntento", "BloqueadoHasta" })
        {
            antes.Remove(campoOperativo);
            actual.Remove(campoOperativo);
        }
        Assert.True(JToken.DeepEquals(antes, actual));
    }

    private static bool Recuperar(ColaCorreoDte correo)
        => Invocar<bool>(typeof(DteEmailQueueService), "RecuperarLeaseVencido", correo, Ahora);

    private static string Recurso(string clave)
        => Invocar<string>(typeof(DteEmailQueueService), "RecursoExclusion", clave);

    private static T Invocar<T>(Type tipo, string nombre, params object[] argumentos)
    {
        var metodo = tipo.GetMethod(nombre, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(metodo);
        return (T)metodo.Invoke(null, argumentos)!;
    }
}
