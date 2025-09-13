using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.Autenticacion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CrearUsuarioAlumnoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IEmailService _emailService;

        public CrearUsuarioAlumnoModel(SRAUMOAR.Modelos.Contexto context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        [BindProperty(SupportsGet = true)]
        public int AlumnoId { get; set; }

        public string nombre { get; set; }
        public string email { get; set; }
        public IActionResult OnGet()
        {
            
            Alumno resultado = _context.Alumno.Where(x => x.AlumnoId == AlumnoId).FirstOrDefault();

            nombre = $"{resultado.Nombres} {resultado.Apellidos}";
            email = resultado.Email;


            ViewData["NivelAccesoId"] = new SelectList(_context.NivelesAcceso.Where(x=>x.Id==4), "Id", "Nombre");
            return Page();
        }

        [BindProperty]
        public Usuario Usuario { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            
            try
            {
                Usuario.Activo = true;
                _context.Usuarios.Add(Usuario);
                await _context.SaveChangesAsync();

                // Actualizando el id de usuario en el alumno
                int nuevoUsuarioId = Usuario.IdUsuario;

                // Buscar el alumno correspondiente
                Alumno? alumno = await _context.Alumno.FindAsync(AlumnoId);
                if (alumno != null)
                {
                    Console.WriteLine($"=== ALUMNO ENCONTRADO ===");
                    Console.WriteLine($"Alumno ID: {alumno.AlumnoId}");
                    Console.WriteLine($"Email: {alumno.Email}");
                    Console.WriteLine($"Nombres: {alumno.Nombres} {alumno.Apellidos}");
                    
                    alumno.UsuarioId = nuevoUsuarioId;
                    await _context.SaveChangesAsync();

                    // Enviar notificación por email
                    Console.WriteLine($"=== LLAMANDO MÉTODO DE ENVÍO DE EMAIL ===");
                    await EnviarNotificacionCreacionUsuarioAsync(alumno.Email, Usuario.NombreUsuario, Usuario.Clave, $"{alumno.Nombres} {alumno.Apellidos}");
                }
                else
                {
                    Console.WriteLine($"=== ERROR: ALUMNO NO ENCONTRADO ===");
                    Console.WriteLine($"AlumnoId buscado: {AlumnoId}");
                }

                TempData["SuccessMessage"] = "Usuario creado exitosamente. Se ha enviado un correo electrónico con las credenciales de acceso.";
                return Redirect("/alumno");
            }
            catch (Exception ex)
            {
                // En caso de error, agregar mensaje de error y volver a la página
                ModelState.AddModelError("", "Error al crear el usuario: " + ex.Message);
                return Page();
            }
        }

        private async Task EnviarNotificacionCreacionUsuarioAsync(string email, string nombreUsuario, string contrasena, string nombreCompleto)
        {
            try
            {
                Console.WriteLine($"=== INICIANDO ENVÍO DE EMAIL ===");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Nombre Usuario: {nombreUsuario}");
                Console.WriteLine($"Nombre Completo: {nombreCompleto}");
                
                // Usar el método que ya funciona en ResetearContrasenas
                bool resultado = await _emailService.EnviarNotificacionCambioContrasenaAsync(email, nombreUsuario, contrasena, nombreCompleto);
                
                Console.WriteLine($"Resultado del envío: {resultado}");
                Console.WriteLine($"=== FIN ENVÍO DE EMAIL ===");
            }
            catch (Exception ex)
            {
                // Log del error (en producción usar ILogger)
                Console.WriteLine($"Error enviando email de creación de usuario: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

    }
}
