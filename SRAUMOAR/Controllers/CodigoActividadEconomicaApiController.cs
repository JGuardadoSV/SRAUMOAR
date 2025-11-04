using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CodigoActividadEconomicaApiController : ControllerBase
    {
        private readonly Contexto _context;

        public CodigoActividadEconomicaApiController(Contexto context)
        {
            _context = context;
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarCodigos([FromQuery] string? term = null)
        {
            var query = _context.CodigosActividadEconomica.AsQueryable();

            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(c => 
                    c.Codigo!.Contains(term) || 
                    c.Descripcion!.Contains(term));
            }

            var codigos = await query
                .OrderBy(c => c.Codigo)
                .Take(50) // Limitar a 50 resultados para mejor rendimiento
                .Select(c => new
                {
                    id = c.Id,
                    codigo = c.Codigo,
                    descripcion = c.Descripcion,
                    textoCompleto = $"{c.Codigo} - {c.Descripcion}"
                })
                .ToListAsync();

            return Ok(codigos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var codigo = await _context.CodigosActividadEconomica
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    id = c.Id,
                    codigo = c.Codigo,
                    descripcion = c.Descripcion
                })
                .FirstOrDefaultAsync();

            if (codigo == null)
            {
                return NotFound();
            }

            return Ok(codigo);
        }
    }
}




