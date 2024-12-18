using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.grupos
{
    public class EliminarMateriaDeGrupoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EliminarMateriaDeGrupoModel(SRAUMOAR.Modelos.Contexto context)
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

            var materiasgrupo = await _context.MateriasGrupo.FirstOrDefaultAsync(m => m.MateriasGrupoId == id);

            if (materiasgrupo is not null)
            {
                MateriasGrupo = materiasgrupo;

                return Page();
            }

            return NotFound();
        }
        
        public async Task<IActionResult> OnPostAsync(int? id)
        {
            try
            {
                if (id == null)
                    {
                        return new JsonResult(new { success = false, message = "Registro no encontrado" });
                    }

                    var materiasgrupo = await _context.MateriasGrupo.FindAsync(id);
                    if (materiasgrupo != null)
                    {
                        MateriasGrupo = materiasgrupo;
                        _context.MateriasGrupo.Remove(MateriasGrupo);
                        await _context.SaveChangesAsync();
                        return new JsonResult(new { success = true });
                    }

                    return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                // Log the exception
                return new JsonResult(new { success = false, message = "Error al eliminar el registro" });
            }
        }
    }
}
