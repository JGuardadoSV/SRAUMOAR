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

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Arancel> ArancelesObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesNoObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesEspecializacion { get; set; } = default!;
        public IList<Arancel> Arancel { get; set; } = default!; // Mantener para compatibilidad

        public async Task OnGetAsync()
        {
            Ciclo cicloactual = await _context.Ciclos.Where(x => x.Activo).FirstAsync();
            var todosLosAranceles = await _context.Aranceles
                .Where(x => (x.Ciclo != null && x.Ciclo.Id == cicloactual.Id) || (!x.Obligatorio && x.Ciclo == null))
                .Include(a => a.Ciclo).ToListAsync();

            // Separar aranceles por tipo
            // Aranceles obligatorios normales (no de especialización)
            ArancelesObligatorios = todosLosAranceles.Where(a => a.Obligatorio && !a.EsEspecializacion).ToList();
            // Aranceles no obligatorios normales (no de especialización)
            ArancelesNoObligatorios = todosLosAranceles.Where(a => !a.Obligatorio && !a.EsEspecializacion).ToList();
            // Aranceles de especialización (obligatorios o no obligatorios)
            ArancelesEspecializacion = todosLosAranceles.Where(a => a.EsEspecializacion).ToList();
            
            // Mantener la lista completa para compatibilidad
            Arancel = todosLosAranceles;
        }
    }
}
