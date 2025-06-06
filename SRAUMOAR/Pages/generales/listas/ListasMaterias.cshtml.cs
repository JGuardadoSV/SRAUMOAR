using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.listas
{
    public class ListasMateriasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public ListasMateriasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasGrupo> MateriasGrupo { get;set; } = default!;

        public async Task OnGetAsync(int id)
        {
            MateriasGrupo = await _context.MateriasGrupo
                .Include(m => m.Docente)
                .Include(m => m.Grupo)
                .Include(m => m.Materia).Where(x=>x.Grupo.GrupoId==id).ToListAsync();
        }
    }
}
