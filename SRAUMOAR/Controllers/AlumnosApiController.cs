using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Alumnos;
using System.Security.Claims;

namespace SRAUMOAR.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AlumnosApiController : ControllerBase
    {
        private readonly Contexto _context;

        public AlumnosApiController(Contexto context)
        {
            _context = context;
        }

        [HttpGet("obtenerfoto")]
        [Authorize(Roles = "Estudiantes")]
        public async Task<IActionResult> ObtenerFoto()
        {
            try
            {
                // Obtener el ID del usuario autenticado
                var userId = User.FindFirstValue("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "Usuario no identificado" });
                }

                // Buscar el alumno por el ID de usuario
                var alumno = await _context.Alumno
                    .Where(a => a.UsuarioId == int.Parse(userId))
                    .Select(a => new { a.Foto })
                    .FirstOrDefaultAsync();

                if (alumno == null)
                {
                    return NotFound(new { success = false, message = "Alumno no encontrado" });
                }

                if (alumno.Foto == null || alumno.Foto.Length == 0)
                {
                    return Ok(new { success = false, message = "El alumno no tiene foto" });
                }

                // Convertir la foto a base64
                var fotoBase64 = Convert.ToBase64String(alumno.Foto);

                return Ok(new { 
                    success = true, 
                    foto = fotoBase64,
                    message = "Foto obtenida exitosamente" 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error interno del servidor: {ex.Message}" 
                });
            }
        }
    }
}
