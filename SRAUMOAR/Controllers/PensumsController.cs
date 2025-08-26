using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PensumsController : ControllerBase
    {
        private readonly Contexto _context;

        public PensumsController(Contexto context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPensums([FromQuery] int carreraId)
        {
            try
            {
                var pensums = await _context.Pensums
                    .Where(p => p.CarreraId == carreraId)
                    .OrderByDescending(p => p.Anio)
                    .Select(p => new
                    {
                        pensumId = p.PensumId,
                        anio = p.Anio
                    })
                    .ToListAsync();

                return Ok(pensums);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener los pensúms: " + ex.Message });
            }
        }
    }
}

