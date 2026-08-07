using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Entidades.Generales;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public static class DteEmisorConfigMapper
{
    public static EmisorConfig Crear(ConfiguracionFacturacionElectronica configuracion)
    {
        ConfiguracionFeService.ValidarDatosEmisor(configuracion);

        return new EmisorConfig
        {
            NIT = configuracion.NitEmisor!,
            NRC = configuracion.NrcEmisor!,
            GIRO = configuracion.DescripcionActividad!,
            CODACTIVIDAD = configuracion.CodigoActividad!,
            DIRECCION = configuracion.ComplementoDireccion!,
            DEPARTAMENTO = configuracion.CodigoDepartamento!,
            // La propiedad legacy DISTRITO se usa como municipio en los formularios existentes.
            DISTRITO = configuracion.CodigoMunicipio!,
            MUNICIPIO = configuracion.CodigoMunicipio!,
            CODIGODISTRITO = configuracion.CodigoDistrito!,
            EMAIL = configuracion.CorreoEmisor!,
            TELEFONO = configuracion.TelefonoEmisor!,
            NOMBRE = configuracion.NombreEmisor!,
            NOMBRECOMERCIAL = configuracion.NombreComercial ?? configuracion.NombreEmisor!,
            AMBIENTE = configuracion.AmbienteFiscal == "01" ? 1 : 0
        };
    }
}
