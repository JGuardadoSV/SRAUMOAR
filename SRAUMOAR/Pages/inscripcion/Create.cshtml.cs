using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
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
            Alumno alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno(); // Obtener el alumno
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            var becado= _context.Becados.Where(x => x.AlumnoId == id).FirstOrDefault();
            EstaInscrito =  _context.Inscripciones
              .Any(i => i.AlumnoId == idalumno && i.CicloId == cicloactual);

            // Verificar si el alumno pagó "Matricula" (comportamiento normal)
            bool pagoMatricula = _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .Any(x => x.CicloId == cicloactual && 
                         x.AlumnoId == idalumno &&
                         x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && !d.Arancel.EsEspecializacion));

            // Verificar si el alumno pagó "Matricula" de especialización
            bool pagoMatriculaEspecializacion = _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .Any(x => x.CicloId == cicloactual && 
                         x.AlumnoId == idalumno &&
                         x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && d.Arancel.EsEspecializacion));

            // El alumno puede inscribirse si pagó Matricula normal O Matricula de especialización
            YaPago = pagoMatricula || pagoMatriculaEspecializacion;

            if (alumno.PermiteInscripcionSinPago || becado != null)
            {
                YaPago = true;
            }

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
