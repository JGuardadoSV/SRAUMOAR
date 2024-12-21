using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Becados> Becados { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Becados = await _context.Becados
                .Include(b => b.Alumno)
                .Include(b => b.Ciclo)
                .Include(b => b.EntidadBeca)
                .Where(x => x.Ciclo.Activo == true)
                .ToListAsync();
        }
    }
}
