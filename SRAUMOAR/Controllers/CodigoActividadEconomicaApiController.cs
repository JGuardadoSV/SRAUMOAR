using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.FacturacionElectronica;
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
            var catalogoImportado = _context.CatalogosFe
                .AsNoTracking()
                .Where(x => x.CodigoCatalogo == "CAT-019" && x.Version == "1.1" && x.Activo);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var texto = term.Trim();
                catalogoImportado = catalogoImportado.Where(x => x.Codigo.Contains(texto) || x.Descripcion.Contains(texto));
            }

            var catalogos = await catalogoImportado
                .OrderBy(x => x.Codigo)
                .Take(50)
                .Select(x => new
                {
                    id = x.Id,
                    codigo = x.Codigo,
                    descripcion = x.Descripcion,
                    textoCompleto = $"{x.Codigo} - {x.Descripcion}"
                })
                .ToListAsync();

            if (catalogos.Count > 0)
                return Ok(catalogos);

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







