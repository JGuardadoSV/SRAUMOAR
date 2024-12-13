using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Pages
{
    public class HomeModel : PageModel
    {
        private readonly ILogger<HomeModel> _logger;
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public HomeModel(ILogger<HomeModel> logger, SRAUMOAR.Modelos.Contexto context)
        {
            _logger = logger;
            _context = context;
        }

        public IList<Alumno> Alumno { get; set; } = default!;
        
        public async Task OnGetAsync()
        {
            var currentYear = DateTime.Now.Year;
           Alumno = await _context.Alumno
                .Include(x => x.Municipio)
                .Include(x => x.Carrera)
                .Where(x => x.FechaDeRegistro.Year == currentYear)
                .OrderByDescending(x => x.FechaDeRegistro)
                .ToListAsync();

        }
    }
}
