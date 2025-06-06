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
    public class ListasDeAsistenciaModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public ListasDeAsistenciaModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Grupo> Grupo { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .Include(g => g.Docente).ToListAsync();
        }
    }
}
