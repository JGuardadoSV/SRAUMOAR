using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.ciclos
{
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public bool HayCicloActivo { get; set; }
        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            HayCicloActivo = _context.Ciclos.Any(x => x.Activo);

            return Page();
        }

       

        [BindProperty]
        public Ciclo Ciclo { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            var hayCicloActivo = await _context.Ciclos.AnyAsync(x => x.Activo);
            if (hayCicloActivo)
            {
                Ciclo.Activo = false;
            }

            if (!ModelState.IsValid)
            {
                HayCicloActivo = hayCicloActivo;
                return Page();
            }

            _context.Ciclos.Add(Ciclo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
