using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Becados Becados { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var becados =  await _context.Becados.FirstOrDefaultAsync(m => m.BecadosId == id);
            if (becados == null)
            {
                return NotFound();
            }
            Becados = becados;
           ViewData["AlumnoId"] = new SelectList(_context.Alumno, "AlumnoId", "Apellidos");
           ViewData["CicloId"] = new SelectList(_context.Ciclos, "Id", "Id");
           ViewData["EntidadBecaId"] = new SelectList(_context.InstitucionesBeca, "EntidadBecaId", "Email");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(Becados).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BecadosExists(Becados.BecadosId))
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

        private bool BecadosExists(int id)
        {
            return _context.Becados.Any(e => e.BecadosId == id);
        }
    }
}
