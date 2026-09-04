namespace SRAUMOAR.Modelos.FacturacionElectronica;

public sealed record DteRetornoLineaRequest(int NumeroItemOriginal, decimal Cantidad, decimal? PrecioUnitario = null);

public sealed record DteRetornoRequest(
    int FacturaOriginalId,
    string TipoRetorno,
    string Motivo,
    IReadOnlyCollection<DteRetornoLineaRequest> Lineas,
    Guid OperacionId = default);

public sealed record DteRetornoLineaPreparada(
    int NumeroItemOriginal,
    string? Codigo,
    string Descripcion,
    decimal CantidadOriginal,
    decimal CantidadRetornada,
    decimal PrecioUnitario,
    string ClasificacionFiscal);

public sealed record DteRetornoPreparado(
    int FacturaOriginalId,
    string AmbienteFiscal,
    string Json,
    string CodigoGeneracion,
    string NumeroControl,
    decimal TotalGravado,
    decimal TotalExento,
    decimal TotalPagar,
    decimal TotalIva,
    string? CorreoReceptor,
    string TipoRetorno,
    string Motivo,
    string HashJsonOriginal,
    IReadOnlyCollection<DteRetornoLineaPreparada> Lineas);

public sealed record DteRetornoResultado(
    int FacturaId,
    string CodigoGeneracion,
    string NumeroControl,
    bool Aceptado,
    string Estado,
    string AmbienteFiscal,
    string? SelloRecepcion,
    string? Error);

public sealed record DteRetornoLineaDisponible(
    int NumeroItemOriginal,
    string? Codigo,
    string Descripcion,
    decimal CantidadOriginal,
    decimal CantidadRetornada,
    decimal CantidadDisponible,
    decimal PrecioUnitario,
    string ClasificacionFiscal);

public sealed record DteRetornoDisponible(
    int FacturaOriginalId,
    string TipoDocumento,
    string CodigoGeneracion,
    string AmbienteFiscal,
    IReadOnlyCollection<DteRetornoLineaDisponible> Lineas);
