using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarrerasController : ControllerBase
    {
        private readonly Contexto _context;

        public CarrerasController(Contexto context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCarreras()
        {
            try
            {
                var carreras = await _context.Carreras
                    .Where(c => c.Activa == true) // Solo carreras activas
                    .OrderBy(c => c.NombreCarrera)
                    .Select(c => new
                    {
                        carreraId = c.CarreraId,
                        nombreCarrera = c.NombreCarrera
                    })
                    .ToListAsync();

                return Ok(carreras);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener las carreras: " + ex.Message });
            }
        }
    }
}
