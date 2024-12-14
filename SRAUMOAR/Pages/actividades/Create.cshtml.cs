using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.actividades
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["ArancelId"] = new SelectList(_context.Aranceles, "ArancelId", "Nombre");
            ViewData["CicloId"] = new SelectList(
        _context.Ciclos.Where(c => c.Activo==true).Select(c => new {
            c.Id,
            Descripcion = $"Ciclo {c.NCiclo} - {c.anio}"
        }),
        "Id",
        "Descripcion"
    );
            return Page();
        }

        [BindProperty]
        public ActividadAcademica ActividadAcademica { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            ActividadAcademica.Fecha = DateTime.Now;
            _context.ActividadesAcademicas.Add(ActividadAcademica);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
