using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.inscripcion
{
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public bool YaPago { get; set; }
        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty(SupportsGet = true)]
        public int idalumno { get; set; }
        public IActionResult OnGet(int id)
        {
           idalumno = id;

            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            YaPago = _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                .Any(x => x.CicloId == cicloactual && x.DetallesCobroArancel.FirstOrDefault().Arancel.Nombre == "Matricula" && x.AlumnoId == idalumno);


            var carreraid = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault()?.CarreraId??0;

            ViewData["AlumnoId"] = new SelectList(
                _context.Alumno.Where(x=>x.AlumnoId==id)
                .Select(a => new {
                    AlumnoId = a.AlumnoId, 
                    Nombres = a.Nombres + " " + a.Apellidos})
                , "AlumnoId", "Nombres");
            ViewData["CicloId"] = new SelectList(
                _context.Ciclos
                .Where(x => x.Activo == true)
                .Select(c => new {
                    Id = c.Id,
                    Nombre = c.NCiclo+" - "+c.anio
                })
                , "Id", "Nombre");
            //ViewData["GrupoId"] = new SelectList(
            //  _context.Grupo
            //  .Where(x => x.CarreraId == carreraid)
            //  .Include(c => c.Carrera)
            //  .Select(c => new
            //  {
            //      Id = c.GrupoId,
            //      Grupo = c.Carrera.NombreCarrera + " - " + c.Nombre
            //  }), "Id", "Grupo");
            return Page();
        }

        [BindProperty]
        public Inscripcion Inscripcion { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Inscripciones.Add(Inscripcion);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
