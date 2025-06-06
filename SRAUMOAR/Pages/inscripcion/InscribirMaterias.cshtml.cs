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

        [BindProperty]
        public List<int> MateriasSeleccionadas { get; set; } = new List<int>();

        [BindProperty]
        public MateriasInscritas MateriasInscritas { get; set; } = default!;

        public IActionResult OnGet(int id)
        {
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();
            ViewData["AlumnoId"] = new SelectList(_context.Alumno.Where(x => x.AlumnoId == id)
                .Select(a => new { AlumnoId = a.AlumnoId, Nombre = a.Nombres + " " + a.Apellidos })
                , "AlumnoId", "Nombre");

            var cicloActual = _context.Ciclos.FirstOrDefault(c => c.Activo).Id;

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

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var alumnoId = Request.Form["MateriasInscritas.AlumnoId"].ToString();
            if (string.IsNullOrEmpty(alumnoId))
            {
                ModelState.AddModelError(string.Empty, "El ID del alumno es requerido");
                return Page();
            }

            var alumnoIdInt = int.Parse(alumnoId);
            var fechaInscripcion = DateTime.Now;

            foreach (var materiaGrupoId in MateriasSeleccionadas)
            {
                // Verificar si ya existe la inscripción
                bool existeInscripcion = await _context.MateriasInscritas
                    .AnyAsync(mi => mi.AlumnoId == alumnoIdInt && mi.MateriasGrupoId == materiaGrupoId);

                if (!existeInscripcion)
                {
                    var nuevaInscripcion = new MateriasInscritas
                    {
                        AlumnoId = alumnoIdInt,
                        MateriasGrupoId = materiaGrupoId,
                        FechaInscripcion = fechaInscripcion,
                        NotaPromedio = 0,
                        Aprobada = false
                    };

                    _context.MateriasInscritas.Add(nuevaInscripcion);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./MateriasInscritas", new { id = alumnoIdInt });
        }
    }
}
