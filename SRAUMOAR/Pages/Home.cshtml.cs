using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;

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
        public string ciclo { get; set; }
        public int totalgrupos { get; set; }
        public int totalinscritos { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int PageSize { get; set; } = 10;
        
        public int TotalPages { get; set; }
        
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public async Task OnGetAsync()
        {
            Ciclo cicloactual = await _context.Ciclos.Where(x => x.Activo).FirstAsync();
            totalgrupos = _context.Grupo.Where(x => x.CicloId == cicloactual.Id).Count();
            totalinscritos = _context.Inscripciones.Where(x => x.CicloId == cicloactual.Id).Count();
            ciclo = cicloactual.NCiclo.ToString() + "/" + cicloactual.anio.ToString();

            var currentYear = DateTime.Now.Year;
            var query = _context.Alumno
                .Include(x => x.Municipio)
                .Include(x => x.Carrera)
                .Where(x => x.FechaDeRegistro.Year == currentYear)
                .OrderByDescending(x => x.FechaDeRegistro);

            var totalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

            Alumno = await query
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
