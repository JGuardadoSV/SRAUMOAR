using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Arancel Arancel { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var arancel = await _context.Aranceles.FirstOrDefaultAsync(m => m.ArancelId == id);
            if (arancel == null)
            {
                return NotFound();
            }

            Arancel = arancel;
            CargarCiclos(Arancel.CicloId);
            return Page();
        }

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

            var arancelExistente = await _context.Aranceles.FindAsync(Arancel.ArancelId);
            if (arancelExistente == null)
            {
                return NotFound();
            }

            arancelExistente.Nombre = Arancel.Nombre;
            arancelExistente.Costo = Arancel.Costo;
            arancelExistente.Exento = Arancel.Exento;
            arancelExistente.Activo = Arancel.Activo;
            arancelExistente.Obligatorio = Arancel.Obligatorio;
            arancelExistente.EsEspecializacion = Arancel.EsEspecializacion;
            arancelExistente.FechaInicio = Arancel.FechaInicio;
            arancelExistente.FechaFin = Arancel.FechaFin;
            arancelExistente.ValorMora = Arancel.ValorMora;
            arancelExistente.CicloId = Arancel.CicloId;

            _context.Attach(arancelExistente).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArancelExists(Arancel.ArancelId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToPage("./Index", new { cicloId = Arancel.CicloId });
        }

        private bool ArancelExists(int id)
        {
            return _context.Aranceles.Any(e => e.ArancelId == id);
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
