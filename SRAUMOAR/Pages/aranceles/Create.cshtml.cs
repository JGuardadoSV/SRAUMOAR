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
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet(int? cicloId = null)
        {
            CargarCiclos(cicloId);
            if (cicloId.HasValue)
            {
                Arancel = new Arancel { CicloId = cicloId.Value };
            }

            return Page();
        }

        [BindProperty]
        public Arancel Arancel { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!Arancel.Obligatorio && !Arancel.EsEspecializacion)
            {
                ModelState.Remove("Arancel.CicloId");
                ModelState.Remove("Arancel.FechaInicio");
                ModelState.Remove("Arancel.FechaFin");
                ModelState.Remove("Arancel.ValorMora");
                Arancel.CicloId = null;
                Arancel.FechaInicio = null;
                Arancel.FechaFin = null;
                Arancel.ValorMora = 0;
            }

            if (!ModelState.IsValid)
            {
                CargarCiclos(Arancel.CicloId);
                return Page();
            }

            _context.Aranceles.Add(Arancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new { cicloId = Arancel.CicloId });
        }

        private void CargarCiclos(int? selectedValue = null)
        {
            ViewData["CicloId"] = new SelectList(
                _context.Ciclos
                    .OrderByDescending(x => x.Activo)
                    .ThenByDescending(x => x.anio)
                    .ThenByDescending(x => x.NCiclo)
                    .Select(x => new
                    {
                        x.Id,
                        NombreCiclo = x.NCiclo + " - " + x.anio + (x.Activo ? " (Activo)" : " (En preparacion/Inactivo)")
                    }),
                "Id",
                "NombreCiclo",
                selectedValue
            );
        }
    }
}
