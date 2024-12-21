using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.actividades
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<ActividadAcademica> ActividadAcademica { get;set; } = default!;

        public async Task OnGetAsync()
        {
            var cicloactual= await _context.Ciclos.Where(x => x.Activo).FirstAsync();
            ActividadAcademica = await _context.ActividadesAcademicas
                .Include(a => a.Arancel)
                .Include(a => a.Ciclo).Where(c=>c.CicloId==cicloactual.Id).ToListAsync();
        }
    }
}
