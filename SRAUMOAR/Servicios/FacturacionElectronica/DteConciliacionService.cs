using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteConciliacionService
{
    Task<string> ConciliarAsync(int facturaId, CancellationToken cancellationToken = default);
}

public sealed class DteConciliacionService(Contexto contexto, IConfiguracionFeService configuraciones,
    IDteConsultaClient consultas, IDteEmailQueueService correo, ILogger<DteConciliacionService> logger) : IDteConciliacionService
{
    public async Task<string> ConciliarAsync(int facturaId, CancellationToken cancellationToken = default)
    {
        var factura = await contexto.Facturas.AsNoTracking().SingleAsync(x => x.FacturaId == facturaId, cancellationToken);
        if (factura.Anulada) return "El documento ya está anulado.";
        if (factura.EstadoTransmision == DteEstados.Anulando)
            return await ConciliarInvalidacionAsync(factura, cancellationToken);
        if (DteEstadoFiscal.EsAceptado(factura)) return "El documento ya está aceptado.";
        if (factura.EstadoTransmision is not (DteEstados.Enviado or DteEstados.Incierto or DteEstados.Preparado))
            return "El estado del documento requiere una corrección fiscal controlada; no se retransmitió.";

        var json = JObject.Parse(factura.JsonDte ?? throw new InvalidOperationException("Falta el JSON original."));
        var identificacion = json["identificacion"] as JObject ?? throw new InvalidOperationException("Falta identificación.");
        var ambiente = identificacion.Value<string>("ambiente")!;
        var codigo = identificacion.Value<string>("codigoGeneracion")!;
        var tipo = identificacion.Value<string>("tipoDte") ?? identificacion.Value<string>("tipoEvento")!;
        if (!string.Equals(codigo, factura.CodigoGeneracion, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(factura.AmbienteFiscal) && factura.AmbienteFiscal != ambiente))
            throw new InvalidOperationException("La identidad local no coincide con el JSON; se requiere revisión.");

        var historial = await contexto.DteTransmisiones.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CodigoGeneracion == codigo && x.AmbienteFiscal == ambiente && x.TipoOperacion == "EMISION", cancellationToken);
        if (historial is not null && !JToken.DeepEquals(JObject.Parse(historial.JsonEnviado), json))
            throw new InvalidOperationException("El historial no corresponde al JSON persistido.");
        if (historial is null)
            return "No hay un intento fiscal registrado para esta identidad. Use el flujo de emisión de la operación original.";
        if (historial.FacturaId.HasValue && historial.FacturaId != facturaId)
            throw new InvalidOperationException("La transmisión pertenece a otra factura.");
        DteConsultaResultado resultado;
        if (historial?.Estado == DteEstados.Aceptado && DteConsultaClient.SelloValido(historial.SelloRecepcion))
            resultado = new(true, historial.SelloRecepcion, historial.Respuesta ?? "", "Aceptación recuperada del historial local.");
        else
        {
            // Evitar competir con una transmisión todavía en curso.
            if (historial?.Estado == DteEstados.Enviado && historial.FechaInicio > DateTime.UtcNow.AddMinutes(-10))
                return "La transmisión está en curso. Espere antes de consultar su resultado.";
            if (historial.Estado == DteEstados.Preparado)
                return "El intento no llegó al envío fiscal. Reanude la operación original con su mismo documento.";
            if (historial.Estado is not (DteEstados.Enviado or DteEstados.Incierto))
                return "El historial requiere una corrección fiscal controlada; no se alteró la reserva.";
            var configuracion = await configuraciones.ObtenerAsync(ambiente, cancellationToken);
            var emisor = json["emisor"] as JObject ?? json["donatario"] as JObject;
            var nit = (emisor?.Value<string>("nit") ?? emisor?.Value<string>("numDocumento")
                ?? throw new InvalidOperationException("Falta el NIT del documento original.")).Replace("-", "").Replace(" ", "");
            resultado = await consultas.ConsultarAsync(configuracion, nit, tipo, codigo, cancellationToken);
        }
        if (!resultado.Aceptado) return resultado.Mensaje;

        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using (var tx = await contexto.Database.BeginTransactionAsync(cierre.Token))
        {
            if (historial.Estado != DteEstados.Aceptado)
            {
                var conciliada = await contexto.DteTransmisiones.Where(x => x.Id == historial.Id
                    && x.Estado == historial.Estado && x.Intentos == historial.Intentos)
                    .ExecuteUpdateAsync(x => x.SetProperty(t => t.Estado, DteEstados.Aceptado)
                        .SetProperty(t => t.SelloRecepcion, resultado.Sello).SetProperty(t => t.Respuesta, resultado.Respuesta)
                        .SetProperty(t => t.FechaFin, DateTime.UtcNow), cierre.Token);
                if (conciliada != 1) return "El historial cambió durante la consulta. Vuelva a revisar el resultado.";
            }
            var actualizada = await contexto.Facturas.Where(x => x.FacturaId == facturaId && !x.Anulada
                && x.EstadoTransmision == factura.EstadoTransmision
                && x.CodigoGeneracion == codigo && EF.Functions.Collate(x.JsonDte!, "Latin1_General_100_BIN2") == factura.JsonDte)
                .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, DteEstados.Aceptado)
                    .SetProperty(f => f.SelloRecepcion, resultado.Sello).SetProperty(f => f.FechaAceptacion, DateTime.UtcNow)
                    .SetProperty(f => f.RespuestaHacienda, resultado.Respuesta).SetProperty(f => f.AmbienteFiscal, ambiente), cierre.Token);
            if (actualizada != 1) return "El documento cambió durante la consulta. Vuelva a revisar su estado.";
            await tx.CommitAsync(cierre.Token);
        }
        factura.SelloRecepcion = resultado.Sello;
        factura.EstadoTransmision = DteEstados.Aceptado;
        if (!string.IsNullOrWhiteSpace(factura.CorreoReceptor))
        {
            try { await correo.EncolarDteAsync(factura, tipo, factura.JsonDte!, resultado.Sello, factura.CorreoReceptor, cierre.Token); }
            catch (Exception ex) { logger.LogError(ex, "No pudo encolarse el correo conciliado {FacturaId}", facturaId); }
        }
        return resultado.Mensaje;
    }

    private async Task<string> ConciliarInvalidacionAsync(Factura factura, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(factura.JsonAnulacion)) return "Falta el JSON reservado de invalidación; se requiere revisión.";
        var json = JObject.Parse(factura.JsonAnulacion);
        var codigo = json["identificacion"]!.Value<string>("codigoGeneracion");
        var ambiente = json["identificacion"]!.Value<string>("ambiente");
        var evento = await contexto.DteEventos.AsNoTracking().SingleOrDefaultAsync(x => x.FacturaId == factura.FacturaId
            && x.TipoEvento == "INVALIDACION" && x.CodigoEvento == codigo && x.AmbienteFiscal == ambiente, ct);
        if (evento is null || !PuedeRecuperarInvalidacion(factura, evento))
            return "No existe evidencia local de invalidación aceptada. Se conserva la reserva y el mismo código; no se retransmitió.";
        var actualizada = await contexto.Facturas.Where(x => x.FacturaId == factura.FacturaId && !x.Anulada
            && x.EstadoTransmision == DteEstados.Anulando
            && EF.Functions.Collate(x.JsonAnulacion!, "Latin1_General_100_BIN2") == factura.JsonAnulacion)
            .ExecuteUpdateAsync(x => x.SetProperty(f => f.Anulada, true).SetProperty(f => f.EstadoTransmision, DteEstados.Anulado)
                .SetProperty(f => f.SelloAnulacion, evento.SelloRecepcion).SetProperty(f => f.FechaAnulacion, DateTime.UtcNow), ct);
        if (actualizada != 1) return "La invalidación cambió durante la conciliación; revise su estado.";
        factura.Anulada = true;
        factura.EstadoTransmision = DteEstados.Anulado;
        factura.SelloAnulacion = evento.SelloRecepcion;
        var original = JObject.Parse(factura.JsonDte ?? "{}");
        var destinatario = original["documento"]?.Value<string>("correo") ?? factura.CorreoReceptor;
        if (!string.IsNullOrWhiteSpace(destinatario))
        {
            try { await correo.EncolarInvalidacionAsync(factura, factura.JsonAnulacion, evento.SelloRecepcion, destinatario, ct); }
            catch (Exception ex) { logger.LogError(ex, "No pudo encolarse la invalidación conciliada {FacturaId}", factura.FacturaId); }
        }
        return "Invalidación aceptada recuperada del historial; no se volvió a enviar.";
    }

    internal static bool PuedeRecuperarInvalidacion(Factura factura, SRAUMOAR.Entidades.FacturacionElectronica.DteEvento evento)
    {
        if (factura.Anulada || factura.EstadoTransmision != DteEstados.Anulando
            || evento.FacturaId != factura.FacturaId || evento.TipoEvento != "INVALIDACION"
            || evento.Estado != DteEstados.Aceptado || !DteConsultaClient.SelloValido(evento.SelloRecepcion)) return false;
        try
        {
            var reservado = JObject.Parse(factura.JsonAnulacion ?? "{}");
            return reservado["identificacion"]?.Value<string>("codigoGeneracion") == evento.CodigoEvento
                && reservado["identificacion"]?.Value<string>("ambiente") == evento.AmbienteFiscal
                && JToken.DeepEquals(reservado, JObject.Parse(evento.JsonEvento));
        }
        catch (Newtonsoft.Json.JsonException) { return false; }
    }
}
