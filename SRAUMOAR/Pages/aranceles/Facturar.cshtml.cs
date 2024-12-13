using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    public class FacturarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public FacturarModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGet(int alumnoId, int arancelId)
        {
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == alumnoId);
            var arancel = await _context.Aranceles.Include(a => a.Ciclo).FirstOrDefaultAsync(a => a.ArancelId == arancelId);
            //var ciclo = await _context.Ciclos.FirstOrDefaultAsync(c => c.Id == cicloId);

            ViewData["Alumno"] = alumno;
            ViewData["AlumnoNombre"] = alumno.Nombres + " " + alumno.Apellidos;
            ViewData["AlumnoId"] = alumno.AlumnoId;
            ViewData["Arancel"] = arancel;
            //ViewData["Ciclo"] = ciclo;


            return Page();
        }

        [BindProperty]
        public CobroArancel CobroArancel { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            CobroArancel.Fecha = DateTime.Now;
            _context.CobrosArancel.Add(CobroArancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Facturas");
        }
    }
}
