using Microsoft.Extensions.Configuration;

namespace SRAUMOAR.Servicios
{
    public class EmisorConfigService
    {
        private readonly IConfiguration _configuration;

        public EmisorConfigService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string NIT => _configuration["EMISOR:NIT"];
        public string NRC => _configuration["EMISOR:NRC"];
        public string GIRO => _configuration["EMISOR:GIRO"];
        public string CODACTIVIDAD => _configuration["EMISOR:CODACTIVIDAD"];
        public string DIRECCION => _configuration["EMISOR:DIRECCION"];
        public string DEPARTAMENTO => _configuration["EMISOR:DEPARTAMENTO"];
        public string DISTRITO => _configuration["EMISOR:DISTRITO"];
        public string EMAIL => _configuration["EMISOR:EMAIL"];
        public string EMAILBCC => _configuration["EMISOR:EMAILBCC"];
        public string TELEFONO => _configuration["EMISOR:TELEFONO"];
        public string NOMBRE => _configuration["EMISOR:NOMBRE"];
        public string NOMBRECOMERCIAL => _configuration["EMISOR:NOMBRECOMERCIAL"];
        public int AMBIENTE => _configuration.GetValue<int>("EMISOR:AMBIENTE");
        public string CLAVETESTCERTIFICADO => _configuration["EMISOR:CLAVETESTCERTIFICADO"];
        public string CLAVEPRODCERTIFICADO => _configuration["EMISOR:CLAVEPRODCERTIFICADO"];
        public string CLAVETESTAPI => _configuration["EMISOR:CLAVETESTAPI"];
        public string CLAVEPRODAPI => _configuration["EMISOR:CLAVEPRODAPI"];

        public string ApiUrl => AMBIENTE == 1 ? "http://207.58.153.147:7122" : "http://207.58.153.147:7122";
        public string AmbienteString => AMBIENTE == 0 ? "00" : "01";
        public string PasswordAPI => AMBIENTE == 0 ? CLAVETESTAPI : CLAVEPRODAPI;
        public string PasswordCertificado => AMBIENTE == 0 ? CLAVETESTCERTIFICADO : CLAVEPRODCERTIFICADO;
    }
}
