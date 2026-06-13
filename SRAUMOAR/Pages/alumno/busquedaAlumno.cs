using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Pages.alumno
{

    [Route("api/alumnosapi")]
    [ApiController]
    public class busquedaAlumno : Controller
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public busquedaAlumno(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Alumno> Alumno { get; set; } = default!;
        [HttpGet("busquedaajax1")]
        public async Task<IActionResult> OnGetSearch(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new JsonResult(new List<object>());
            }

            // Separar por espacios para poder buscar por nombres, apellidos o la combinación
            var palabras = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var query = _context.Alumno.AsQueryable();

            foreach (var palabra in palabras)
            {
                var p = palabra.Trim();
                query = query.Where(s => s.Nombres.Contains(p) || 
                                         s.Apellidos.Contains(p) || 
                                         (s.Carnet != null && s.Carnet.Contains(p)) ||
                                         s.Email.Contains(p));
            }

            var students = await query
                .Select(s => new
                {
                    id = s.AlumnoId,
                    name = $"{s.Nombres} {s.Apellidos}",
                    photoUrl = s.Foto != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(s.Foto)}" : null
                })
                .Take(10)
                .ToListAsync();

            return new JsonResult(students);
        }
    }
}
