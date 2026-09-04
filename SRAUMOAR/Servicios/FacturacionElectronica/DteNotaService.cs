using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Linq.Expressions;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteNotaService
{
    Task<DteNotaPreparada> PrepararAsync(DteNotaRequest request, CancellationToken cancellationToken = default);
    Task<DteNotaResultado> EmitirAsync(DteNotaRequest request, CancellationToken cancellationToken = default);
}

public sealed class DteNotaService : IDteNotaService
{
    private readonly Contexto _contexto;
    private readonly IConfiguracionFeService _configuracion;
    private readonly IDteJsonFactory _jsonFactory;
    private readonly ICorrelativoService _correlativos;
    private readonly IDteTransmisionService _transmision;
    private readonly IDteEmailQueueService _emailQueue;
    private readonly ILogger<DteNotaService> _logger;

    public DteNotaService(
        Contexto contexto,
        IConfiguracionFeService configuracion,
        IDteJsonFactory jsonFactory,
        ICorrelativoService correlativos,
        IDteTransmisionService transmision,
        IDteEmailQueueService emailQueue,
        ILogger<DteNotaService> logger)
    {
        _contexto = contexto;
        _configuracion = configuracion;
        _jsonFactory = jsonFactory;
        _correlativos = correlativos;
        _transmision = transmision;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<DteNotaPreparada> PrepararAsync(DteNotaRequest request, CancellationToken cancellationToken = default)
    {
        var tipoNota = NormalizarTipoNota(request.TipoNota);
        var original = await _contexto.Facturas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacturaId == request.FacturaOriginalId, cancellationToken)
            ?? throw new InvalidOperationException("No se encontró la factura original.");

        if (!DteReservaReglas.EsOriginalAceptadoVigente(original))
            throw new InvalidOperationException("La factura original debe estar aceptada y vigente.");
        if (string.IsNullOrWhiteSpace(original.JsonDte))
            throw new InvalidOperationException("La factura original no tiene JSON DTE.");

        var root = JObject.Parse(original.JsonDte);
        var identificacion = root["identificacion"] as JObject
            ?? throw new InvalidOperationException("La factura original no contiene identificación.");
        var ambienteJson = ConfiguracionFeService.NormalizarAmbiente(
            identificacion.Value<string>("ambiente") ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(original.AmbienteFiscal))
        {
            var ambientePersistido = ConfiguracionFeService.NormalizarAmbiente(original.AmbienteFiscal);
            if (!string.Equals(ambientePersistido, ambienteJson, StringComparison.Ordinal))
                throw new InvalidOperationException("El ambiente fiscal persistido no coincide con el ambiente del JSON original.");
        }
        var ambiente = ambienteJson;
        var configuracion = await _configuracion.ObtenerAsync(ambiente, cancellationToken);
        if (!configuracion.Entidad.Habilitado)
            throw new InvalidOperationException($"El ambiente fiscal {ambiente} está deshabilitado.");

        var resumen = root["resumen"] as JObject
            ?? throw new InvalidOperationException("La factura original no contiene resumen.");
        var totalOriginal = Numero(resumen, "montoTotalOperacion", "totalPagar") ?? original.TotalPagar;
        if (totalOriginal <= 0m)
            throw new InvalidOperationException("La factura original no tiene un total válido.");

        var monto = Math.Round(request.Monto.GetValueOrDefault(totalOriginal), 2, MidpointRounding.AwayFromZero);
        if (monto <= 0m)
            throw new InvalidOperationException("El monto de la nota debe ser mayor que cero.");
        if (tipoNota == DteTipos.NotaCredito)
            DteReservaReglas.ValidarMontoCredito(totalOriginal, 0m, monto);

        var originalParaNota = await CompletarReceptorHistoricoAsync(root, request, cancellationToken);
        var codigo = Guid.NewGuid().ToString().ToUpperInvariant();
        var correlativo = await _correlativos.ObtenerSiguienteCorrelativo(
            tipoNota,
            ambiente,
            configuracion.Entidad.CodigoEstablecimiento,
            configuracion.Entidad.CodigoPuntoVenta);
        var numeroControl = $"DTE-{tipoNota}-{configuracion.Entidad.CodigoEstablecimiento}{configuracion.Entidad.CodigoPuntoVenta}-{correlativo:D15}";
        var json = _jsonFactory.CrearNota(originalParaNota.ToString(Newtonsoft.Json.Formatting.None), tipoNota, numeroControl, codigo, monto, request.Motivo, configuracion.Entidad);
        var totalesNota = LeerTotalesNota(json);
        var receptorCorreo = (root["receptor"] as JObject)?.Value<string>("correo") ?? original.CorreoReceptor;

        return new DteNotaPreparada(
            original.FacturaId,
            tipoNota,
            ambiente,
            json,
            codigo,
            numeroControl,
            4,
            Math.Round(monto, 2, MidpointRounding.AwayFromZero),
            receptorCorreo,
            string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            totalesNota.TotalGravado,
            totalesNota.TotalExento,
            totalesNota.TotalIva);
    }

    public async Task<DteNotaResultado> EmitirAsync(DteNotaRequest request, CancellationToken cancellationToken = default)
    {
        var preparada = await PrepararAsync(request, cancellationToken);
        var claveIdempotencia = CrearClaveIdempotencia(
            preparada.FacturaOriginalId,
            preparada.TipoNota,
            preparada.Monto,
            preparada.Motivo);
        var existente = await _contexto.Facturas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClaveIdempotencia == claveIdempotencia, cancellationToken);
        if (existente is not null)
            return await TransmitirNotaPersistidaAsync(existente.FacturaId, preparada, cancellationToken);

        var nota = new Factura
        {
            Fecha = DateTime.UtcNow,
            CodigoGeneracion = preparada.CodigoGeneracion,
            NumeroControl = preparada.NumeroControl,
            TipoDTE = int.Parse(preparada.TipoNota),
            TipoDteCodigo = preparada.TipoNota,
            AmbienteFiscal = preparada.AmbienteFiscal,
            EstadoTransmision = DteEstados.Preparado,
            JsonDte = preparada.Json,
            FacturaOriginalId = preparada.FacturaOriginalId,
            MotivoNota = preparada.Motivo,
            MontoNota = preparada.Monto,
            ClaveIdempotencia = claveIdempotencia,
            CorreoReceptor = preparada.CorreoReceptor,
            TotalGravado = preparada.TotalGravado,
            TotalExento = preparada.TotalExento,
            TotalPagar = LeerTotalesNota(preparada.Json).TotalPagar,
            TotalIva = preparada.TotalIva
        };

        try
        {
            await using var tx = await _contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var original = await DteReservaReglas.BloquearOriginalAsync(_contexto, preparada.FacturaOriginalId, cancellationToken);
            if (!DteReservaReglas.EsOriginalAceptadoVigente(original))
                throw new InvalidOperationException("La factura original dejó de estar aceptada y vigente antes de reservar la nota.");

            // Releer después del lock: un doble POST no consume saldo dos veces.
            existente = await _contexto.Facturas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ClaveIdempotencia == claveIdempotencia, cancellationToken);
            if (existente is not null)
            {
                nota = existente;
            }
            else
            {
                if (preparada.TipoNota == DteTipos.NotaCredito)
                {
                    var resumen = JObject.Parse(original.JsonDte ?? throw new InvalidOperationException("Falta el JSON original."))["resumen"] as JObject
                        ?? throw new InvalidOperationException("Falta el resumen original.");
                    var total = Numero(resumen, "montoTotalOperacion", "totalPagar") ?? original.TotalPagar;
                    var reservado = await _contexto.Facturas.AsNoTracking()
                        .Where(x => x.FacturaOriginalId == original.FacturaId)
                        .Where(DteReservaReglas.ReservaCredito)
                        .SumAsync(x => x.MontoNota ?? x.TotalPagar, cancellationToken);
                    DteReservaReglas.ValidarMontoCredito(total, reservado, preparada.Monto);
                }

                _contexto.Facturas.Add(nota);
                await _contexto.SaveChangesAsync(cancellationToken);
            }
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _contexto.Entry(nota).State = EntityState.Detached;
            existente = await _contexto.Facturas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ClaveIdempotencia == claveIdempotencia, cancellationToken);
            if (existente is not null)
                return await TransmitirNotaPersistidaAsync(existente.FacturaId, preparada, cancellationToken);
            throw;
        }

        return await TransmitirNotaPersistidaAsync(nota.FacturaId, preparada, cancellationToken);
    }

    private async Task<DteNotaResultado> TransmitirNotaPersistidaAsync(int facturaId, DteNotaPreparada preparada, CancellationToken ct)
    {
        // Una falla PRE deja Preparado y conserva JSON, código y reserva para el retry.
        var reclamada = await _contexto.Facturas.Where(x => x.FacturaId == facturaId && !x.Anulada
                && (x.SelloRecepcion == null || x.SelloRecepcion == "") && x.EstadoTransmision == DteEstados.Preparado
                && (x.TipoDteCodigo == DteTipos.NotaCredito || x.TipoDteCodigo == DteTipos.NotaDebito))
            .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, DteEstados.Enviado), ct);
        foreach (var entry in _contexto.ChangeTracker.Entries<Factura>().Where(x => x.Entity.FacturaId == facturaId).ToArray())
            entry.State = EntityState.Detached;
        var nota = await _contexto.Facturas.SingleAsync(x => x.FacturaId == facturaId, ct);
        if (!DteReservaReglas.PuedeTransmitirReclamada(reclamada, nota)) return ConvertirResultado(nota, preparada);
        var json = nota.JsonDte ?? throw new InvalidOperationException("La nota preparada no contiene JSON.");
        var tipoNota = nota.TipoDteCodigo ?? preparada.TipoNota;

        DteTransmisionResult resultado;
        try
        {
            resultado = await _transmision.TransmitirAsync(new DteTransmisionRequest(
                json,
                tipoNota,
                nota.CodigoGeneracion,
                nota.NumeroControl,
                4,
                nota.CorreoReceptor,
                nota.FacturaId), ct);
        }
        catch (Exception ex)
        {
            resultado = new DteTransmisionResult(false, DteEstados.Incierto, null, string.Empty, 1, preparada.AmbienteFiscal, ex.Message);
            _logger.LogError(ex, "Error preparando la transmisión de nota {CodigoGeneracion}", nota.CodigoGeneracion);
        }

        nota.SelloRecepcion = resultado.SelloRecepcion;
        nota.EstadoTransmision = resultado.Estado;
        nota.IntentosTransmision = resultado.Intentos;
        nota.RespuestaHacienda = resultado.Respuesta;
        nota.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _contexto.SaveChangesAsync(cierre.Token);

        if (resultado.Aceptado && !string.IsNullOrWhiteSpace(nota.CorreoReceptor))
        {
            try
            {
                await _emailQueue.EncolarDteAsync(nota, tipoNota, json, resultado.SelloRecepcion, nota.CorreoReceptor, cierre.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "La nota {FacturaId} fue aceptada, pero no se pudo encolar su correo.", nota.FacturaId);
            }
        }

        return new DteNotaResultado(
            nota.FacturaId,
            tipoNota,
            nota.CodigoGeneracion,
            nota.NumeroControl,
            resultado.Aceptado,
            resultado.Estado,
            resultado.AmbienteFiscal,
            resultado.SelloRecepcion,
            resultado.Error);
    }

    private static string NormalizarTipoNota(string tipoNota)
        => tipoNota?.Trim() switch
        {
            DteTipos.NotaCredito => DteTipos.NotaCredito,
            DteTipos.NotaDebito => DteTipos.NotaDebito,
            _ => throw new ArgumentException("La nota debe ser tipo 05 o 06.", nameof(tipoNota))
        };

    private async Task<JObject> CompletarReceptorHistoricoAsync(JObject original, DteNotaRequest request, CancellationToken ct)
    {
        var seleccion = SeleccionReceptorHistorico(original, request.DepartamentoReceptor, request.MunicipioReceptor, request.DistritoReceptor);
        if (seleccion is null) return (JObject)original.DeepClone();
        var geografia = await _contexto.CatalogosFe.AsNoTracking()
            .Where(x => x.Activo && x.Version == "1.1"
                && (x.CodigoCatalogo == "CAT-012" || x.CodigoCatalogo == "CAT-013" || x.CodigoCatalogo == "CAT-008"))
            .ToListAsync(ct);
        if (!TerritorioFiscalService.EsDireccionValida(seleccion[0].Codigo, seleccion[1].Codigo, seleccion[2].Codigo,
                geografia, geografia, geografia))
            throw new InvalidOperationException("El departamento, municipio y distrito seleccionados no forman una ubicación fiscal válida.");

        // CrearNota aplica el esquema completo, incluida pertenencia geográfica. Nunca
        // se modifica JsonDte de la factura histórica ni la dirección de su emisor.
        return ClonarConDireccionReceptor(original, seleccion[0].Codigo, seleccion[1].Codigo, seleccion[2].Codigo);
    }

    private static (string Catalogo, string Codigo)[]? SeleccionReceptorHistorico(
        JObject original, string? departamento, string? municipio, string? distrito)
    {
        if (!string.IsNullOrWhiteSpace(original["receptor"]?["direccion"]?.Value<string>("distrito")))
            return null;
        if (string.IsNullOrWhiteSpace(departamento) || string.IsNullOrWhiteSpace(municipio) || string.IsNullOrWhiteSpace(distrito))
            throw new InvalidOperationException("El receptor histórico no tiene distrito. Seleccione departamento, municipio y distrito de los catálogos importados.");
        return [("CAT-012", TerritorioFiscalService.Normalizar(departamento)),
            ("CAT-013", TerritorioFiscalService.Normalizar(municipio)),
            ("CAT-008", TerritorioFiscalService.Normalizar(distrito))];
    }

    private static JObject ClonarConDireccionReceptor(JObject original, string departamento, string municipio, string distrito)
    {
        var copia = (JObject)original.DeepClone();
        var receptor = copia["receptor"] as JObject ?? throw new InvalidOperationException("Falta el receptor original.");
        var direccion = receptor["direccion"] as JObject;
        if (direccion is null)
        {
            direccion = new JObject();
            receptor["direccion"] = direccion;
        }
        // Una dirección existente ya pertenece a la copia. Reasignarla puede clonar
        // el token y dejar esta referencia desconectada de la dirección devuelta.
        direccion["departamento"] = departamento;
        direccion["municipio"] = municipio;
        direccion["distrito"] = distrito;
        return copia;
    }

    private static (decimal TotalGravado, decimal TotalExento, decimal TotalIva, decimal TotalPagar) LeerTotalesNota(string json)
    {
        var resumen = JObject.Parse(json)["resumen"] as JObject
            ?? throw new InvalidOperationException("La nota generada no contiene resumen.");
        decimal Requerido(string campo) => Numero(resumen, campo)
            ?? throw new InvalidOperationException($"La nota generada no contiene {campo} válido.");
        // No recalcular desde el CCF legacy: el JSON generado ya contiene IVA y neto.
        // MontoNota sigue siendo el monto de operación usado para reservar el crédito.
        return (Requerido("totalGravada"), Requerido("totalExenta"), Requerido("totalIva"), Requerido("totalPagar"));
    }

    private static decimal? Numero(JObject objeto, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            var token = objeto[nombre];
            if (token is null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
            if (token.Type is Newtonsoft.Json.Linq.JTokenType.Integer or Newtonsoft.Json.Linq.JTokenType.Float) return token.Value<decimal>();
            if (decimal.TryParse(token.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                return valor;
        }
        return null;
    }

    private static string CrearClaveIdempotencia(int facturaOriginalId, string tipoNota, decimal monto, string? motivo)
    {
        var material = string.Join("|", facturaOriginalId, tipoNota,
            monto.ToString("0.00", CultureInfo.InvariantCulture), motivo?.Trim() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static DteNotaResultado ConvertirResultado(Factura nota, DteNotaPreparada preparada)
        => new(
            nota.FacturaId,
            preparada.TipoNota,
            nota.CodigoGeneracion,
            nota.NumeroControl,
            DteReservaReglas.EsOriginalAceptadoVigente(nota),
            DteReservaReglas.EsOriginalAceptadoVigente(nota) ? DteEstados.Aceptado : nota.EstadoTransmision,
            nota.AmbienteFiscal ?? preparada.AmbienteFiscal,
            nota.SelloRecepcion,
            DteReservaReglas.EsOriginalAceptadoVigente(nota) ? null : nota.RespuestaHacienda);
}

// Reglas compartidas por NC y ERET, evaluables sin contexto ni proveedor de BD.
internal static class DteReservaReglas
{
    internal static readonly Expression<Func<Factura, bool>> ReservaCredito = x =>
        !x.Anulada && (x.TipoDteCodigo == DteTipos.NotaCredito || x.TipoDteCodigo == null && x.TipoDTE == 5)
        && (x.EstadoTransmision == DteEstados.Preparado || x.EstadoTransmision == DteEstados.Enviado
            || x.EstadoTransmision == DteEstados.Incierto || x.EstadoTransmision == DteEstados.Aceptado
            || x.EstadoTransmision == DteEstados.Anulando);

    internal static bool EsOriginalAceptadoVigente(Factura factura) => DteEstadoFiscal.EsAceptado(factura);

    internal static bool PuedeTransmitirReclamada(int reclamada, Factura factura) =>
        reclamada == 1 && !factura.Anulada && !EsOriginalAceptadoVigente(factura)
        && factura.EstadoTransmision == DteEstados.Enviado;

    internal static void ValidarMontoCredito(decimal totalOriginal, decimal reservado, decimal monto)
    {
        if (totalOriginal <= 0m || monto <= 0m || reservado + monto > totalOriginal)
            throw new InvalidOperationException("La suma de las notas de crédito reservadas no puede superar el total original.");
    }

    internal static Task<Factura> BloquearOriginalAsync(Contexto contexto, int id, CancellationToken ct) =>
        contexto.Facturas.FromSqlInterpolated($"SELECT * FROM [Factura] WITH (UPDLOCK, HOLDLOCK) WHERE [FacturaId] = {id}")
            .AsNoTracking().SingleAsync(ct);
}
