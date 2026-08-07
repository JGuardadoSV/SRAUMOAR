namespace SRAUMOAR.Modelos.FacturacionElectronica;

public sealed record DteTransmisionRequest(
    string Json,
    string TipoDte,
    string CodigoGeneracion,
    string NumeroControl,
    int Version,
    string? CorreoReceptor = null,
    int? FacturaId = null);

public sealed record DteTransmisionResult(
    bool Aceptado,
    string Estado,
    string? SelloRecepcion,
    string Respuesta,
    int Intentos,
    string AmbienteFiscal,
    string? Error = null);

public sealed record DteEventoResult(
    bool Aceptado,
    string Estado,
    string? SelloRecepcion,
    string Respuesta,
    string AmbienteFiscal,
    string? Error = null);
