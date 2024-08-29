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
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public int idgrupo = 0;
        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasGrupo> MateriasGrupo { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            idgrupo = id.Value;
            MateriasGrupo = await _context.MateriasGrupo.Where(x=>x.GrupoId==id)
                .Include(m => m.Grupo)
                .Include(m => m.Materia).ToListAsync();
        }
    }
}
