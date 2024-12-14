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

        public async Task OnGetAsync()
        {
            Ciclo cicloactual=await _context.Ciclos.Where(x=>x.Activo).FirstAsync();
             totalgrupos = _context.Grupo.Where(x => x.CicloId == cicloactual.Id).Count();
             totalinscritos = _context.Inscripciones.Where(x => x.CicloId == cicloactual.Id).Count();
            ciclo= cicloactual.NCiclo.ToString()+"/"+ cicloactual.anio.ToString();


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
