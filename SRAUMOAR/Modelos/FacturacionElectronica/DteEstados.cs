namespace SRAUMOAR.Modelos.FacturacionElectronica;

public static class DteEstados
{
    public const string Preparado = "Preparado";
    public const string Enviado = "Enviado";
    public const string Aceptado = "Aceptado";
    public const string Rechazado = "Rechazado";
    public const string Incierto = "Incierto";
    public const string Anulado = "Anulado";
}

public static class DteTipos
{
    public const string Factura = "01";
    public const string CreditoFiscal = "03";
    public const string SujetoExcluido = "14";
    public const string Donacion = "15";
    public const string NotaCredito = "05";
    public const string NotaDebito = "06";
    public const string Invalidacion = "INVALIDACION";
}
