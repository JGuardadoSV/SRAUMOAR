using System.Net.Mail;
using System.Net;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Servicios
{
    public interface IEmailService
    {
        Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo, string nombreDestinatario = null);
        Task<bool> EnviarNotificacionCambioContrasenaAsync(string email, string nombreUsuario, string nuevaContrasena, string nombreCompleto);
    }

    public class EmailService : IEmailService
    {
        private readonly ISmtpConfigurationService _smtpConfiguration;

        public EmailService(ISmtpConfigurationService smtpConfiguration)
        {
            _smtpConfiguration = smtpConfiguration;
        }

        public async Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo, string nombreDestinatario = null)
        {
            try
            {
                var configuracion = await _smtpConfiguration.ObtenerAsync();
                var entidad = configuracion.Entidad
                    ?? throw new InvalidOperationException("No existe una configuración SMTP activa.");
                var contrasena = await _smtpConfiguration.ObtenerPasswordAsync()
                    ?? throw new InvalidOperationException("La configuración SMTP activa no tiene contraseña.");

                using var cliente = new SmtpClient(entidad.Servidor, entidad.Puerto)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(entidad.Usuario, contrasena),
                    EnableSsl = entidad.UsarSsl || entidad.UsarTls
                };

                using var mensaje = new MailMessage
                {
                    From = new MailAddress(entidad.EmailRemitente, entidad.NombreRemitente),
                    Subject = asunto,
                    Body = cuerpo,
                    IsBodyHtml = true
                };
                mensaje.To.Add(new MailAddress(destinatario, nombreDestinatario ?? destinatario));

                if (!string.IsNullOrWhiteSpace(entidad.CopiaOculta))
                {
                    foreach (var copia in entidad.CopiaOculta.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        mensaje.Bcc.Add(copia);
                }

                await cliente.SendMailAsync(mensaje);
                return true;
            }
            catch (Exception ex)
            {
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
                        .header {{ background-color: #1bad0e; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
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
