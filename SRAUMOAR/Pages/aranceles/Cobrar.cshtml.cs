using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    public class CobrarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CobrarModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Arancel> Arancel { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {

            ViewData["Alumno"] = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);

            Ciclo cicloactual=await _context.Ciclos.Where(x=>x.Activo).FirstAsync();

            // Obtener los IDs de los aranceles que ya pagó el alumno
            var arancelesPagados = await _context.CobrosArancel
            .Where(c => c.AlumnoId == id) // Asegúrate de tener el `alumnoId`
                .Select(c => c.ArancelId)
                .ToListAsync();

            Arancel = await _context.Aranceles.Where(x => x.Ciclo.Id == cicloactual.Id)
                .Include(a => a.Ciclo).ToListAsync();


            var arancelesConEstado = Arancel.Select(a => new
            {
                Arancel = a,
                YaPago = arancelesPagados.Contains(a.ArancelId) // Verificar si el arancel está en los pagados
            }).ToList();
        }

        public bool AlumnoHaPagado(int arancelId, int alumnoId) { 
            return _context.CobrosArancel.Any(c => c.ArancelId == arancelId && c.AlumnoId == alumnoId); 
        }

    }
}
