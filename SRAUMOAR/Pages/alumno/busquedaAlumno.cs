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
            var students = await _context.Alumno
            .Where(s => s.Nombres.Contains(term))
            .Select(s => new
            {
                id = s.AlumnoId,
                name = s.Nombres,
                photoUrl = s.Foto != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(s.Foto)}" : null,

            })
            .Take(10)
            .ToListAsync();

            return new JsonResult(students);
        }
    }
}
