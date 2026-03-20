using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.deserciones
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class EditModel : PageModel
    {
        private readonly Contexto _context;

        public EditModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public DesercionAlumno DesercionAlumno { get; set; } = default!;

        public Alumno? AlumnoSeleccionado { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            DesercionAlumno = await _context.DesercionesAlumno
                .Include(d => d.Alumno)
                    .ThenInclude(a => a!.Carrera)
                .FirstOrDefaultAsync(d => d.DesercionAlumnoId == id.Value);

            if (DesercionAlumno == null)
            {
                return NotFound();
            }

            AlumnoSeleccionado = DesercionAlumno.Alumno;
            await CargarListasAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarAlumnoSeleccionadoAsync();
                await CargarListasAsync();
                return Page();
            }

            var duplicado = await _context.DesercionesAlumno
                .AnyAsync(d =>
                    d.DesercionAlumnoId != DesercionAlumno.DesercionAlumnoId &&
                    d.AlumnoId == DesercionAlumno.AlumnoId &&
                    d.CicloId == DesercionAlumno.CicloId);

            if (duplicado)
            {
                ModelState.AddModelError(string.Empty, "Ya existe otro registro para ese alumno en el ciclo seleccionado.");
                await CargarAlumnoSeleccionadoAsync();
                await CargarListasAsync();
                return Page();
            }

            var existente = await _context.DesercionesAlumno
                .FirstOrDefaultAsync(d => d.DesercionAlumnoId == DesercionAlumno.DesercionAlumnoId);

            if (existente == null)
            {
                return NotFound();
            }

            existente.CicloId = DesercionAlumno.CicloId;
            existente.CausaDesercionId = DesercionAlumno.CausaDesercionId;
            existente.Observacion = DesercionAlumno.Observacion;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Registro actualizado correctamente.";
            return RedirectToPage("./Index", new { SelectedCicloId = existente.CicloId });
        }

        private async Task CargarListasAsync()
        {
            ViewData["CicloId"] = new SelectList(
                await _context.Ciclos
                    .AsNoTracking()
                    .OrderByDescending(c => c.anio)
                    .ThenByDescending(c => c.NCiclo)
                    .Select(c => new
                    {
                        c.Id,
                        Nombre = $"Ciclo {c.NCiclo}/{c.anio}" + (c.Activo ? " (Activo)" : "")
                    })
                    .ToListAsync(),
                "Id",
                "Nombre",
                DesercionAlumno.CicloId);

            ViewData["CausaDesercionId"] = new SelectList(
                await _context.CausasDesercion
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(),
                "CausaDesercionId",
                "Nombre",
                DesercionAlumno.CausaDesercionId);
        }

        private async Task CargarAlumnoSeleccionadoAsync()
        {
            AlumnoSeleccionado = await _context.Alumno
                .AsNoTracking()
                .Include(a => a.Carrera)
                .FirstOrDefaultAsync(a => a.AlumnoId == DesercionAlumno.AlumnoId);
        }
    }
}
