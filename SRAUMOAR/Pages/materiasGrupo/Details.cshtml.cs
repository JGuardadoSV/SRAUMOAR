using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public MateriasGrupo MateriasGrupo { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var materiasgrupo = await _context.MateriasGrupo.FirstOrDefaultAsync(m => m.MateriasGrupoId == id);
            if (materiasgrupo == null)
            {
                return NotFound();
            }
            else
            {
                MateriasGrupo = materiasgrupo;
            }
            return Page();
        }
    }
}
