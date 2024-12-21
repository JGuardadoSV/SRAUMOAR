using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
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
      .Where(c => c.AlumnoId == id)
      .SelectMany(c => c.DetallesCobroArancel.DefaultIfEmpty())
      .Select(dc => dc.ArancelId)
      .ToListAsync();


            Arancel = await _context.Aranceles.Where(x => x.Ciclo.Id == cicloactual.Id)
                .Include(a => a.Ciclo).ToListAsync();


            var arancelesConEstado = Arancel.Select(a => new
            {
                Arancel = a,
                YaPago = arancelesPagados.Contains(a.ArancelId) // Verificar si el arancel está en los pagados
            }).ToList();
        }

        public IActionResult OnPost(List<int> selectedAranceles, int alumnoId)
        {
            if (selectedAranceles == null || !selectedAranceles.Any())
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar al menos un arancel.");
                return Page();
            }

            // Redirigir a la página de cobro con los IDs de los aranceles seleccionados
            return RedirectToPage("./Facturar", new { arancelIds = string.Join(",", selectedAranceles),idalumno=alumnoId });
        }


        public bool AlumnoHaPagado(int arancelId, int alumnoId) {
            return _context.DetallesCobrosArancel
               .Any(d => d.ArancelId == arancelId &&
                         d.CobroArancel.AlumnoId == alumnoId);

        }

    }
}
