using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistorialController : ControllerBase
    {
        private readonly Contexto _context;

        public HistorialController(Contexto context)
        {
            _context = context;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "Controlador Historial funcionando correctamente" });
        }

        [HttpGet("test2")]
        public IActionResult Test2()
        {
            return Ok(new { message = "Test2 funcionando" });
        }

        [HttpPost("test3")]
        public IActionResult Test3()
        {
            return Ok(new { message = "Test3 POST funcionando" });
        }

        [HttpGet("eliminarMateria")]
        public async Task<IActionResult> EliminarMateria([FromQuery] int id)
        {
            try
            {
                // Buscar la materia del historial
                var historialMateria = await _context.HistorialMateria
                    .Include(hm => hm.Materia)
                    .FirstOrDefaultAsync(hm => hm.HistorialMateriaId == id);

                if (historialMateria == null)
                {
                    return NotFound(new { success = false, message = "Materia no encontrada" });
                }

                // Obtener nombre de la materia (puede ser de Materia o libre)
                string nombreMateria = historialMateria.Materia != null 
                    ? historialMateria.Materia.NombreMateria 
                    : (historialMateria.MateriaNombreLibre ?? "Materia sin nombre");

                // Eliminar la materia del historial
                _context.HistorialMateria.Remove(historialMateria);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    success = true, 
                    message = $"Materia '{nombreMateria}' eliminada exitosamente" 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error interno del servidor: " + ex.Message 
                });
            }
        }
    }
}
