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

namespace SRAUMOAR.Pages.materiasGrupo
{
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public MateriasGrupo MateriasGrupo { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var materiasgrupo =  await _context.MateriasGrupo.FirstOrDefaultAsync(m => m.MateriasGrupoId == id);
            if (materiasgrupo == null)
            {
                return NotFound();
            }
            MateriasGrupo = materiasgrupo;
           ViewData["GrupoId"] = new SelectList(_context.Grupo, "GrupoId", "GrupoId");
           ViewData["MateriaId"] = new SelectList(_context.Materias, "MateriaId", "CodigoMateria");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(MateriasGrupo).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MateriasGrupoExists(MateriasGrupo.MateriasGrupoId))
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

        private bool MateriasGrupoExists(int id)
        {
            return _context.MateriasGrupo.Any(e => e.MateriasGrupoId == id);
        }
    }
}
