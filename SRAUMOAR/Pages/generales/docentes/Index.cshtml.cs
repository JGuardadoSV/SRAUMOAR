using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.docentes
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Docente> Docente { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Docente = await _context.Docentes
                .Include(u=>u.Usuario)
                .Include(d => d.Profesion).ToListAsync();
        }
    }
}
