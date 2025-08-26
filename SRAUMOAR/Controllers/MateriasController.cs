using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MateriasController : ControllerBase
    {
        private readonly Contexto _context;

        public MateriasController(Contexto context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterias([FromQuery] int pensumId)
        {
            try
            {
                var materias = await _context.Materias
                    .Where(m => m.PensumId == pensumId)
                    .OrderBy(m => m.NombreMateria)
                    .Select(m => new
                    {
                        materiaId = m.MateriaId,
                        nombreMateria = m.NombreMateria,
                        codigoMateria = m.CodigoMateria
                    })
                    .ToListAsync();

                return Ok(materias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener las materias: " + ex.Message });
            }
        }
    }
}

