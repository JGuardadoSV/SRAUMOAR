using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace SRAUMOAR.Servicios
{
    public interface IEmailService
    {
        Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo, string nombreDestinatario = null);
        Task<bool> EnviarNotificacionCambioContrasenaAsync(string email, string nombreUsuario, string nuevaContrasena, string nombreCompleto);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _servidor;
        private readonly int _puerto;
        private readonly string _usuario;
        private readonly string _contrasena;
        private readonly string _emailRemitente;
        private readonly string _nombreRemitente;
        private readonly string _opcionesSeguridad;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _servidor = _configuration["SMTP_SERVIDOR"] ?? "smtp.devselsalvador.com";
            _puerto = int.Parse(_configuration["SMTP_PUERTO"] ?? "587");
            _usuario = _configuration["SMTP_USUARIO"] ?? "facturas@devselsalvador.com";
            _contrasena = _configuration["SMTP_CONTRASENA"] ?? "*Sq$aW58xhp!";
            _emailRemitente = _configuration["SMTP_EMAIL_REMITENTE"] ?? "facturas@devselsalvador.com";
            _nombreRemitente = _configuration["SMTP_NOMBRE_REMITENTE"] ?? "Seguridad UMOAR";
            _opcionesSeguridad = _configuration["SMTP_OPCIONES_SEGURIDAD"] ?? "None";
        }

        public async Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo, string nombreDestinatario = null)
        {
            try
            {
                using (var cliente = new SmtpClient(_servidor, _puerto))
                {
                    cliente.UseDefaultCredentials = false;
                    cliente.Credentials = new NetworkCredential(_usuario, _contrasena);
                    
                    // Configurar opciones de seguridad según la configuración
                    if (_opcionesSeguridad.Equals("SSL", StringComparison.OrdinalIgnoreCase))
                    {
                        cliente.EnableSsl = true;
                    }
                    else if (_opcionesSeguridad.Equals("TLS", StringComparison.OrdinalIgnoreCase))
                    {
                        cliente.EnableSsl = true;
                    }
                    // Si es "None", no se habilita SSL

                    var mensaje = new MailMessage
                    {
                        From = new MailAddress(_emailRemitente, _nombreRemitente),
                        Subject = asunto,
                        Body = cuerpo,
                        IsBodyHtml = true
                    };

                    mensaje.To.Add(new MailAddress(destinatario, nombreDestinatario ?? destinatario));

                    await cliente.SendMailAsync(mensaje);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log del error (en producción usar ILogger)
                Console.WriteLine($"Error enviando email: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnviarNotificacionCambioContrasenaAsync(string email, string nombreUsuario, string nuevaContrasena, string nombreCompleto)
        {
            var asunto = "Cambio de Contraseña - Sistema SRAUMOAR";
            var cuerpo = GenerarPlantillaCambioContrasena(nombreCompleto, nombreUsuario, nuevaContrasena);
            
            return await EnviarEmailAsync(email, asunto, cuerpo, nombreCompleto);
        }

        private string GenerarPlantillaCambioContrasena(string nombreCompleto, string nombreUsuario, string nuevaContrasena)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f8f9fa; padding: 20px; border: 1px solid #dee2e6; }}
                        .credentials {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; margin-top: 20px; color: #6c757d; font-size: 14px; }}
                        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Sistema de Registro Academico UMOAR</h2>
                            <p>Notificación de Cambio de Contraseña</p>
                        </div>
                        
                        <div class='content'>
                            <p>Estimado/a <strong>{nombreCompleto}</strong>,</p>
                            
                            <p>Se ha realizado un cambio en sus credenciales de acceso al sistema SRAUMOAR.</p>
                            
                            <div class='credentials'>
                                <h4>Sus nuevas credenciales son:</h4>
                                <p><strong>Usuario:</strong> {nombreUsuario}</p>
                                <p><strong>Contraseña:</strong> {nuevaContrasena}</p>
                            </div>
                            
                            <div class='warning'>
                                <p><strong>⚠️ Importante:</strong></p>
                                <ul>
                                    <li>Su contraseña anterior ya no es válida</li>
                                    <li>Guarde esta información en un lugar seguro</li>
                                    <li>No comparta sus credenciales con nadie</li>
                                    <li>Se recomienda cambiar la contraseña después del primer inicio de sesión</li>
                                </ul>
                            </div>
                            
                            <p>Si usted no solicitó este cambio, contacte inmediatamente al administrador del sistema.</p>
                            
                            <p>Atentamente,<br>
                            <strong>Equipo de Sistemas UMOAR</strong></p>
                        </div>
                        
                        <div class='footer'>
                            <p>Este es un mensaje automático, por favor no responda a este correo.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
}
