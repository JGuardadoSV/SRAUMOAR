using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.distrito
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
            CargarDepartamentos();
            return Page();
        }

        [BindProperty]
        public Distrito Distrito { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CargarDepartamentos();
                return Page();
            }

            _context.Distritos.Add(Distrito);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void CargarDepartamentos()
        {
            ViewData["DepartamentoId"] = new SelectList(
                _context.Departamentos.OrderBy(d => d.NombreDepartamento),
                "DepartamentoId",
                "NombreDepartamento",
                Distrito?.DepartamentoId);
        }
    }
}
