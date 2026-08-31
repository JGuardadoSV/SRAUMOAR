using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public static class DteEstadoFiscal
{
    // La migración introdujo Preparado como valor inicial incluso para documentos históricos sellados.
    public static bool EsAceptado(Factura factura)
        => !factura.Anulada && !string.IsNullOrWhiteSpace(factura.SelloRecepcion)
            && (factura.EstadoTransmision == DteEstados.Aceptado
                || factura.EstadoTransmision == DteEstados.Preparado && factura.IntentosTransmision == 0);
}
