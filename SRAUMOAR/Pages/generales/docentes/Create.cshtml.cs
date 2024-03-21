using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.docentes
{
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["ProfesionId"] = new SelectList(_context.Profesiones, "ProfesionId", "NombreProfesion");
            return Page();
        }

        [BindProperty]
        public Docente Docente { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            Docente.FechaDeRegistro=DateTime.Now;
            if (!ModelState.IsValid)
            {
               ViewData["ProfesionId"] = new SelectList(_context.Profesiones, "ProfesionId", "NombreProfesion");
                return Page();
            }

            _context.Docentes.Add(Docente);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
