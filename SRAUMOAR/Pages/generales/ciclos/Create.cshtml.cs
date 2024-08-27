using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.ciclos
{
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public Boolean sePuedeRegistrar { get; set; }
        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            int cantidad=_context.Ciclos.Where(x=>x.Activo==true).Count();
            sePuedeRegistrar = (cantidad == 0);

            return Page();
        }

       

        [BindProperty]
        public Ciclo Ciclo { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Ciclos.Add(Ciclo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
