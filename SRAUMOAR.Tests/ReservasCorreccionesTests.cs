using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Pages.facturas;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

// Sólo objetos y dobles: no se construye Contexto, proveedor EF, app ni clientes externos.
public sealed class ReservasCorreccionesTests
{
    private static readonly Type Reglas = typeof(DteNotaService).Assembly.GetType(
        "SRAUMOAR.Servicios.FacturacionElectronica.DteReservaReglas", throwOnError: true)!;

    [Theory]
    [InlineData(DteEstados.Preparado, true)]
    [InlineData(DteEstados.Enviado, true)]
    [InlineData(DteEstados.Incierto, true)]
    [InlineData(DteEstados.Aceptado, true)]
    [InlineData(DteEstados.Anulando, true)]
    [InlineData(DteEstados.Anulado, false)]
    [InlineData(DteEstados.Rechazado, false)]
    public void CreditoReservaTodosLosEstadosNoLiberados(string estado, bool reserva)
    {
        var predicado = PredicadoCredito();
        var nota = new Factura { TipoDteCodigo = "05", EstadoTransmision = estado, MontoNota = 10m };
        Assert.Equal(reserva, predicado(nota));
        nota.Anulada = true;
        Assert.False(predicado(nota));
        nota.Anulada = false;
        nota.TipoDteCodigo = "06";
        Assert.False(predicado(nota));
    }

    [Fact]
    public void CreditoHistoricoSinTipoCanonicoTambienReserva()
    {
        Assert.True(PredicadoCredito()(new Factura { TipoDTE = 5, SelloRecepcion = "SELLO-SINTETICO" }));
    }

    [Fact]
    public void SaldoCreditoIncluyePendientesYNoPermiteExcesoDeUnCentavo()
    {
        var notas = new[]
        {
            Nota(DteEstados.Preparado, 60m), Nota(DteEstados.Enviado, 15m),
            Nota(DteEstados.Incierto, 10m), Nota(DteEstados.Aceptado, 5m),
            Nota(DteEstados.Anulando, 5m), Nota(DteEstados.Rechazado, 100m),
            Nota(DteEstados.Anulado, 100m), Nota(DteEstados.Preparado, 100m, anulada: true)
        };
        var reservado = notas.Where(PredicadoCredito()).Sum(x => x.MontoNota ?? x.TotalPagar);
        Assert.Equal(95m, reservado);
        Invocar(Reglas, "ValidarMontoCredito", 100m, reservado, 5m);
        Assert.Throws<InvalidOperationException>(() => Invocar(Reglas, "ValidarMontoCredito", 100m, reservado, 5.01m));
    }

    [Fact]
    public void SegundaReservaDebeUsarSaldoActualNoLaLecturaPrevia()
    {
        Invocar(Reglas, "ValidarMontoCredito", 100m, 0m, 60m);
        Assert.Throws<InvalidOperationException>(() => Invocar(Reglas, "ValidarMontoCredito", 100m, 60m, 41m));
        Invocar(Reglas, "ValidarMontoCredito", 100m, 60m, 40m);
    }

    [Theory]
    [InlineData(DteEstados.Preparado, "SELLO-SINTETICO", false, true)]
    [InlineData(DteEstados.Aceptado, "SELLO-SINTETICO", false, true)]
    [InlineData(DteEstados.Preparado, null, false, false)]
    [InlineData(DteEstados.Aceptado, " ", false, false)]
    [InlineData(DteEstados.Preparado, "SELLO-SINTETICO", true, false)]
    [InlineData(DteEstados.Anulando, "SELLO-SINTETICO", false, false)]
    [InlineData(DteEstados.Anulado, "SELLO-SINTETICO", false, false)]
    [InlineData(DteEstados.Rechazado, "SELLO-SINTETICO", false, false)]
    [InlineData(DteEstados.Enviado, "SELLO-SINTETICO", false, false)]
    [InlineData(DteEstados.Incierto, "SELLO-SINTETICO", false, false)]
    public void OriginalHistoricoPreparadoConSelloEsAceptadoSinMigracion(string estado, string? sello, bool anulada, bool aceptado)
    {
        var original = new Factura { EstadoTransmision = estado, SelloRecepcion = sello, Anulada = anulada };
        Assert.Equal(aceptado, (bool)Invocar(Reglas, "EsOriginalAceptadoVigente", original)!);
        Assert.Equal(estado, original.EstadoTransmision);
    }

    [Theory]
    [InlineData(DteEstados.Preparado, 0, true)]
    [InlineData(DteEstados.Preparado, 1, false)]
    [InlineData(DteEstados.Aceptado, 1, true)]
    public void CompatibilidadHistoricaExigeCeroIntentosSiEstaPreparado(string estado, int intentos, bool aceptado)
    {
        var factura = new Factura { EstadoTransmision = estado, IntentosTransmision = intentos, SelloRecepcion = "SELLO-SINTETICO" };
        Assert.Equal(aceptado, (bool)Invocar(Reglas, "EsOriginalAceptadoVigente", factura)!);
        var resultado = (DteRetornoResultado)Invocar(typeof(DteRetornoService), "ResultadoExistente", factura)!;
        Assert.Equal(aceptado, resultado.Aceptado);
        Assert.Equal(aceptado ? DteEstados.Aceptado : estado, resultado.Estado);
    }

    [Theory]
    [InlineData(0, DteEstados.Enviado, false)]
    [InlineData(0, DteEstados.Preparado, false)]
    [InlineData(0, DteEstados.Aceptado, false)]
    [InlineData(1, DteEstados.Enviado, true)]
    [InlineData(1, DteEstados.Anulando, false)]
    [InlineData(1, DteEstados.Aceptado, false)]
    public void SinClaimNoSeTransmiteNiSePersisteRespuestaAunSiSeObservaEnviado(int reclamada, string estado, bool transmitir)
    {
        var factura = new Factura { EstadoTransmision = estado };
        Assert.Equal(transmitir, (bool)Invocar(Reglas, "PuedeTransmitirReclamada", reclamada, factura)!);
        factura.Anulada = true;
        Assert.False((bool)Invocar(Reglas, "PuedeTransmitirReclamada", reclamada, factura)!);
    }

    [Fact]
    public void DosOperacionesIgualesTienenIdentidadDistintaPeroMismoHash()
    {
        var primera = Solicitud();
        var segunda = primera with { OperacionId = Guid.NewGuid() };
        Assert.NotEqual(Clave(primera), Clave(segunda));
        Assert.Equal(Hash(primera), Hash(segunda));
        Assert.Equal(100, Clave(primera).Length); // Campo existente, sin migración.
        Assert.Equal(Clave(primera), Clave(primera with { }));
        Invocar(typeof(DteRetornoService), "ValidarContenidoOperacion", Clave(primera), Clave(primera));
    }

    [Fact]
    public void MismaOperacionConContenidoDistintoEsRechazada()
    {
        var original = Solicitud();
        var cambios = new[]
        {
            original with { FacturaOriginalId = 99 }, original with { TipoRetorno = "2" },
            original with { Motivo = "Otro motivo" },
            original with { Lineas = [new(1, 2m, 8.5m)] },
            original with { Lineas = [new(1, 1m, 8.6m)] },
            original with { Lineas = [new(1, 1m, null)] },
            original with { Lineas = [new(2, 1m, 8.5m)] }
        };
        foreach (var cambio in cambios)
        {
            Assert.Equal(original.OperacionId, cambio.OperacionId);
            Assert.NotEqual(Hash(original), Hash(cambio));
            Assert.Throws<InvalidOperationException>(() => Invocar(typeof(DteRetornoService),
                "ValidarContenidoOperacion", Clave(original), Clave(cambio)));
        }
    }

    [Fact]
    public void HashNoDependeDeCulturaOrdenNiEscalaDecimal()
    {
        var cultura = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
            var solicitud = Solicitud() with { Lineas = [new(2, 1.25m, 7.50m), new(1, 2m, null)] };
            var esperado = Clave(solicitud);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(esperado, Clave(solicitud with
            {
                Motivo = "  " + solicitud.Motivo + "  ",
                Lineas = [new(1, 2.0000m, null), new(2, 1.250000m, 7.5m)]
            }));
        }
        finally { CultureInfo.CurrentCulture = cultura; }
    }

    [Fact]
    public void OperacionVaciaYLineasInvalidasSeRechazanSinConsultarDatos()
    {
        var solicitud = Solicitud();
        Invocar(typeof(DteRetornoService), "ValidarSolicitud", solicitud);
        foreach (var invalida in new[]
        {
            solicitud with { OperacionId = Guid.Empty },
            solicitud with { Lineas = [new(1, 0m)] },
            solicitud with { Lineas = [new(1, 1m, -1m)] },
            solicitud with { Lineas = [new(1, 1m), new(1, 1m)] },
            solicitud with { Lineas = [new(1, 1.123456789m)] }
        })
            Assert.Throws<InvalidOperationException>(() => Invocar(typeof(DteRetornoService), "ValidarSolicitud", invalida));
    }

    [Fact]
    public void DosRetornosParcialesConsumenSaldoAunqueTenganIgualContenido()
    {
        Invocar(typeof(DteRetornoService), "ValidarSaldoRetorno", 2m, 0m, 1m);
        Invocar(typeof(DteRetornoService), "ValidarSaldoRetorno", 2m, 1m, 1m);
        Assert.Throws<InvalidOperationException>(() => Invocar(typeof(DteRetornoService), "ValidarSaldoRetorno", 2m, 2m, 1m));
    }

    [Theory]
    [InlineData("seguro", "totalSeguro", 20, 10)]
    [InlineData("flete", "totalFlete", 30, 15)]
    public void Origen11UsaSeguroYFleteDelResumenExportacion(string campo, string total, int importe, int esperado)
    {
        var resumen = new JObject { [campo] = importe, [total] = 999m };
        Assert.Equal((decimal)esperado, Cargo(resumen, "11", campo, total, 0m, 0.5m));
        Assert.Equal(3m, Cargo(resumen, "11", campo, total, 3m, 0.5m)); // No duplicar cargos por ítem.
        resumen[campo] = null;
        Assert.Equal(499.5m, Cargo(resumen, "11", campo, total, 0m, 0.5m));
        resumen[campo] = 0m;
        Assert.Equal(0m, Cargo(resumen, "11", campo, total, 0m, 0.5m)); // Cero explícito no es ausencia.
    }

    [Fact]
    public void SeguroFleteExportacionCompletaYParcialMantienenImportes()
    {
        var resumen = new JObject { ["seguro"] = 20m, ["flete"] = 30m };
        Assert.Equal(150m, 100m + Cargo(resumen, "11", "seguro", "totalSeguro", 0m, 1m)
            + Cargo(resumen, "11", "flete", "totalFlete", 0m, 1m));
        Assert.Equal(75m, 50m + Cargo(resumen, "11", "seguro", "totalSeguro", 0m, 0.5m)
            + Cargo(resumen, "11", "flete", "totalFlete", 0m, 0.5m));
        Assert.Equal(0m, Cargo(resumen, "01", "seguro", "totalSeguro", 0m, 1m));
    }

    [Fact]
    public void OverrideHistoricoClonaSoloUbicacionReceptor()
    {
        var original = JObject.Parse("""
            {"identificacion":{"tipoDte":"03"},"emisor":{"direccion":{"departamento":"06","municipio":"14","distrito":"01"}},
             "receptor":{"nombre":"Receptor sintético","direccion":{"departamento":"06","municipio":"14","complemento":"Dirección original"}},
             "cuerpoDocumento":[{"precioUni":12.50,"cantidad":2}]}
            """);
        var antes = original.ToString();
        var copia = (JObject)Invocar(typeof(DteNotaService), "ClonarConDireccionReceptor", original, "05", "02", "03")!;
        Assert.Equal(antes, original.ToString());
        Assert.Equal("05", copia["receptor"]!["direccion"]!.Value<string>("departamento"));
        Assert.Equal("02", copia["receptor"]!["direccion"]!.Value<string>("municipio"));
        Assert.Equal("03", copia["receptor"]!["direccion"]!.Value<string>("distrito"));
        Assert.Equal("Dirección original", copia["receptor"]!["direccion"]!.Value<string>("complemento"));
        Assert.True(JToken.DeepEquals(original["emisor"], copia["emisor"]));
        Assert.True(JToken.DeepEquals(original["cuerpoDocumento"], copia["cuerpoDocumento"]));
    }

    [Fact]
    public void OverrideHistoricoSinDireccionCreaDireccionEnLaCopia()
    {
        var original = JObject.Parse("""{"receptor":{"nombre":"Receptor sintético"}}""");
        var copia = (JObject)Invocar(typeof(DteNotaService), "ClonarConDireccionReceptor", original, "05", "02", "03")!;
        Assert.Null(original["receptor"]!["direccion"]);
        Assert.Equal("05", copia["receptor"]!["direccion"]!.Value<string>("departamento"));
        Assert.Equal("02", copia["receptor"]!["direccion"]!.Value<string>("municipio"));
        Assert.Equal("03", copia["receptor"]!["direccion"]!.Value<string>("distrito"));
    }

    [Fact]
    public void TotalesProvienenDelJsonNotaIncluidoIvaLegacyYNetoDistintoDeOperacion()
    {
        const string jsonNota = """
            {"resumen":{"totalGravada":50.00,"totalExenta":0.00,"totalIva":6.50,
            "montoTotalOperacion":56.50,"ivaRete1":0.50,"totalPagar":56.00}}
            """;
        var totales = ((decimal TotalGravado, decimal TotalExento, decimal TotalIva, decimal TotalPagar))
            Invocar(typeof(DteNotaService), "LeerTotalesNota", jsonNota)!;
        Assert.Equal(50m, totales.TotalGravado);
        Assert.Equal(0m, totales.TotalExento);
        Assert.Equal(6.5m, totales.TotalIva);
        Assert.Equal(56m, totales.TotalPagar);
        Assert.NotEqual(JObject.Parse(jsonNota)["resumen"]!.Value<decimal>("montoTotalOperacion"), totales.TotalPagar);
    }

    [Theory]
    [InlineData("totalGravada")]
    [InlineData("totalExenta")]
    [InlineData("totalIva")]
    [InlineData("totalPagar")]
    public void TotalesGeneradosFaltantesNoSeSustituyenSilenciosamentePorCero(string campo)
    {
        var nota = JObject.Parse("""{"resumen":{"totalGravada":50,"totalExenta":0,"totalIva":6.5,"totalPagar":56}}""");
        ((JObject)nota["resumen"]!).Remove(campo);
        Assert.Throws<InvalidOperationException>(() => Invocar(typeof(DteNotaService), "LeerTotalesNota", nota.ToString()));
    }

    [Fact]
    public void ReceptorConDistritoNoSeSobrescribeYSinDistritoExigeSeleccionCompleta()
    {
        var original = JObject.Parse("""{"receptor":{"direccion":{"distrito":"01"}}}""");
        Assert.Null(Invocar(typeof(DteNotaService), "SeleccionReceptorHistorico", original, "99", "99", "99"));
        ((JObject)original["receptor"]!["direccion"]!).Remove("distrito");
        Assert.Throws<InvalidOperationException>(() => Invocar(typeof(DteNotaService), "SeleccionReceptorHistorico", original, "06", "14", null));
        var seleccion = ((string Catalogo, string Codigo)[])Invocar(typeof(DteNotaService),
            "SeleccionReceptorHistorico", original, " 06 ", "14", "01")!;
        Assert.Equal(new[] { ("CAT-012", "06"), ("CAT-013", "14"), ("CAT-008", "01") }, seleccion);
    }

    [Theory]
    [InlineData("CAT-012")]
    [InlineData("CAT-013")]
    [InlineData("CAT-008")]
    public void SeleccionSoloAdmiteCodigoImportadoActivoVersion11(string catalogo)
    {
        var predicado = ((Expression<Func<CatalogoFeItem, bool>>)Invocar(typeof(DteNotaService),
            "CodigoCatalogoValido", catalogo, "01")!).Compile();
        var item = new CatalogoFeItem { CodigoCatalogo = catalogo, Codigo = "01", Version = "1.1", Activo = true };
        Assert.True(predicado(item));
        item.Activo = false;
        Assert.False(predicado(item));
        item.Activo = true;
        item.Version = "1.0";
        Assert.False(predicado(item));
        item.Version = "1.1";
        item.Codigo = "99";
        Assert.False(predicado(item));
        item.Codigo = "01";
        item.CodigoCatalogo = "LEGACY";
        Assert.False(predicado(item));
    }

    [Theory]
    [InlineData(DteEstados.Preparado)]
    [InlineData(DteEstados.Enviado)]
    [InlineData(DteEstados.Incierto)]
    [InlineData(DteEstados.Rechazado)]
    public async Task PostNoAceptadoConservaIdentidadSeleccionPrecioCantidadYMotivo(string estado)
    {
        var servicio = new RetornosDobles { Estado = estado };
        var pagina = Pagina(servicio);
        await pagina.OnGetAsync(default);
        var operacion = pagina.OperacionId;
        PrepararEntrada(pagina);
        await pagina.OnPostAsync(default);
        Assert.Equal(operacion, pagina.OperacionId);
        Assert.Equal("Motivo de prueba", pagina.Motivo);
        Assert.Equal("2", pagina.TipoRetorno);
        Assert.True(pagina.Lineas[0].Seleccionada);
        Assert.Equal(1m, pagina.Lineas[0].Cantidad);
        Assert.Equal(8.5m, pagina.Lineas[0].PrecioUnitario);
        Assert.Equal(6.25m, pagina.Lineas[1].PrecioUnitario);
        Assert.Equal(0m, pagina.Lineas[0].CantidadDisponible);
        await pagina.OnPostAsync(default); // Saldo cero de la propia reserva no impide reintentar.
        Assert.Equal(2, servicio.Solicitudes.Count);
        Assert.All(servicio.Solicitudes, x => Assert.Equal(operacion, x.OperacionId));
        Assert.All(servicio.Solicitudes, x => Assert.Equal(8.5m, x.Lineas.Single().PrecioUnitario));
    }

    [Fact]
    public async Task PostAceptadoRenuevaGuidYLimpiaModelStateAnterior()
    {
        var servicio = new RetornosDobles { Estado = DteEstados.Aceptado };
        var pagina = Pagina(servicio);
        await pagina.OnGetAsync(default);
        var operacion = pagina.OperacionId;
        PrepararEntrada(pagina);
        pagina.ModelState.SetModelValue(nameof(pagina.OperacionId), operacion.ToString(), operacion.ToString());
        // SetModelValue deja Unvalidated; el pipeline MVC normalmente valida antes
        // del handler, pero aquí lo invocamos directamente con un doble.
        pagina.ModelState.MarkFieldValid(nameof(pagina.OperacionId));
        Assert.True(pagina.ModelState.IsValid);
        await pagina.OnPostAsync(default);
        Assert.NotEqual(Guid.Empty, pagina.OperacionId);
        Assert.NotEqual(operacion, pagina.OperacionId);
        Assert.Equal(operacion, servicio.Solicitudes.Single().OperacionId);
        Assert.Empty(pagina.ModelState);
        Assert.Empty(pagina.Motivo);
        Assert.All(pagina.Lineas, x => Assert.False(x.Seleccionada));
    }

    [Fact]
    public async Task PostConErrorDeValidacionNoTransmiteNiPierdeEntradas()
    {
        var servicio = new RetornosDobles();
        var pagina = Pagina(servicio);
        await pagina.OnGetAsync(default);
        PrepararEntrada(pagina);
        var operacion = pagina.OperacionId;
        pagina.ModelState.AddModelError("Motivo", "Error simulado");
        await pagina.OnPostAsync(default);
        Assert.Empty(servicio.Solicitudes);
        Assert.Equal(operacion, pagina.OperacionId);
        Assert.Equal(8.5m, pagina.Lineas[0].PrecioUnitario);
        Assert.Equal(6.25m, pagina.Lineas[1].PrecioUnitario);
    }

    [Fact]
    public async Task PostSinGuidNoGeneraUnoSilenciosamenteNiTransmite()
    {
        var servicio = new RetornosDobles();
        var pagina = Pagina(servicio);
        await pagina.OnGetAsync(default);
        PrepararEntrada(pagina);
        pagina.OperacionId = Guid.Empty;
        await pagina.OnPostAsync(default);
        Assert.Empty(servicio.Solicitudes);
        Assert.Equal(Guid.Empty, pagina.OperacionId);
        Assert.False(pagina.ModelState.IsValid);
    }

    private static void PrepararEntrada(RetornoModel pagina)
    {
        pagina.Motivo = "Motivo de prueba";
        pagina.TipoRetorno = "2";
        pagina.Lineas[0].Seleccionada = true;
        pagina.Lineas[0].Cantidad = 1m;
        pagina.Lineas[0].PrecioUnitario = 8.5m;
        pagina.Lineas[1].PrecioUnitario = 6.25m;
    }

    private static RetornoModel Pagina(IDteRetornoService servicio) => new(servicio)
    {
        FacturaId = 7,
        PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext(),
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        }
    };

    private sealed class RetornosDobles : IDteRetornoService
    {
        public string Estado { get; init; } = DteEstados.Incierto;
        public List<DteRetornoRequest> Solicitudes { get; } = [];

        public Task<DteRetornoDisponible> ObtenerDisponibleAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(new DteRetornoDisponible(id, "11", "CODIGO-SINTETICO", "00",
                [new(1, "P1", "Producto", 2m, Solicitudes.Count > 0 ? 2m : 0m, Solicitudes.Count > 0 ? 0m : 2m, 10m, "ventaGravada"),
                 new(2, "P2", "Otro producto", 5m, 0m, 5m, 7m, "ventaGravada")]));

        public Task<DteRetornoPreparado> PrepararAsync(DteRetornoRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException("La página no debe preparar por separado ni acceder a proveedores.");

        public Task<DteRetornoResultado> EmitirAsync(DteRetornoRequest request, CancellationToken ct = default)
        {
            Solicitudes.Add(request);
            return Task.FromResult(new DteRetornoResultado(100, "CODIGO-SINTETICO", "CONTROL-SINTETICO",
                Estado == DteEstados.Aceptado, Estado, "00", Estado == DteEstados.Aceptado ? "SELLO-SINTETICO" : null, null));
        }
    }

    private static Factura Nota(string estado, decimal monto, bool anulada = false) =>
        new() { TipoDteCodigo = "05", EstadoTransmision = estado, MontoNota = monto, Anulada = anulada };

    private static Func<Factura, bool> PredicadoCredito() =>
        ((Expression<Func<Factura, bool>>)Reglas.GetField("ReservaCredito", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Compile();

    private static DteRetornoRequest Solicitud() => new(7, "1", "Motivo", [new(1, 1m, 8.5m)], Guid.NewGuid());
    private static string Clave(DteRetornoRequest request) => (string)Invocar(typeof(DteRetornoService), "ClaveIdempotencia", request)!;
    private static string Hash(DteRetornoRequest request) => (string)Invocar(typeof(DteRetornoService), "HashContenido", request)!;
    private static decimal Cargo(JObject resumen, string tipo, string campo, string total, decimal items, decimal proporcion) =>
        (decimal)Invocar(typeof(DteRetornoService), "CargoRetornado", resumen, tipo, campo, total, items, proporcion)!;

    private static object? Invocar(Type tipo, string metodo, params object?[] argumentos)
    {
        var implementacion = tipo.GetMethod(metodo, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró {tipo.Name}.{metodo} en el ensamblado de prueba. Compruebe que no sea una compilación intermedia.");
        try { return implementacion.Invoke(null, argumentos); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
