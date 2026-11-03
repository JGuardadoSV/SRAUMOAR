using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record DteEmisionRecuperacionResultado(int FacturaId, bool Aceptado, string Estado, string Mensaje);

public interface IDteEmisionRecuperacionService
{
    Task<DteEmisionRecuperacionResultado?> ReanudarAsync(string claveOperacion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reutiliza siempre la factura, identidad y JSON ya reservados. El servicio de
/// transmisión no vuelve a enviar estados Enviado/Incierto/Aceptado: solo reconcilia.
/// </summary>
public sealed class DteEmisionRecuperacionService : IDteEmisionRecuperacionService
{
    private readonly Contexto _contexto;
    private readonly IDteTransmisionService _transmision;
    private readonly IDteEmailQueueService _correo;
    private readonly ILogger<DteEmisionRecuperacionService> _logger;
    private readonly IDteConciliacionService _conciliacion;

    public DteEmisionRecuperacionService(Contexto contexto, IDteTransmisionService transmision,
        IDteEmailQueueService correo, ILogger<DteEmisionRecuperacionService> logger, IDteConciliacionService conciliacion)
    {
        _contexto = contexto;
        _transmision = transmision;
        _correo = correo;
        _logger = logger;
        _conciliacion = conciliacion;
    }

    public async Task<DteEmisionRecuperacionResultado?> ReanudarAsync(string claveOperacion,
        CancellationToken cancellationToken = default)
    {
        var factura = await _contexto.Facturas.SingleOrDefaultAsync(
            x => x.ClaveIdempotencia == claveOperacion, cancellationToken);
        if (factura is null) return null;
        if (factura.Anulada)
            return Resultado(factura, "La operación ya existe y está anulada; no se generó otro DTE.");
        if (factura.EstadoTransmision == DteEstados.Anulando)
            return Resultado(factura, "La operación tiene una invalidación en curso. Use Conciliar; no se reabrió la emisión.");
        if (string.IsNullOrWhiteSpace(factura.JsonDte) || string.IsNullOrWhiteSpace(factura.TipoDteCodigo))
            return Resultado(factura, "La operación ya existe, pero requiere revisión porque no conserva un JSON fiscal completo.");
        if (DteEstadoFiscal.EsAceptado(factura))
        {
            await EncolarCorreoSeguroAsync(factura, cancellationToken);
            return Resultado(factura, "La operación ya estaba aceptada; no se generó otro DTE.");
        }

        var transmisionExistente = await _contexto.DteTransmisiones.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CodigoGeneracion == factura.CodigoGeneracion && x.TipoOperacion == "EMISION"
            && x.AmbienteFiscal == factura.AmbienteFiscal, cancellationToken);
        if (factura.EstadoTransmision != DteEstados.Preparado
            && (transmisionExistente is null || transmisionExistente.Estado == DteEstados.Preparado))
            return Resultado(factura,
                "La operación local ya salió del estado preparado y no se retransmitió. La discrepancia requiere revisión.");
        if (transmisionExistente is not null && transmisionExistente.Estado != DteEstados.Preparado)
        {
            if (transmisionExistente.Estado == DteEstados.Aceptado)
            {
                var mensaje = await _conciliacion.ConciliarAsync(factura.FacturaId, cancellationToken);
                await _contexto.Entry(factura).ReloadAsync(cancellationToken);
                return Resultado(factura, mensaje);
            }
            if (!JToken.DeepEquals(JObject.Parse(factura.JsonDte), JObject.Parse(transmisionExistente.JsonEnviado)))
                return Resultado(factura, "El historial difiere del JSON original y requiere revisión.");
            await _contexto.Facturas.Where(x => x.FacturaId == factura.FacturaId && !x.Anulada
                && x.EstadoTransmision == factura.EstadoTransmision
                && (x.SelloRecepcion == null || x.SelloRecepcion == ""))
                .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, transmisionExistente.Estado)
                    .SetProperty(f => f.IntentosTransmision, transmisionExistente.Intentos)
                    .SetProperty(f => f.RespuestaHacienda, transmisionExistente.Respuesta), cancellationToken);
            await _contexto.Entry(factura).ReloadAsync(cancellationToken);
            return Resultado(factura, $"La operación existente está en estado {factura.EstadoTransmision}. Use Conciliar para verificar resultados inciertos; no se retransmitió ni se generó otro DTE.");
        }
        var identificacion = JObject.Parse(factura.JsonDte)["identificacion"] as JObject
            ?? throw new InvalidOperationException("El documento reservado no contiene identificación fiscal.");
        var version = identificacion.Value<int?>("version")
            ?? throw new InvalidOperationException("El documento reservado no contiene versión fiscal.");
        var resultado = await _transmision.TransmitirAsync(new DteTransmisionRequest(
            factura.JsonDte, factura.TipoDteCodigo, factura.CodigoGeneracion, factura.NumeroControl,
            version, factura.CorreoReceptor, factura.FacturaId), cancellationToken);

        factura.SelloRecepcion = resultado.SelloRecepcion ?? string.Empty;
        factura.EstadoTransmision = resultado.Estado;
        factura.IntentosTransmision = resultado.Intentos;
        factura.RespuestaHacienda = resultado.Respuesta;
        factura.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
        await _contexto.SaveChangesAsync(cancellationToken);
        if (resultado.Aceptado && !string.IsNullOrWhiteSpace(factura.CorreoReceptor))
            await EncolarCorreoSeguroAsync(factura, cancellationToken);

        return Resultado(factura, resultado.Aceptado
            ? "La operación reservada fue reconciliada como aceptada; no se generó otro DTE."
            : $"La operación reservada continúa en estado {resultado.Estado}; no se generó otro DTE.");
    }

    private static DteEmisionRecuperacionResultado Resultado(
        SRAUMOAR.Entidades.Colecturia.Factura factura, string mensaje)
        => new(factura.FacturaId, DteEstadoFiscal.EsAceptado(factura), factura.EstadoTransmision, mensaje);

    private async Task EncolarCorreoSeguroAsync(SRAUMOAR.Entidades.Colecturia.Factura factura, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(factura.CorreoReceptor) || string.IsNullOrWhiteSpace(factura.JsonDte)
            || string.IsNullOrWhiteSpace(factura.TipoDteCodigo) || string.IsNullOrWhiteSpace(factura.SelloRecepcion)) return;
        try
        {
            await _correo.EncolarDteAsync(factura, factura.TipoDteCodigo, factura.JsonDte,
                factura.SelloRecepcion, factura.CorreoReceptor, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "El DTE {FacturaId} está aceptado, pero no se pudo reconciliar su correo", factura.FacturaId);
        }
    }
}
