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
        public List<ConfiguracionReporte> Configuraciones { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Configuraciones = await _context.ConfiguracionesReportes
                .OrderBy(c => c.Reporte)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            foreach (var config in Configuraciones)
            {
                var dbConfig = await _context.ConfiguracionesReportes.FindAsync(config.Id);
                if (dbConfig != null)
                {
                    dbConfig.Valor = config.Valor ?? "";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ajustes de reportes guardados correctamente.";

            return RedirectToPage();
        }
    }
}
