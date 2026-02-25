using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Pages.arancelesBecados
{
    public class EditModel : PageModel
    {
        private readonly Contexto _context;

        public EditModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public ArancelBecado ArancelBecado { get; set; } = default!;

        public SelectList AlumnosBecadosList { get; set; } = default!;
        public SelectList ArancelesList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var arancelBecado = await _context.ArancelesBecados
                .Include(ab => ab.Becado)
                .Include(ab => ab.Arancel)
                .FirstOrDefaultAsync(ab => ab.ArancelBecadoId == id);

            if (arancelBecado == null)
            {
                return NotFound();
            }

            ArancelBecado = arancelBecado;

            // Cargar las listas
            await CargarListas();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarListas();
                return Page();
            }

            // Verificar que no exista ya un arancel personalizado para este alumno y arancel (excluyendo el actual)
            var existe = await _context.ArancelesBecados
                .AnyAsync(ab => ab.BecadosId == ArancelBecado.BecadosId && 
                               ab.ArancelId == ArancelBecado.ArancelId && 
                               ab.ArancelBecadoId != ArancelBecado.ArancelBecadoId && 
                               ab.Activo);

            if (existe)
            {
                ModelState.AddModelError("", "Ya existe un arancel personalizado para este alumno y arancel.");
                await CargarListas();
                return Page();
            }

            // Obtener el arancel para calcular el porcentaje de descuento
            var arancel = await _context.Aranceles.FindAsync(ArancelBecado.ArancelId);
            if (arancel != null)
            {
                ArancelBecado.PorcentajeDescuento = arancel.Costo > 0 ? 
                    ((arancel.Costo - ArancelBecado.PrecioPersonalizado) / arancel.Costo) * 100 : 0;
            }

            _context.Attach(ArancelBecado).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArancelBecadoExists(ArancelBecado.ArancelBecadoId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private async Task CargarListas()
        {
            // Obtener solo alumnos con beca parcial
            var alumnosBecados = await _context.Becados
                .Include(b => b.Alumno)
                .Include(b => b.EntidadBeca)
                .Include(b => b.Ciclo)
                .Where(b => b.Estado && b.TipoBeca == 2) // TipoBeca = 2 es parcial
                .Select(b => new
                {
                    BecadosId = b.BecadosId,
                    DisplayText = $"{b.Alumno.Nombres} {b.Alumno.Apellidos} - {b.EntidadBeca.Nombre} (Ciclo {b.Ciclo.NCiclo} - {b.Ciclo.anio})"
                })
                .ToListAsync();

            AlumnosBecadosList = new SelectList(alumnosBecados, "BecadosId", "DisplayText");

            // Obtener ciclo actual y filtrar aranceles por ciclo (incluir arancel actual si no está en ciclo actual)
            var cicloActual = await _context.Ciclos.Where(c => c.Activo).FirstOrDefaultAsync();
            var aranceles = await _context.Aranceles
                .Where(a => a.Activo && a.Obligatorio && (
                    (cicloActual != null && a.CicloId == cicloActual.Id) ||
                    a.ArancelId == ArancelBecado.ArancelId))
                .Select(a => new
                {
                    ArancelId = a.ArancelId,
                    DisplayText = $"{a.Nombre} - {a.Costo:C}"
                })
                .ToListAsync();

            ArancelesList = new SelectList(aranceles, "ArancelId", "DisplayText");
        }

        private bool ArancelBecadoExists(int id)
        {
            return _context.ArancelesBecados.Any(e => e.ArancelBecadoId == id);
        }
    }
} 