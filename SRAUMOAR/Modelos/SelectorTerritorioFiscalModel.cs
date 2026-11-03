namespace SRAUMOAR.Modelos;

public sealed record SelectorTerritorioFiscalModel(string Prefijo, string CampoDepartamento, string CampoMunicipio,
    string CampoDistrito, string? Departamento = null, string? Municipio = null, string? Distrito = null);

public sealed record UbicacionAlumnoModalModel(int AlumnoId, string Nombre, bool AbrirSiFalta = true);
