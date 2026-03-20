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
    public class CreateModel : PageModel
    {
        private readonly Contexto _context;

        public CreateModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public DesercionAlumno DesercionAlumno { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Buscar { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? AlumnoId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CicloId { get; set; }

        public Alumno? AlumnoSeleccionado { get; set; }
        public List<Alumno> ResultadosBusqueda { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            CicloId ??= await ObtenerCicloPorDefectoAsync();
            DesercionAlumno.CicloId = CicloId ?? 0;
            await CargarListasAsync();

            if (AlumnoId.HasValue && AlumnoId.Value > 0)
            {
                AlumnoSeleccionado = await _context.Alumno
                    .Include(a => a.Carrera)
                    .FirstOrDefaultAsync(a => a.AlumnoId == AlumnoId.Value);

                if (AlumnoSeleccionado != null)
                {
                    DesercionAlumno.AlumnoId = AlumnoSeleccionado.AlumnoId;
                }

                if (DesercionAlumno.CicloId > 0 && DesercionAlumno.AlumnoId > 0)
                {
                    var existente = await _context.DesercionesAlumno
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.AlumnoId == DesercionAlumno.AlumnoId && d.CicloId == DesercionAlumno.CicloId);

                    if (existente != null)
                    {
                        TempData["Info"] = "Ya existe un registro para ese alumno en el ciclo seleccionado. Puedes editarlo.";
                        return RedirectToPage("./Edit", new { id = existente.DesercionAlumnoId });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(Buscar))
            {
                var termino = Buscar.Trim();
                ResultadosBusqueda = await _context.Alumno
                    .AsNoTracking()
                    .Include(a => a.Carrera)
                    .Where(a =>
                        (a.Nombres != null && a.Nombres.Contains(termino)) ||
                        (a.Apellidos != null && a.Apellidos.Contains(termino)) ||
                        (a.Carnet != null && a.Carnet.Contains(termino)) ||
                        (a.Email != null && a.Email.Contains(termino)) ||
                        (a.TelefonoPrimario != null && a.TelefonoPrimario.Contains(termino)))
                    .OrderBy(a => a.Apellidos)
                    .ThenBy(a => a.Nombres)
                    .Take(20)
                    .ToListAsync();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await CargarListasAsync();

            if (!ModelState.IsValid)
            {
                await CargarAlumnoSeleccionadoAsync();
                return Page();
            }

            var duplicado = await _context.DesercionesAlumno
                .AnyAsync(d => d.AlumnoId == DesercionAlumno.AlumnoId && d.CicloId == DesercionAlumno.CicloId);

            if (duplicado)
            {
                var existente = await _context.DesercionesAlumno
                    .AsNoTracking()
                    .FirstAsync(d => d.AlumnoId == DesercionAlumno.AlumnoId && d.CicloId == DesercionAlumno.CicloId);
                TempData["Info"] = "Ya existe un registro para ese alumno en el ciclo seleccionado. Puedes editarlo.";
                return RedirectToPage("./Edit", new { id = existente.DesercionAlumnoId });
            }

            DesercionAlumno.FechaRegistro = DateTime.Now;
            _context.DesercionesAlumno.Add(DesercionAlumno);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Deserción o retiro registrado correctamente.";
            return RedirectToPage("./Index", new { SelectedCicloId = DesercionAlumno.CicloId });
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

        private async Task<int?> ObtenerCicloPorDefectoAsync()
        {
            return await _context.Ciclos
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync()
                ?? await _context.Ciclos
                    .AsNoTracking()
                    .OrderByDescending(c => c.anio)
                    .ThenByDescending(c => c.NCiclo)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();
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
