namespace SRAUMOAR.Entidades.Generales
{
    public class ConfiguracionReporte
    {
        public int Id { get; set; }
        public string Reporte { get; set; } = null!;
        public string Clave { get; set; } = null!;
        public string Valor { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
    }
}
