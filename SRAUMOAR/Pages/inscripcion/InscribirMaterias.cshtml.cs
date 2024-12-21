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
    public class InscribirMateriasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public InscribirMateriasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public Alumno Alumno { get; set; }
        public IActionResult OnGet(int id)
        {
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();
        ViewData["AlumnoId"] = new SelectList(_context.Alumno.Where(x => x.AlumnoId == id)
            .Select(a => new { AlumnoId = a.AlumnoId, Nombre = a.Nombres + " " + a.Apellidos })
            , "AlumnoId", "Nombre");

            // Suponiendo que tienes un método para obtener el ciclo actual
            var cicloActual =  _context.Ciclos.FirstOrDefault(c => c.Activo).Id;

            ViewData["MateriasGrupoId"] = new SelectList(
    _context.MateriasGrupo
        .Include(mg => mg.Materia)
        .Include(mg => mg.Grupo)
        .Where(mg => mg.Grupo.CicloId == cicloActual)
        .Select(mg => new
        {
            MateriasGrupoId = mg.MateriasGrupoId,
            NombreCompleto = $"{mg.Materia.NombreMateria} - {mg.Grupo.Nombre}"
        })
        .ToList(),
    "MateriasGrupoId",
    "NombreCompleto"
);

            // Obtener materias que tienen grupos en el ciclo actual
            //ViewData["MateriasGrupoId"] = new SelectList(
            //    _context.Materias
            //        .Where(m => _context.MateriasGrupo
            //            .Any(gm => gm.MateriaId == m.MateriaId &&
            //                       gm.Grupo.CicloId == cicloActual))
            //        .ToList(),
            //    "MateriaId",
            //    "NombreMateria"
            // );
            //ViewData["MateriasGrupoId"] = new SelectList(
            //    _context.MateriasGrupo
            //        .Include(mg => mg.Grupo)
            //        .Include(mg => mg.Materia)
            //        .Where(mg => mg.Grupo.CicloId == cicloActual.Id)
            //        .ToList(),
            //    "MateriasGrupoId",
            //    "Materia.Nombre" // Asumiendo que quieres mostrar el nombre de la materia
            //);

            //ViewData["MateriasGrupoId"]= new SelectList(_context.Materias.ToList(), "MateriaId", "NombreMateria");
            return Page();
        }

        [BindProperty]
        public MateriasInscritas MateriasInscritas { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }


            bool existeInscripcion = await _context.MateriasInscritas
                .AnyAsync(mi => mi.AlumnoId == MateriasInscritas.AlumnoId && mi.MateriasGrupoId == MateriasInscritas.MateriasGrupoId);

            MateriasInscritas.FechaInscripcion = DateTime.Now;
            MateriasInscritas.NotaPromedio = 0;
            MateriasInscritas.Aprobada = false;
            if (!existeInscripcion)
            {
                _context.MateriasInscritas.Add(MateriasInscritas);
                await _context.SaveChangesAsync();
            }

            //redirigir a MateriasInscritas?id=5
            return RedirectToPage("./MateriasInscritas", new { id = MateriasInscritas.AlumnoId });

           // return RedirectToPage("./Index");
        }
    }
}
