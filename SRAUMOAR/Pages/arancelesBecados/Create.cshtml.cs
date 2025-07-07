using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Pages.arancelesBecados
{
    public class CreateModel : PageModel
    {
        private readonly Contexto _context;

        public CreateModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public ArancelBecado ArancelBecado { get; set; } = default!;

        public SelectList AlumnosBecadosList { get; set; } = default!;
        public SelectList ArancelesList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
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

            // Obtener solo aranceles activos y obligatorios
            var aranceles = await _context.Aranceles
                .Where(a => a.Activo && a.Obligatorio)
                .Select(a => new
                {
                    ArancelId = a.ArancelId,
                    DisplayText = $"{a.Nombre} - {a.Costo:C}"
                })
                .ToListAsync();

            ArancelesList = new SelectList(aranceles, "ArancelId", "DisplayText");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(); // Recargar las listas
                return Page();
            }

            // Verificar que no exista ya un arancel personalizado para este alumno y arancel
            var existe = await _context.ArancelesBecados
                .AnyAsync(ab => ab.BecadosId == ArancelBecado.BecadosId && 
                               ab.ArancelId == ArancelBecado.ArancelId && 
                               ab.Activo);

            if (existe)
            {
                ModelState.AddModelError("", "Ya existe un arancel personalizado para este alumno y arancel.");
                await OnGetAsync(); // Recargar las listas
                return Page();
            }

            // Obtener el arancel para calcular el porcentaje de descuento
            var arancel = await _context.Aranceles.FindAsync(ArancelBecado.ArancelId);
            if (arancel != null)
            {
                ArancelBecado.PorcentajeDescuento = arancel.Costo > 0 ? 
                    ((arancel.Costo - ArancelBecado.PrecioPersonalizado) / arancel.Costo) * 100 : 0;
            }

            _context.ArancelesBecados.Add(ArancelBecado);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
} 