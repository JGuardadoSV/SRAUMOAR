using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteRetornoService
{
    Task<DteRetornoDisponible> ObtenerDisponibleAsync(int facturaOriginalId, CancellationToken cancellationToken = default);
    Task<DteRetornoPreparado> PrepararAsync(DteRetornoRequest request, CancellationToken cancellationToken = default);
    Task<DteRetornoResultado> EmitirAsync(DteRetornoRequest request, CancellationToken cancellationToken = default);
}

public sealed class DteRetornoService : IDteRetornoService
{
    private static readonly HashSet<string> TiposRelacionados = ["01", "11", "14"];
    private static readonly HashSet<string> EstadosReservados = [DteEstados.Preparado, DteEstados.Enviado, DteEstados.Incierto, DteEstados.Aceptado, DteEstados.Anulando];
    private readonly Contexto _contexto;
    private readonly IConfiguracionFeService _configuracion;
    private readonly ICorrelativoService _correlativos;
    private readonly IDteTransmisionService _transmision;
    private readonly IDteEmailQueueService _emailQueue;
    private readonly ILogger<DteRetornoService> _logger;

    public DteRetornoService(Contexto contexto, IConfiguracionFeService configuracion, ICorrelativoService correlativos,
        IDteTransmisionService transmision, IDteEmailQueueService emailQueue, ILogger<DteRetornoService> logger)
    {
        _contexto = contexto;
        _configuracion = configuracion;
        _correlativos = correlativos;
        _transmision = transmision;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<DteRetornoDisponible> ObtenerDisponibleAsync(int facturaOriginalId, CancellationToken cancellationToken = default)
    {
        var (factura, root, tipo, ambiente) = await ObtenerOriginalAsync(facturaOriginalId, cancellationToken);
        var retornado = await CantidadesReservadasAsync(factura.FacturaId, cancellationToken);
        var lineas = Cuerpo(root).Select(item =>
        {
            var numero = Entero(item, "numItem");
            var original = Numero(item, "cantidad");
            var usado = retornado.GetValueOrDefault(numero);
            return new DteRetornoLineaDisponible(numero, Texto(item, "codigo"), Texto(item, "descripcion") ?? "Ítem",
                original, usado, Math.Max(0m, original - usado), Numero(item, "precioUni"), Clasificacion(item));
        }).ToArray();
        return new DteRetornoDisponible(factura.FacturaId, tipo, Identificacion(root).Value<string>("codigoGeneracion") ?? factura.CodigoGeneracion, ambiente, lineas);
    }

    public async Task<DteRetornoPreparado> PrepararAsync(DteRetornoRequest request, CancellationToken cancellationToken = default)
    {
        ValidarSolicitud(request);
        var disponible = await ObtenerDisponibleAsync(request.FacturaOriginalId, cancellationToken);
        var (factura, root, tipoRelacionado, ambiente) = await ObtenerOriginalAsync(request.FacturaOriginalId, cancellationToken);
        var config = await _configuracion.ObtenerAsync(ambiente, cancellationToken);
        if (!config.Entidad.Habilitado) throw new InvalidOperationException($"El ambiente fiscal {ambiente} está deshabilitado.");

        var solicitudes = request.Lineas.GroupBy(x => x.NumeroItemOriginal).ToDictionary(x => x.Key, x => x.Single());
        var disponibles = disponible.Lineas.ToDictionary(x => x.NumeroItemOriginal);
        foreach (var linea in solicitudes.Values)
        {
            if (!disponibles.TryGetValue(linea.NumeroItemOriginal, out var saldo))
                throw new InvalidOperationException($"No existe el ítem original {linea.NumeroItemOriginal}.");
            if (linea.Cantidad <= 0m || linea.Cantidad > saldo.CantidadDisponible)
                throw new InvalidOperationException($"La cantidad del ítem {linea.NumeroItemOriginal} supera el saldo disponible ({saldo.CantidadDisponible}).");
            if (linea.PrecioUnitario is <= 0m) throw new InvalidOperationException("El precio unitario debe ser mayor que cero.");
            if (linea.PrecioUnitario > saldo.PrecioUnitario)
                throw new InvalidOperationException($"El precio del ítem {linea.NumeroItemOriginal} no puede superar el precio unitario original.");
        }

        var codigo = Guid.NewGuid().ToString().ToUpperInvariant();
        var correlativo = await _correlativos.ObtenerSiguienteCorrelativo(DteTipos.EventoRetorno, ambiente,
            config.Entidad.CodigoEstablecimiento, config.Entidad.CodigoPuntoVenta);
        var numeroControl = $"DTE-18-{config.Entidad.CodigoEstablecimiento}{config.Entidad.CodigoPuntoVenta}-{correlativo:D15}";
        var cuerpo = new JArray();
        var preparadas = new List<DteRetornoLineaPreparada>();
        decimal gravada = 0, exenta = 0, noSujeta = 0, compra = 0, iva = 0;
        decimal seguroItems = 0, fleteItems = 0, noGravadoItems = 0, ivaReteItems = 0, reteRentaItems = 0;
        var codigosTributo = new HashSet<string>(StringComparer.Ordinal);
        var identificacionOriginal = Identificacion(root);
        var codigoOriginal = identificacionOriginal.Value<string>("codigoGeneracion")
            ?? throw new InvalidOperationException("El JSON original no contiene código de generación.");
        var resumenOriginal = (JObject?)root["resumen"] ?? new JObject();
        var baseOriginal = BaseOperacionOriginal(root, resumenOriginal);

        foreach (var original in Cuerpo(root))
        {
            var numeroOriginal = Entero(original, "numItem");
            if (!solicitudes.TryGetValue(numeroOriginal, out var solicitada)) continue;
            var cantidadOriginal = Numero(original, "cantidad");
            var proporcion = solicitada.Cantidad / cantidadOriginal;
            var precio = solicitada.PrecioUnitario ?? Numero(original, "precioUni");
            var descuento = Redondear2(Numero(original, "montoDescu") * proporcion);
            var baseLinea = Redondear2(solicitada.Cantidad * precio) - descuento;
            if (baseLinea < 0m) throw new InvalidOperationException($"El precio del ítem {numeroOriginal} no cubre su descuento proporcional.");
            var clasificacion = Clasificacion(original);
            decimal lineaGravada = 0, lineaExenta = 0, lineaNoSujeta = 0, lineaCompra = 0, lineaIva = 0;
            if (clasificacion == "compra") lineaCompra = baseLinea;
            else if (clasificacion == "ventaExenta") lineaExenta = baseLinea;
            else if (clasificacion == "ventaNoSuj") lineaNoSujeta = baseLinea;
            else
            {
                lineaGravada = baseLinea;
                if (tipoRelacionado == "01") lineaIva = Redondear2(baseLinea - baseLinea / 1.13m);
            }
            gravada += lineaGravada; exenta += lineaExenta; noSujeta += lineaNoSujeta; compra += lineaCompra; iva += lineaIva;

            var tributosLinea = (original["tributos"] as JArray)?.Values<string>()
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "20").Select(x => x!).Distinct(StringComparer.Ordinal).ToList() ?? [];
            if (tipoRelacionado == "11" && !tributosLinea.Contains("C3", StringComparer.Ordinal)) tributosLinea.Add("C3");
            foreach (var tributo in tributosLinea) codigosTributo.Add(tributo);
            var seguroLinea = Redondear2(Numero(original, "seguro") * proporcion);
            var fleteLinea = Redondear2(Numero(original, "flete") * proporcion);
            var noGravadoLinea = Redondear2(Numero(original, "noGravado") * proporcion);
            var ivaReteLinea = Redondear2(Numero(original, "ivaRete") * proporcion);
            var reteRentaLinea = Redondear2(Numero(original, "reteRenta") * proporcion);
            seguroItems += seguroLinea; fleteItems += fleteLinea; noGravadoItems += noGravadoLinea;
            ivaReteItems += ivaReteLinea; reteRentaItems += reteRentaLinea;
            cuerpo.Add(new JObject
            {
                ["numItem"] = cuerpo.Count + 1, ["tipoItem"] = original.Value<int?>("tipoItem") ?? 1,
                ["codigoGeneracion"] = codigoOriginal, ["cantidad"] = solicitada.Cantidad,
                ["precioUni"] = precio, ["descripcion"] = Texto(original, "descripcion") ?? "Ítem retornado",
                ["codigo"] = Nulo(Texto(original, "codigo")), ["uniMedida"] = original.Value<int?>("uniMedida") ?? 59,
                ["montoDescu"] = descuento, ["codTributo"] = original["codTributo"]?.DeepClone() ?? JValue.CreateNull(),
                ["ventaNoSuj"] = Redondear2(lineaNoSujeta), ["ventaExenta"] = Redondear2(lineaExenta),
                ["ventaGravada"] = Redondear2(lineaGravada), ["compra"] = Redondear2(lineaCompra),
                ["tributos"] = tributosLinea.Count == 0 ? JValue.CreateNull() : new JArray(tributosLinea), ["psv"] = 0m, ["ivaItem"] = Redondear2(lineaIva),
                ["noGravado"] = noGravadoLinea, ["seguro"] = seguroLinea, ["flete"] = fleteLinea,
                ["ivaRete"] = ivaReteLinea, ["reteRenta"] = reteRentaLinea
            });
            preparadas.Add(new DteRetornoLineaPreparada(numeroOriginal, Texto(original, "codigo"), Texto(original, "descripcion") ?? "Ítem",
                cantidadOriginal, solicitada.Cantidad, precio, clasificacion));
        }

        gravada = Redondear2(gravada); exenta = Redondear2(exenta); noSujeta = Redondear2(noSujeta); compra = Redondear2(compra); iva = Redondear2(iva);
        var subtotal = Redondear2(gravada + exenta + noSujeta + compra);
        var proporcionOperacion = baseOriginal > 0m ? Math.Min(1m, subtotal / baseOriginal) : 0m;
        var seguro = CargoRetornado(resumenOriginal, tipoRelacionado, "seguro", "totalSeguro", seguroItems, proporcionOperacion);
        var flete = CargoRetornado(resumenOriginal, tipoRelacionado, "flete", "totalFlete", fleteItems, proporcionOperacion);
        var noGravado = noGravadoItems != 0m ? Redondear2(noGravadoItems) : Redondear2(Numero(resumenOriginal, "totalNoGravado") * proporcionOperacion);
        var ivaRete = ivaReteItems != 0m ? Redondear2(ivaReteItems) : Redondear2(Numero(resumenOriginal, "ivaRete") * proporcionOperacion);
        var reteRenta = reteRentaItems != 0m ? Redondear2(reteRentaItems) : Redondear2(Numero(resumenOriginal, "reteRenta") * proporcionOperacion);
        var noOnerosas = Redondear2(Numero(resumenOriginal, "totalNoOnerosas") * proporcionOperacion);
        var tributosResumen = CrearTributosResumen(resumenOriginal, codigosTributo, proporcionOperacion);
        var totalTributos = tributosResumen is JArray tributosArray
            ? Redondear2(tributosArray.OfType<JObject>().Sum(x => Numero(x, "valor")))
            : 0m;
        var montoOperacion = Redondear2(subtotal + seguro + flete + noGravado + totalTributos);
        var total = Redondear2(Math.Max(0m, montoOperacion - ivaRete - reteRenta));
        var receptorOriginal = (JObject?)(root["receptor"] ?? root["sujetoExcluido"] ?? root["documento"])
            ?? throw new InvalidOperationException("El documento original no contiene receptor.");
        var emisorOriginal = (JObject?)root["emisor"] ?? throw new InvalidOperationException("El documento original no contiene emisor.");
        var fecha = FechaElSalvador();
        var json = new JObject
        {
            ["identificacion"] = new JObject { ["version"] = 1, ["ambiente"] = ambiente, ["tipoModelo"] = 1, ["tipoOperacion"] = 1,
                ["tipoEvento"] = "18", ["tipoContingencia"] = JValue.CreateNull(), ["motivoContin"] = JValue.CreateNull(),
                ["codigoGeneracion"] = codigo, ["fecEmi"] = fecha.ToString("yyyy-MM-dd"), ["horEmi"] = fecha.ToString("HH:mm:ss"),
                ["fusion"] = JValue.CreateNull(), ["tipoMoneda"] = "USD" },
            ["documentoRelacionado"] = new JArray(new JObject { ["tipoDocumento"] = tipoRelacionado,
                ["codigoGeneracion"] = codigoOriginal, ["fechaEmision"] = identificacionOriginal.Value<string>("fecEmi") ?? factura.Fecha.ToString("yyyy-MM-dd") }),
            ["emisor"] = CrearEmisor(emisorOriginal, config.Entidad),
            ["documento"] = CrearDocumento(receptorOriginal),
            ["ventaTercero"] = JValue.CreateNull(), ["compraTercero"] = JValue.CreateNull(), ["cuerpoDocumento"] = cuerpo,
            ["resumen"] = new JObject { ["totalNoSuj"] = noSujeta, ["totalExenta"] = exenta, ["totalGravada"] = gravada,
                ["totalCompraExcluidos"] = compra, ["subTotalVentas"] = subtotal, ["tributos"] = tributosResumen, ["totalSeguro"] = seguro,
                ["totalFlete"] = flete, ["montoTotalOperacion"] = montoOperacion, ["ivaRete"] = ivaRete, ["reteRenta"] = reteRenta,
                ["totalNoGravado"] = noGravado, ["totalPagar"] = total, ["totalLetras"] = $"{total:0.00} DÓLARES DE LOS ESTADOS UNIDOS DE AMÉRICA",
                ["totalNoOnerosas"] = noOnerosas, ["totalIva"] = iva, ["saldoFavor"] = 0m },
            ["apendice"] = JValue.CreateNull()
        }.ToString(Formatting.None);

        return new DteRetornoPreparado(factura.FacturaId, ambiente, json, codigo, numeroControl, gravada, exenta, total, iva,
            Texto(receptorOriginal, "correo") ?? factura.CorreoReceptor, request.TipoRetorno, request.Motivo.Trim(), preparadas);
    }

    public async Task<DteRetornoResultado> EmitirAsync(DteRetornoRequest request, CancellationToken cancellationToken = default)
    {
        ValidarSolicitud(request);
        var clave = ClaveIdempotencia(request);
        var prefijo = PrefijoOperacion(request.OperacionId);
        var existente = await BuscarOperacionAsync(prefijo, cancellationToken);
        if (existente is not null)
        {
            ValidarContenidoOperacion(existente.ClaveIdempotencia!, clave);
            if (existente.EstadoTransmision != DteEstados.Rechazado)
                return await ReutilizarAsync(existente, cancellationToken);
        }
        DteRetornoPreparado preparada;
        try
        {
            preparada = await PrepararAsync(request, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // El primer POST pudo consumir el saldo entre la consulta de identidad
            // y la preparación del segundo; este último sigue siendo un reintento.
            existente = await BuscarOperacionAsync(prefijo, cancellationToken);
            if (existente is not null)
            {
                ValidarContenidoOperacion(existente.ClaveIdempotencia!, clave);
                if (existente.EstadoTransmision != DteEstados.Rechazado)
                    return await ReutilizarAsync(existente, cancellationToken);
            }
            throw;
        }
        var factura = new Factura
        {
            Fecha = DateTime.UtcNow, CodigoGeneracion = preparada.CodigoGeneracion, NumeroControl = preparada.NumeroControl,
            TipoDTE = 18, TipoDteCodigo = DteTipos.EventoRetorno, AmbienteFiscal = preparada.AmbienteFiscal,
            EstadoTransmision = DteEstados.Preparado, JsonDte = preparada.Json, FacturaOriginalId = preparada.FacturaOriginalId,
            TipoRetorno = preparada.TipoRetorno, MotivoRetorno = preparada.Motivo, ClaveIdempotencia = clave,
            CorreoReceptor = preparada.CorreoReceptor, TotalGravado = preparada.TotalGravado, TotalExento = preparada.TotalExento,
            TotalPagar = preparada.TotalPagar, TotalIva = preparada.TotalIva
        };
        foreach (var l in preparada.Lineas) factura.DetallesRetorno.Add(new DteRetornoDetalle
        {
            NumeroItemOriginal = l.NumeroItemOriginal, Codigo = l.Codigo,
            Descripcion = l.Descripcion, CantidadOriginal = l.CantidadOriginal, CantidadRetornada = l.CantidadRetornada,
            PrecioUnitario = l.PrecioUnitario, ClasificacionFiscal = l.ClasificacionFiscal
        });

        try
        {
            await using var tx = await _contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // El lock usa SOLO la identidad, también si otro POST cambia el original o el payload.
            await _contexto.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @resultado int;
                EXEC @resultado = sys.sp_getapplock @Resource = {prefijo}, @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @resultado < 0 THROW 50001, 'No se pudo reservar la operación ERET; reintente con el mismo identificador.', 1;", cancellationToken);
            existente = await BuscarOperacionAsync(prefijo, cancellationToken);
            if (existente is not null)
                ValidarContenidoOperacion(existente.ClaveIdempotencia!, clave);
            if (existente is null || existente.EstadoTransmision == DteEstados.Rechazado)
            {
                await ValidarReservaAsync(preparada, cancellationToken);
                // Un rechazo libera saldo. Reintentar genera otro DTE, sin perder la asociación
                // operación/contenido: liberar la clave e insertar se confirman juntos.
                if (existente is not null)
                    await _contexto.Facturas.Where(x => x.FacturaId == existente.FacturaId)
                        .ExecuteUpdateAsync(x => x.SetProperty(f => f.ClaveIdempotencia, (string?)null), cancellationToken);
                _contexto.Facturas.Add(factura);
                await _contexto.SaveChangesAsync(cancellationToken);
            }
            else
            {
                factura = existente;
            }
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var detalle in factura.DetallesRetorno)
                _contexto.Entry(detalle).State = EntityState.Detached;
            _contexto.Entry(factura).State = EntityState.Detached;
            existente = await BuscarOperacionAsync(prefijo, cancellationToken);
            if (existente is not null)
            {
                ValidarContenidoOperacion(existente.ClaveIdempotencia!, clave);
                if (existente.EstadoTransmision != DteEstados.Rechazado)
                    return await ReutilizarAsync(existente, cancellationToken);
            }
            throw;
        }
        return await ReutilizarAsync(factura, cancellationToken);
    }

    private Task<Factura?> BuscarOperacionAsync(string prefijo, CancellationToken ct) =>
        _contexto.Facturas.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ClaveIdempotencia != null && x.ClaveIdempotencia.StartsWith(prefijo), ct);

    private Task<DteRetornoResultado> ReutilizarAsync(Factura factura, CancellationToken ct) =>
        !factura.Anulada && !DteReservaReglas.EsOriginalAceptadoVigente(factura)
            && factura.EstadoTransmision is DteEstados.Preparado or DteEstados.Enviado
            ? TransmitirPersistidaAsync(factura.FacturaId, ct)
            : Task.FromResult(ResultadoExistente(factura));

    private async Task<DteRetornoResultado> TransmitirPersistidaAsync(int facturaId, CancellationToken cancellationToken)
    {
        var reclamada = await _contexto.Facturas
            .Where(x => x.FacturaId == facturaId && !x.Anulada && (x.SelloRecepcion == null || x.SelloRecepcion == "")
                && x.TipoDteCodigo == DteTipos.EventoRetorno && x.EstadoTransmision == DteEstados.Preparado)
            .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, DteEstados.Enviado), cancellationToken);
        foreach (var entry in _contexto.ChangeTracker.Entries<Factura>().Where(x => x.Entity.FacturaId == facturaId).ToArray())
            entry.State = EntityState.Detached;
        var factura = await _contexto.Facturas.SingleAsync(x => x.FacturaId == facturaId, cancellationToken);
        // Sólo el dueño del claim puede transmitir y persistir la respuesta. Un segundo
        // POST no debe escribir un Enviado viejo encima de una aceptación concurrente.
        if (!DteReservaReglas.PuedeTransmitirReclamada(reclamada, factura)) return ResultadoExistente(factura);
        if (string.IsNullOrWhiteSpace(factura.JsonDte)) throw new InvalidOperationException("El ERET preparado no contiene JSON.");
        DteTransmisionResult resultado;
        try
        {
            resultado = await _transmision.TransmitirAsync(new DteTransmisionRequest(factura.JsonDte, DteTipos.EventoRetorno,
                factura.CodigoGeneracion, factura.NumeroControl, 1, factura.CorreoReceptor, factura.FacturaId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparando transmisión ERET {Codigo}", factura.CodigoGeneracion);
            resultado = new(false, DteEstados.Incierto, null, string.Empty, 1, factura.AmbienteFiscal ?? string.Empty, ex.Message);
        }
        factura.SelloRecepcion = resultado.SelloRecepcion; factura.EstadoTransmision = resultado.Estado;
        factura.IntentosTransmision = resultado.Intentos; factura.RespuestaHacienda = resultado.Respuesta;
        factura.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _contexto.SaveChangesAsync(cierre.Token);
        if (resultado.Aceptado && !string.IsNullOrWhiteSpace(factura.CorreoReceptor))
            await EncolarCorreoSeguroAsync(factura, factura.JsonDte, resultado.SelloRecepcion, cierre.Token);
        return new(factura.FacturaId, factura.CodigoGeneracion, factura.NumeroControl, resultado.Aceptado, resultado.Estado,
            resultado.AmbienteFiscal, resultado.SelloRecepcion, resultado.Error);
    }

    private async Task<(Factura Factura, JObject Root, string Tipo, string Ambiente)> ObtenerOriginalAsync(int id, CancellationToken ct)
    {
        var factura = await _contexto.Facturas.AsNoTracking().SingleOrDefaultAsync(x => x.FacturaId == id, ct)
            ?? throw new InvalidOperationException("No se encontró el documento original.");
        if (!DteReservaReglas.EsOriginalAceptadoVigente(factura))
            throw new InvalidOperationException("El documento original debe estar aceptado y vigente.");
        if (string.IsNullOrWhiteSpace(factura.JsonDte)) throw new InvalidOperationException("El documento original no tiene JSON DTE.");
        var root = JObject.Parse(factura.JsonDte);
        var identificacion = Identificacion(root);
        var tipo = identificacion.Value<string>("tipoDte") ?? string.Empty;
        if (!TiposRelacionados.Contains(tipo)) throw new InvalidOperationException("El ERET solamente puede relacionarse con DTE 01, 11 o 14.");
        var ambiente = ConfiguracionFeService.NormalizarAmbiente(identificacion.Value<string>("ambiente") ?? string.Empty);
        var codigoJson = identificacion.Value<string>("codigoGeneracion")
            ?? throw new InvalidOperationException("El JSON original no contiene código de generación.");
        if (!string.Equals(codigoJson, factura.CodigoGeneracion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El código de generación persistido no coincide con el JSON original.");
        if (!string.IsNullOrWhiteSpace(factura.AmbienteFiscal)
            && ConfiguracionFeService.NormalizarAmbiente(factura.AmbienteFiscal) != ambiente)
            throw new InvalidOperationException("El ambiente persistido no coincide con el JSON original.");
        return (factura, root, tipo, ambiente);
    }

    private async Task<Dictionary<int, decimal>> CantidadesReservadasAsync(int originalId, CancellationToken ct) =>
        await _contexto.DteRetornoDetalles.AsNoTracking()
            .Where(x => x.Factura.FacturaOriginalId == originalId && !x.Factura.Anulada && EstadosReservados.Contains(x.Factura.EstadoTransmision))
            .GroupBy(x => x.NumeroItemOriginal).Select(x => new { Numero = x.Key, Cantidad = x.Sum(y => y.CantidadRetornada) })
            .ToDictionaryAsync(x => x.Numero, x => x.Cantidad, ct);

    private async Task ValidarReservaAsync(DteRetornoPreparado preparada, CancellationToken ct)
    {
        var original = await DteReservaReglas.BloquearOriginalAsync(_contexto, preparada.FacturaOriginalId, ct);
        if (!DteReservaReglas.EsOriginalAceptadoVigente(original))
            throw new InvalidOperationException("El documento original dejó de estar aceptado y vigente antes de reservar el retorno.");
        var root = JObject.Parse(original.JsonDte ?? throw new InvalidOperationException("Falta el JSON original."));
        var identificacion = Identificacion(root);
        if (ConfiguracionFeService.NormalizarAmbiente(identificacion.Value<string>("ambiente") ?? string.Empty) != preparada.AmbienteFiscal
            || !string.Equals(identificacion.Value<string>("codigoGeneracion"), original.CodigoGeneracion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La identificación del original cambió antes de reservar el retorno.");
        var originales = Cuerpo(root).ToDictionary(x => Entero(x, "numItem"));
        var usadas = await CantidadesReservadasAsync(preparada.FacturaOriginalId, ct);
        foreach (var linea in preparada.Lineas)
        {
            if (!originales.TryGetValue(linea.NumeroItemOriginal, out var item)
                || Numero(item, "cantidad") != linea.CantidadOriginal || linea.PrecioUnitario > Numero(item, "precioUni"))
                throw new InvalidOperationException($"El ítem original {linea.NumeroItemOriginal} cambió antes de reservar el retorno.");
            ValidarSaldoRetorno(Numero(item, "cantidad"), usadas.GetValueOrDefault(linea.NumeroItemOriginal), linea.CantidadRetornada);
        }
    }

    private static void ValidarSaldoRetorno(decimal original, decimal reservado, decimal cantidad)
    {
        if (cantidad <= 0m || reservado + cantidad > original)
            throw new InvalidOperationException("La cantidad retornada supera el saldo disponible del ítem original.");
    }

    private async Task EncolarCorreoSeguroAsync(Factura factura, string json, string? sello, CancellationToken ct)
    {
        try { await _emailQueue.EncolarDteAsync(factura, DteTipos.EventoRetorno, json, sello, factura.CorreoReceptor!, ct); }
        catch (Exception ex) { _logger.LogError(ex, "ERET {FacturaId} aceptado, pero no se pudo encolar su correo", factura.FacturaId); }
    }

    private static JToken CrearTributosResumen(JObject resumenOriginal, IEnumerable<string> codigos, decimal proporcion)
    {
        var originales = (resumenOriginal["tributos"] as JArray)?.OfType<JObject>()
            .Where(x => !string.IsNullOrWhiteSpace(Texto(x, "codigo")))
            .ToDictionary(x => Texto(x, "codigo")!, StringComparer.Ordinal) ?? new Dictionary<string, JObject>(StringComparer.Ordinal);
        var resultado = new JArray();
        foreach (var codigo in codigos.OrderBy(x => x, StringComparer.Ordinal))
        {
            originales.TryGetValue(codigo, out var original);
            if (original is null && codigo != "C3")
                throw new InvalidOperationException($"No se puede calcular el tributo {codigo} porque no aparece valorizado en el resumen original.");
            resultado.Add(new JObject
            {
                ["codigo"] = codigo,
                ["descripcion"] = Texto(original ?? new JObject(), "descripcion") ?? (codigo == "C3" ? "Impuesto al Valor Agregado" : "Impuesto"),
                ["valor"] = codigo == "C3" ? 0m : Redondear2((original is null ? 0m : Numero(original, "valor")) * proporcion)
            });
        }
        return resultado.Count == 0 ? JValue.CreateNull() : resultado;
    }

    private static void ValidarSolicitud(DteRetornoRequest request)
    {
        if (request.OperacionId == Guid.Empty) throw new InvalidOperationException("Falta el identificador de la operación de retorno. Abra un nuevo formulario.");
        if (request.FacturaOriginalId <= 0) throw new InvalidOperationException("Seleccione un documento original válido.");
        if (request.TipoRetorno is not ("1" or "2" or "3" or "4")) throw new InvalidOperationException("El tipo de retorno debe estar entre 1 y 4.");
        if (string.IsNullOrWhiteSpace(request.Motivo) || request.Motivo.Trim().Length > 1000) throw new InvalidOperationException("El motivo es obligatorio y admite hasta 1000 caracteres.");
        if (request.Lineas is null || request.Lineas.Count == 0) throw new InvalidOperationException("Seleccione al menos una línea para retornar.");
        if (request.Lineas.Count > 2000) throw new InvalidOperationException("El ERET admite como máximo 2000 líneas.");
        if (request.Lineas.GroupBy(x => x.NumeroItemOriginal).Any(x => x.Count() > 1)) throw new InvalidOperationException("No se puede repetir una línea original.");
        if (request.Lineas.Any(x => x.NumeroItemOriginal <= 0 || x.Cantidad <= 0m || x.PrecioUnitario is <= 0m))
            throw new InvalidOperationException("Los ítems, cantidades y precios deben ser mayores que cero.");
        if (request.Lineas.Any(x => Escala(x.Cantidad) > 8 || x.PrecioUnitario.HasValue && Escala(x.PrecioUnitario.Value) > 8))
            throw new InvalidOperationException("Cantidades y precios admiten hasta ocho decimales.");
        if (request.Lineas.Any(x => x.Cantidad >= 100000000000m || x.PrecioUnitario >= 100000000000m))
            throw new InvalidOperationException("Una cantidad o precio excede el máximo fiscal permitido.");
    }

    private static JObject CrearEmisor(JObject original, SRAUMOAR.Entidades.FacturacionElectronica.ConfiguracionFacturacionElectronica config)
    {
        var nit = NormalizarDocumento(config.NitEmisor ?? Texto(original, "nit"));
        if (nit.Length is < 9 or > 14) throw new InvalidOperationException("El NIT del emisor no cumple la longitud requerida para ERET.");
        ValidarCodigoMh(config.CodigoEstablecimientoMh, "código de establecimiento MH");
        ValidarCodigoMh(config.CodigoPuntoVentaMh, "código de punto de venta MH");
        ValidarCodigoContribuyente(config.CodigoEstablecimiento, 10, "código de establecimiento");
        ValidarCodigoContribuyente(config.CodigoPuntoVenta, 15, "código de punto de venta");
        return new JObject
        {
            ["nit"] = nit, ["nombre"] = config.NombreEmisor ?? Texto(original, "nombre"),
            ["codEstableMH"] = config.CodigoEstablecimientoMh, ["codEstable"] = Nulo(config.CodigoEstablecimiento),
            ["codPuntoVentaMH"] = config.CodigoPuntoVentaMh, ["codPuntoVenta"] = Nulo(config.CodigoPuntoVenta),
            ["recintoFiscal"] = original["recintoFiscal"]?.DeepClone() ?? JValue.CreateNull(),
            ["tipoRegimen"] = original["tipoRegimen"]?.DeepClone() ?? JValue.CreateNull(),
            ["regimen"] = original["regimen"]?.DeepClone() ?? JValue.CreateNull(),
            ["tipoItemExpor"] = original["tipoItemExpor"]?.DeepClone() ?? JValue.CreateNull()
        };
    }

    private static JObject CrearDocumento(JObject receptor)
    {
        var numero = Texto(receptor, "numDocumento") ?? Texto(receptor, "nit") ?? Texto(receptor, "dui");
        var tipo = Texto(receptor, "tipoDocumento");
        if (string.IsNullOrWhiteSpace(tipo) && !string.IsNullOrWhiteSpace(numero))
            tipo = NormalizarDocumento(numero).Length == 14 ? "36" : NormalizarDocumento(numero).Length == 9 ? "13" : "37";
        return new JObject { ["tipoDocumento"] = tipo, ["numDocumento"] = numero, ["nombre"] = Texto(receptor, "nombre"),
            ["codPais"] = Nulo(Texto(receptor, "codPais")), ["nombrePais"] = Nulo(Texto(receptor, "nombrePais")),
            ["telefono"] = Nulo(Texto(receptor, "telefono")), ["correo"] = Nulo(Texto(receptor, "correo")) };
    }

    private static void ValidarCodigoMh(string? codigo, string nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Trim().Length != 4)
            throw new InvalidOperationException($"El {nombre} debe contener exactamente cuatro caracteres.");
    }

    private static void ValidarCodigoContribuyente(string? codigo, int maximo, string nombre)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && codigo.Trim().Length > maximo)
            throw new InvalidOperationException($"El {nombre} admite como máximo {maximo} caracteres.");
    }

    private static decimal BaseOperacionOriginal(JObject root, JObject resumen)
    {
        var baseResumen = Numero(resumen, "subTotalVentas");
        if (baseResumen <= 0m) baseResumen = Numero(resumen, "totalCompra");
        if (baseResumen <= 0m) baseResumen = Numero(resumen, "subTotal");
        if (baseResumen > 0m) return baseResumen;

        return Redondear2(Cuerpo(root).Sum(item =>
            Numero(item, "ventaNoSuj") + Numero(item, "ventaExenta") +
            Numero(item, "ventaGravada") + Numero(item, "compra")));
    }

    private static string NormalizarDocumento(string? valor) => (valor ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim();

    private static JObject Identificacion(JObject root) => (JObject?)root["identificacion"] ?? throw new InvalidOperationException("Falta identificación.");
    private static IEnumerable<JObject> Cuerpo(JObject root) => ((JArray?)root["cuerpoDocumento"] ?? throw new InvalidOperationException("Falta cuerpoDocumento.")).OfType<JObject>();
    private static string Clasificacion(JObject x) => Numero(x, "compra") > 0 ? "compra" : Numero(x, "ventaExenta") > 0 ? "ventaExenta" : Numero(x, "ventaNoSuj") > 0 ? "ventaNoSuj" : "ventaGravada";
    private static string? Texto(JObject x, string nombre) => x.Value<string>(nombre);
    private static int Entero(JObject x, string nombre) => x.Value<int?>(nombre) ?? throw new InvalidOperationException($"Falta {nombre}.");
    private static decimal Numero(JObject x, string nombre) => x[nombre]?.Type is JTokenType.Integer or JTokenType.Float
        ? x[nombre]!.Value<decimal>() : decimal.TryParse(x[nombre]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0m;
    private static decimal Redondear2(decimal n) => Math.Round(n, 2, MidpointRounding.AwayFromZero);
    private static int Escala(decimal n) => (decimal.GetBits(n)[3] >> 16) & 0x7F;
    private static JToken Nulo(string? s) => string.IsNullOrWhiteSpace(s) ? JValue.CreateNull() : new JValue(s);
    private static DateTime FechaElSalvador()
    {
        try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central America Standard Time"); }
        catch (TimeZoneNotFoundException) { return DateTime.UtcNow.AddHours(-6); }
    }
    private static decimal CargoRetornado(JObject resumen, string tipoOriginal, string campoExportacion,
        string campoTotal, decimal sumaItems, decimal proporcion)
    {
        if (sumaItems != 0m) return Redondear2(sumaItems);
        var campo = tipoOriginal == "11" && resumen[campoExportacion] is { Type: not JTokenType.Null }
            ? campoExportacion : campoTotal;
        return Redondear2(Numero(resumen, campo) * proporcion);
    }

    private static string PrefijoOperacion(Guid operacionId) => $"18:{operacionId:N}:";

    private static string ClaveIdempotencia(DteRetornoRequest request) =>
        PrefijoOperacion(request.OperacionId) + HashContenido(request);

    private static string HashContenido(DteRetornoRequest request)
    {
        // JSON canónico evita ambigüedad por separadores y por cultura (coma/punto).
        var material = new JObject
        {
            ["original"] = request.FacturaOriginalId, ["tipo"] = request.TipoRetorno, ["motivo"] = request.Motivo.Trim(),
            ["lineas"] = new JArray(request.Lineas.OrderBy(x => x.NumeroItemOriginal).Select(x => new JObject
            {
                ["item"] = x.NumeroItemOriginal,
                ["cantidad"] = x.Cantidad.ToString("0.########", CultureInfo.InvariantCulture),
                ["precio"] = x.PrecioUnitario?.ToString("0.########", CultureInfo.InvariantCulture)
            }))
        }.ToString(Formatting.None);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static void ValidarContenidoOperacion(string persistida, string solicitada)
    {
        if (!string.Equals(persistida, solicitada, StringComparison.Ordinal))
            throw new InvalidOperationException("El identificador de operación ya fue utilizado con un contenido distinto. Para otro retorno abra un nuevo formulario.");
    }

    private static DteRetornoResultado ResultadoExistente(Factura x) => new(x.FacturaId, x.CodigoGeneracion, x.NumeroControl,
        DteReservaReglas.EsOriginalAceptadoVigente(x), DteReservaReglas.EsOriginalAceptadoVigente(x) ? DteEstados.Aceptado : x.EstadoTransmision,
        x.AmbienteFiscal ?? string.Empty, x.SelloRecepcion,
        DteReservaReglas.EsOriginalAceptadoVigente(x) ? null : x.RespuestaHacienda);
}
