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

            var arancel =  await _context.Aranceles.FirstOrDefaultAsync(m => m.ArancelId == id);
            if (arancel == null)
            {
                return NotFound();
            }
            Arancel = arancel;
           ViewData["CicloId"] = new SelectList(
    _context.Ciclos
        .Where(x => x.Activo == true)
        .Select(x => new { x.Id, NombreCiclo = x.NCiclo + " - " + x.anio }),
    "Id",
    "NombreCiclo"
);
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Validación condicional: si no es obligatorio NI especialización, los campos de ciclo y fechas son opcionales
            if (!Arancel.Obligatorio && !Arancel.EsEspecializacion)
            {
                // Remover errores de validación para campos opcionales cuando no es obligatorio ni especialización
                ModelState.Remove("Arancel.CicloId");
                ModelState.Remove("Arancel.FechaInicio");
                ModelState.Remove("Arancel.FechaFin");
                ModelState.Remove("Arancel.ValorMora");
                // Establecer valores null para campos opcionales
                Arancel.CicloId = null;
                Arancel.FechaInicio = null;
                Arancel.FechaFin = null;
                Arancel.ValorMora = 0;
            }

            if (!ModelState.IsValid)
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

            // Obtener el arancel existente de la base de datos
            var arancelExistente = await _context.Aranceles.FindAsync(Arancel.ArancelId);
            if (arancelExistente == null)
            {
                return NotFound();
            }

            // Actualizar solo las propiedades que se pueden editar
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
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ArancelExists(int id)
        {
            return _context.Aranceles.Any(e => e.ArancelId == id);
        }
    }
}

