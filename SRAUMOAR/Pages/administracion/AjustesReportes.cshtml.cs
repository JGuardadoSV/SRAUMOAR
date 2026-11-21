using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class AjustesReportesModel : PageModel
    {
        private readonly Contexto _context;

        public AjustesReportesModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public List<ConfiguracionReporteInput> Configuraciones { get; set; } = new();

        public class ConfiguracionReporteInput
        {
            public int Id { get; set; }
            public string? Reporte { get; set; }
            public string? Clave { get; set; }
            public string? Valor { get; set; }
            public string? Descripcion { get; set; }
        }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var configuraciones = await _context.ConfiguracionesReportes
                .OrderBy(c => c.Reporte)
                .ThenBy(c => c.Id)
                .ToListAsync();

            Configuraciones = configuraciones.Select(c => new ConfiguracionReporteInput
            {
                Id = c.Id,
                Reporte = c.Reporte,
                Clave = c.Clave,
                Valor = c.Valor,
                Descripcion = c.Descripcion
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "No se guardaron los ajustes porque algunos datos enviados no son válidos. Revisa los campos e inténtalo de nuevo.";
                return Page();
            }

            try
            {
                if (Configuraciones.Count == 0)
                {
                    ErrorMessage = "No se recibieron configuraciones para guardar.";
                    return Page();
                }

                var ids = Configuraciones.Select(c => c.Id).Distinct().ToArray();
                var configuracionesDb = await _context.ConfiguracionesReportes
                    .Where(c => ids.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                if (ids.Length != Configuraciones.Count || configuracionesDb.Count != ids.Length)
                {
                    ErrorMessage = "No se guardaron los ajustes porque la lista recibida no coincide con las configuraciones registradas. Recarga la página e inténtalo de nuevo.";
                    return Page();
                }

                foreach (var config in Configuraciones)
                {
                    configuracionesDb[config.Id].Valor = config.Valor ?? string.Empty;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Ajustes de reportes guardados correctamente.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"No se pudieron guardar los ajustes: {ex.GetBaseException().Message}";
                return Page();
            }
        }
    }
}
