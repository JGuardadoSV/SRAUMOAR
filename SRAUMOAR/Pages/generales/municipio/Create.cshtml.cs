using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.municipio
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
            CargarDistritos();
            return Page();
        }

        [BindProperty]
        public Municipio Municipio { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CargarDistritos();
                return Page();
            }

            _context.Municipios.Add(Municipio);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void CargarDistritos()
        {
            ViewData["DistritoId"] = new SelectList(
                _context.Distritos.OrderBy(d => d.NombreDistrito),
                "DistritoId",
                "NombreDistrito",
                Municipio?.DistritoId);
        }
    }
}
