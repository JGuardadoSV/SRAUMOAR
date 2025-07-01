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

namespace SRAUMOAR.Pages.aranceles
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
            ViewData["CicloId"] = new SelectList(
        _context.Ciclos
            .Where(x => x.Activo == true)
            .Select(x => new { x.Id, NombreCiclo = x.NCiclo + " - " + x.anio }),
        "Id",
        "NombreCiclo"
    );
            return Page();
        }

        [BindProperty]
        public Arancel Arancel { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            // Validación condicional: si no es obligatorio, los campos de ciclo y fechas son opcionales
            if (!Arancel.Obligatorio)
            {
                // Remover errores de validación para campos opcionales cuando no es obligatorio
                ModelState.Remove("Arancel.CicloId");
                ModelState.Remove("Arancel.FechaInicio");
                ModelState.Remove("Arancel.FechaFin");
                
                // Establecer valores null para campos opcionales
                Arancel.CicloId = null;
                Arancel.FechaInicio = null;
                Arancel.FechaFin = null;
            }

            if (!ModelState.IsValid)
            {
                ViewData["CicloId"] = new SelectList(_context.Ciclos.Where(x=>x.Activo==true), "Id", "NCiclo");
                return Page();
            }

            _context.Aranceles.Add(Arancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
