namespace SRAUMOAR.Modelos.FacturacionElectronica;

public sealed record DteNotaRequest(
    int FacturaOriginalId,
    string TipoNota,
    decimal? Monto,
    string? Motivo,
    string? DepartamentoReceptor = null,
    string? MunicipioReceptor = null,
    string? DistritoReceptor = null,
    Guid OperacionId = default);

public sealed record DteNotaPreparada(
    int FacturaOriginalId,
    string TipoNota,
    string AmbienteFiscal,
    string Json,
    string CodigoGeneracion,
    string NumeroControl,
    int Version,
    decimal Monto,
    string? CorreoReceptor,
    string? Motivo,
    decimal TotalGravado,
    decimal TotalExento,
    decimal TotalIva);

public sealed record DteNotaResultado(
    int FacturaId,
    string TipoNota,
    string CodigoGeneracion,
    string NumeroControl,
    bool Aceptado,
    string Estado,
    string AmbienteFiscal,
    string? SelloRecepcion,
    string? Error);
