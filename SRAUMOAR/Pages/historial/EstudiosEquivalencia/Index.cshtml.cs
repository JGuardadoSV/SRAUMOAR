using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.historial.EstudiosEquivalencia
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        public List<EstudioEquivalencia> Estudios { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? BuscarTermino { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? AlumnoId { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.EstudiosEquivalencia
                .Include(e => e.Alumno)
                .Include(e => e.Detalles)
                .AsQueryable();

            if (AlumnoId.HasValue)
            {
                query = query.Where(e => e.AlumnoId == AlumnoId.Value);
            }

            if (!string.IsNullOrWhiteSpace(BuscarTermino))
            {
                string term = BuscarTermino.ToLower();
                query = query.Where(e => e.UniversidadOrigen.ToLower().Contains(term) 
                                      || e.CarreraOrigen.ToLower().Contains(term)
                                      || (e.Alumno != null && (e.Alumno.Nombres.ToLower().Contains(term) || e.Alumno.Apellidos.ToLower().Contains(term))));
            }

            Estudios = await query.OrderByDescending(e => e.FechaEstudio).ToListAsync();
        }
    }
}
