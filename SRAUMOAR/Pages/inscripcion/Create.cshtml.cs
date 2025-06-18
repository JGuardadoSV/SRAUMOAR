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
        public bool EstaInscrito { get; set; }
        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty(SupportsGet = true)]
        public int idalumno { get; set; }
        public IActionResult OnGet(int id, int cicloelegido=0)
        {
           idalumno = id;
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;

            EstaInscrito =  _context.Inscripciones
              .Any(i => i.AlumnoId == idalumno && i.CicloId == cicloactual);



            
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

            // Verificar si el alumno ya está inscrito en el ciclo
            bool isAlreadyEnrolled = await _context.Inscripciones
                .AnyAsync(i => i.AlumnoId == Inscripcion.AlumnoId && i.CicloId == Inscripcion.CicloId);

            if (isAlreadyEnrolled)
            {
                // Manejar el caso donde el alumno ya está inscrito
                ModelState.AddModelError(string.Empty, "El alumno ya está inscrito en este ciclo.");
                return RedirectToPage("./Index");
            }


            _context.Inscripciones.Add(Inscripcion);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
