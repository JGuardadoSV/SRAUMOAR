using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Pages.arancelesBecados
{
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        public IList<ArancelBecado> ArancelesBecados { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string? SearchAlumno { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCicloId { get; set; }

        public List<SelectListItem> Ciclos { get; set; } = new();

        public async Task OnGetAsync()
        {
            await CargarCiclosAsync();

            if (!SelectedCicloId.HasValue)
            {
                SelectedCicloId = await ObtenerCicloPorDefectoAsync() ?? 0;
            }

            var query = _context.ArancelesBecados
                .AsNoTracking()
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Alumno)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.EntidadBeca)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Ciclo)
                .Include(ab => ab.Arancel)
                .Where(ab => ab.Activo);

            if (SelectedCicloId.HasValue && SelectedCicloId.Value > 0)
            {
                query = query.Where(ab => ab.Becado != null && ab.Becado.CicloId == SelectedCicloId.Value);
            }

            if (!string.IsNullOrWhiteSpace(SearchAlumno))
            {
                var term = SearchAlumno.Trim();
                var like = $"%{term}%";

                query = query.Where(ab =>
                    ab.Becado != null &&
                    ab.Becado.Alumno != null &&
                    (
                        EF.Functions.Like((ab.Becado.Alumno.Nombres ?? ""), like) ||
                        EF.Functions.Like((ab.Becado.Alumno.Apellidos ?? ""), like) ||
                        EF.Functions.Like(((ab.Becado.Alumno.Nombres ?? "") + " " + (ab.Becado.Alumno.Apellidos ?? "")), like) ||
                        EF.Functions.Like(((ab.Becado.Alumno.Apellidos ?? "") + " " + (ab.Becado.Alumno.Nombres ?? "")), like)
                    )
                );
            }

            ArancelesBecados = await query
                .OrderByDescending(ab => ab.FechaRegistro)
                .ToListAsync();
        }

        private async Task CargarCiclosAsync()
        {
            var ciclos = await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => new { c.Id, c.NCiclo, c.anio })
                .ToListAsync();

            Ciclos = new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Todos los ciclos" }
            };

            Ciclos.AddRange(ciclos.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"Ciclo {c.NCiclo} - {c.anio}"
            }));
        }

        private async Task<int?> ObtenerCicloPorDefectoAsync()
        {
            // Preferir ciclo activo; si no existe, usar el mas reciente.
            var cicloActivoId = await _context.Ciclos
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (cicloActivoId.HasValue)
            {
                return cicloActivoId.Value;
            }

            return await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
        }
    }
}
