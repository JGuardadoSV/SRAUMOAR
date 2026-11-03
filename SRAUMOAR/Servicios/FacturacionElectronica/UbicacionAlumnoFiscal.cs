using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public static class UbicacionAlumnoFiscal
{
    public static bool EsCompleta(Alumno alumno) => CatalogoTerritorialFiscal.EsValida(alumno.CodigoDepartamentoFiscal,
        alumno.CodigoMunicipioFiscal, alumno.CodigoDistritoFiscal) && !string.IsNullOrWhiteSpace(alumno.DireccionDeResidencia)
        && alumno.DireccionDeResidencia.Length <= 200;
}
