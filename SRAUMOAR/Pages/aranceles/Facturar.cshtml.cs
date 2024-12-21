using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class FacturarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public FacturarModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public List<Arancel> Aranceles { get; set; }
        //public async Task<IActionResult> OnGet(int alumnoId, int arancelId)
        public async Task<IActionResult> OnGetAsync(string arancelIds,int idalumno)
        {

            var  cicloId = await _context.Ciclos.Where(x => x.Activo==true).FirstAsync();
            var arancelIdsList = arancelIds.Split(',').Select(int.Parse).ToList();
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            var aranceles = await _context.Aranceles.Include(a => a.Ciclo).Where(a=>arancelIdsList.Contains(a.ArancelId)).ToListAsync();
            var ciclo = await _context.Ciclos.FirstOrDefaultAsync(c => c.Id == cicloId.Id);

            ViewData["Alumno"] = alumno;
            ViewData["AlumnoNombre"] = alumno.Nombres + " " + alumno.Apellidos;
            ViewData["AlumnoId"] = alumno.AlumnoId;
            Aranceles = aranceles;
            ViewData["Ciclo"] = ciclo;


            return Page();
        }

        [BindProperty]
        public CobroArancel CobroArancel { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync(List<int> selectedAranceles, List<decimal> arancelescostos)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            List<DetallesCobroArancel> aranceles = new List<DetallesCobroArancel>();

           for (int i = 0; i < selectedAranceles.Count; i++)
            {
                DetallesCobroArancel arancel = new DetallesCobroArancel();
                arancel.ArancelId = selectedAranceles[i];
                arancel.costo = arancelescostos[i];
                aranceles.Add(arancel);
            }
            CobroArancel.DetallesCobroArancel = aranceles;

            CobroArancel.Fecha = DateTime.Now;
            _context.CobrosArancel.Add(CobroArancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Facturas");
        }
    }
}
