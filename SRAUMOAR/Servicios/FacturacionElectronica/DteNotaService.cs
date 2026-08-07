using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

        if (original.Anulada)
            throw new InvalidOperationException("No se puede generar una nota sobre una factura anulada.");
        if (string.IsNullOrWhiteSpace(original.SelloRecepcion))
            throw new InvalidOperationException("La factura original debe estar aceptada por Hacienda.");
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

        var monto = request.Monto.GetValueOrDefault(totalOriginal);
        if (monto <= 0m)
            throw new InvalidOperationException("El monto de la nota debe ser mayor que cero.");
        if (tipoNota == DteTipos.NotaCredito)
        {
            var montoAcreditado = await _contexto.Facturas.AsNoTracking()
                .Where(x => x.FacturaOriginalId == original.FacturaId
                    && x.TipoDteCodigo == DteTipos.NotaCredito
                    && x.EstadoTransmision == DteEstados.Aceptado)
                .SumAsync(x => x.MontoNota ?? 0m, cancellationToken);

            if (montoAcreditado + monto > totalOriginal + 0.01m)
                throw new InvalidOperationException("La suma de las notas de crédito no puede superar el total original.");
        }

        var codigo = Guid.NewGuid().ToString().ToUpperInvariant();
        var correlativo = await _correlativos.ObtenerSiguienteCorrelativo(
            tipoNota,
            ambiente,
            configuracion.Entidad.CodigoEstablecimiento,
            configuracion.Entidad.CodigoPuntoVenta);
        var numeroControl = $"DTE-{tipoNota}-{configuracion.Entidad.CodigoEstablecimiento}{configuracion.Entidad.CodigoPuntoVenta}-{correlativo:D15}";
        var json = _jsonFactory.CrearNota(original.JsonDte, tipoNota, numeroControl, codigo, monto, request.Motivo, configuracion.Entidad);
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
            Numero(resumen, "totalGravada") is decimal gravada ? Math.Round(gravada * monto / totalOriginal, 2) : 0m,
            Numero(resumen, "totalExenta") is decimal exenta ? Math.Round(exenta * monto / totalOriginal, 2) : 0m,
            Numero(resumen, "totalIva") is decimal iva ? Math.Round(iva * monto / totalOriginal, 2) : 0m);
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
            return ConvertirResultado(existente, preparada);

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
            TotalPagar = preparada.Monto,
            TotalIva = preparada.TotalIva
        };

        _contexto.Facturas.Add(nota);
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _contexto.Entry(nota).State = EntityState.Detached;
            existente = await _contexto.Facturas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ClaveIdempotencia == claveIdempotencia, cancellationToken);
            if (existente is not null)
                return ConvertirResultado(existente, preparada);
            throw;
        }

        DteTransmisionResult resultado;
        try
        {
            resultado = await _transmision.TransmitirAsync(new DteTransmisionRequest(
                preparada.Json,
                preparada.TipoNota,
                preparada.CodigoGeneracion,
                preparada.NumeroControl,
                preparada.Version,
                preparada.CorreoReceptor,
                nota.FacturaId), cancellationToken);
        }
        catch (Exception ex)
        {
            resultado = new DteTransmisionResult(false, DteEstados.Incierto, null, string.Empty, 1, preparada.AmbienteFiscal, ex.Message);
            _logger.LogError(ex, "Error preparando la transmisión de nota {CodigoGeneracion}", preparada.CodigoGeneracion);
        }

        nota.SelloRecepcion = resultado.SelloRecepcion;
        nota.EstadoTransmision = resultado.Estado;
        nota.IntentosTransmision = resultado.Intentos;
        nota.RespuestaHacienda = resultado.Respuesta;
        nota.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
        await _contexto.SaveChangesAsync(cancellationToken);

        if (resultado.Aceptado && !string.IsNullOrWhiteSpace(preparada.CorreoReceptor))
        {
            try
            {
                await _emailQueue.EncolarDteAsync(nota, preparada.TipoNota, preparada.Json, resultado.SelloRecepcion, preparada.CorreoReceptor, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "La nota {FacturaId} fue aceptada, pero no se pudo encolar su correo.", nota.FacturaId);
            }
        }

        return new DteNotaResultado(
            nota.FacturaId,
            preparada.TipoNota,
            preparada.CodigoGeneracion,
            preparada.NumeroControl,
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

    private static decimal? Numero(JObject objeto, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            var token = objeto[nombre];
            if (token is null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
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
            nota.EstadoTransmision == DteEstados.Aceptado,
            nota.EstadoTransmision,
            nota.AmbienteFiscal ?? preparada.AmbienteFiscal,
            nota.SelloRecepcion,
            nota.EstadoTransmision == DteEstados.Aceptado ? null : nota.RespuestaHacienda);
}
