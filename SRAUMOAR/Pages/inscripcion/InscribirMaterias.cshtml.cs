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
using SRAUMOAR.Entidades.Becas;
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
        public bool YaPago { get; set; }
        public bool EstaInscrito { get; set; }

        [BindProperty]
        public List<int> MateriasSeleccionadas { get; set; } = new List<int>();

        [BindProperty]
        public int GrupoSeleccionado { get; set; } 

        [BindProperty]
        public MateriasInscritas MateriasInscritas { get; set; } = default!;

        public IActionResult OnGet(int id, int? GrupoSeleccionado, int cicloelegido = 0)
        {
            Alumno = _context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();
            
            // Obtener ciclo actual
            var cicloActualObj = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault();
            if (cicloActualObj == null)
            {
                return BadRequest("No hay un ciclo activo");
            }

            // Determinar qué ciclo usar: el elegido si se proporciona y existe, sino el actual
            int cicloSeleccionado = cicloActualObj.Id;
            if (cicloelegido > 0)
            {
                // Verificar que el ciclo elegido existe en la base de datos
                var cicloExiste = _context.Ciclos.Any(c => c.Id == cicloelegido);
                if (cicloExiste)
                {
                    cicloSeleccionado = cicloelegido;
                }
            }

            var becado = _context.Becados.Where(x => x.AlumnoId == id).FirstOrDefault();
            
            // Verificar si el alumno está inscrito en el ciclo seleccionado
            EstaInscrito = _context.Inscripciones
                .Any(i => i.AlumnoId == id && i.CicloId == cicloSeleccionado);

            // Verificar si el alumno pagó "Matricula" (comportamiento normal) en el ciclo seleccionado
            bool pagoMatricula = _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .Any(x => x.CicloId == cicloSeleccionado && 
                         x.AlumnoId == id &&
                         x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && !d.Arancel.EsEspecializacion));

            // Verificar si el alumno pagó "Matricula" de especialización en el ciclo seleccionado
            bool pagoMatriculaEspecializacion = _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .Any(x => x.CicloId == cicloSeleccionado && 
                         x.AlumnoId == id &&
                         x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && d.Arancel.EsEspecializacion));

            // El alumno puede inscribirse si pagó Matricula normal O Matricula de especialización
            YaPago = pagoMatricula || pagoMatriculaEspecializacion;

            if (Alumno.PermiteInscripcionSinPago || becado != null)
            {
                YaPago = true;
            }

            // Si no está inscrito, redirigir a Create con el ciclo seleccionado
            if (!EstaInscrito)
            {
                return RedirectToPage("./Create", new { id = id, cicloelegido = cicloSeleccionado });
            }

            if (!YaPago)
            {
                // No redirigir, solo mostrar mensaje en la vista
                ModelState.AddModelError(string.Empty, "El alumno no ha cancelado el arancel correspondiente para realizar este proceso.");
            }
            
            ViewData["AlumnoId"] = new SelectList(_context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == id)
                .Select(a => new { AlumnoId = a.AlumnoId, Nombre = a.Nombres + " " + a.Apellidos })
                , "AlumnoId", "Nombre");

            // Guardar el ciclo seleccionado en ViewData para usarlo en el Post
            ViewData["CicloSeleccionado"] = cicloSeleccionado;
            
            // Filtrar materias solo por el grupo seleccionado si existe
            if (GrupoSeleccionado.HasValue && GrupoSeleccionado.Value > 0)
            {
                ViewData["MateriasGrupoId"] = new SelectList(
                    _context.MateriasGrupo
                        .Include(mg => mg.Materia)
                        .Include(mg => mg.Grupo)
                        .Where(mg => mg.Grupo.GrupoId == GrupoSeleccionado.Value && mg.Grupo.CicloId == cicloSeleccionado)
                        .Select(mg => new
                        {
                            MateriasGrupoId = mg.MateriasGrupoId,
                            NombreCompleto = $"{mg.Materia.NombreMateria} - {mg.Grupo.Nombre}"
                        })
                        .ToList(),
                    "MateriasGrupoId",
                    "NombreCompleto"
                );
                this.GrupoSeleccionado = GrupoSeleccionado.Value;
            }
            else
            {
                ViewData["MateriasGrupoId"] = new SelectList(new List<object>(), "MateriasGrupoId", "NombreCompleto");
            }

            ViewData["GrupoId"] = new SelectList(
               _context.Grupo
                   .Where(mg => mg.CicloId == cicloSeleccionado && mg.CarreraId == Alumno.CarreraId)
                   .Select(mg => new
                   {
                       GrupoId = mg.GrupoId,
                       Nombre = $"{mg.Nombre}"
                   })
                   .ToList(),
               "GrupoId",
               "Nombre",
               GrupoSeleccionado
           );

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int cicloelegido = 0)
        {
            var alumnoId = Request.Form["MateriasInscritas.AlumnoId"].ToString();
            if (string.IsNullOrEmpty(alumnoId))
            {
                ModelState.AddModelError(string.Empty, "El ID del alumno es requerido");
                return Page();
            }

            var alumnoIdInt = int.Parse(alumnoId);
            
            // Obtener ciclo actual
            var cicloActualObj = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault();
            if (cicloActualObj == null)
            {
                return BadRequest("No hay un ciclo activo");
            }

            // Determinar qué ciclo usar: el elegido si se proporciona y existe, sino el actual
            int cicloSeleccionado = cicloActualObj.Id;
            if (cicloelegido > 0)
            {
                var cicloExiste = _context.Ciclos.Any(c => c.Id == cicloelegido);
                if (cicloExiste)
                {
                    cicloSeleccionado = cicloelegido;
                }
            }
            
            var becado = _context.Becados.Where(x => x.AlumnoId == alumnoIdInt).FirstOrDefault();
            
            // Verificar si el alumno está inscrito en el ciclo seleccionado
            EstaInscrito = await _context.Inscripciones
                .AnyAsync(i => i.AlumnoId == alumnoIdInt && i.CicloId == cicloSeleccionado);

            if (!EstaInscrito)
            {
                ModelState.AddModelError(string.Empty, "El alumno no está inscrito en el ciclo seleccionado. Debe inscribirse primero.");
                Alumno = _context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == alumnoIdInt).FirstOrDefault() ?? new Alumno();
                YaPago = false;
                // Recargar los ViewData necesarios
                ViewData["AlumnoId"] = new SelectList(_context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == alumnoIdInt)
                    .Select(a => new { AlumnoId = a.AlumnoId, Nombre = a.Nombres + " " + a.Apellidos })
                    , "AlumnoId", "Nombre");
                ViewData["GrupoId"] = new SelectList(
                   _context.Grupo
                       .Where(mg => mg.CicloId == cicloSeleccionado && mg.CarreraId == Alumno.CarreraId)
                       .Select(mg => new
                       {
                           GrupoId = mg.GrupoId,
                           Nombre = $"{mg.Nombre}"
                       })
                       .ToList(),
                   "GrupoId",
                   "Nombre",
                   GrupoSeleccionado
               );
                ViewData["MateriasGrupoId"] = new SelectList(new List<object>(), "MateriasGrupoId", "NombreCompleto");
                return Page();
            }

            // Verificar si el alumno pagó "Matricula" en el ciclo seleccionado
            bool pagoMatricula = await _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .AnyAsync(x => x.CicloId == cicloSeleccionado && 
                             x.AlumnoId == alumnoIdInt &&
                             x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && !d.Arancel.EsEspecializacion));

            bool pagoMatriculaEspecializacion = await _context.CobrosArancel
                .Include(x => x.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .AnyAsync(x => x.CicloId == cicloSeleccionado && 
                             x.AlumnoId == alumnoIdInt &&
                             x.DetallesCobroArancel.Any(d => d.Arancel.Nombre == "Matricula" && d.Arancel.EsEspecializacion));

            YaPago = pagoMatricula || pagoMatriculaEspecializacion;
            Alumno = _context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == alumnoIdInt).FirstOrDefault() ?? new Alumno();

            if (Alumno.PermiteInscripcionSinPago || becado != null)
            {
                YaPago = true;
            }

            if (!YaPago)
            {
                ModelState.AddModelError(string.Empty, "El alumno no ha cancelado el arancel correspondiente para realizar este proceso.");
                // Recargar los ViewData necesarios
                ViewData["AlumnoId"] = new SelectList(_context.Alumno.Include(a => a.Carrera).Where(x => x.AlumnoId == alumnoIdInt)
                    .Select(a => new { AlumnoId = a.AlumnoId, Nombre = a.Nombres + " " + a.Apellidos })
                    , "AlumnoId", "Nombre");
                ViewData["GrupoId"] = new SelectList(
                   _context.Grupo
                       .Where(mg => mg.CicloId == cicloSeleccionado && mg.CarreraId == Alumno.CarreraId)
                       .Select(mg => new
                       {
                           GrupoId = mg.GrupoId,
                           Nombre = $"{mg.Nombre}"
                       })
                       .ToList(),
                   "GrupoId",
                   "Nombre",
                   GrupoSeleccionado
               );
                if (GrupoSeleccionado > 0)
                {
                    ViewData["MateriasGrupoId"] = new SelectList(
                        _context.MateriasGrupo
                            .Include(mg => mg.Materia)
                            .Include(mg => mg.Grupo)
                            .Where(mg => mg.Grupo.GrupoId == GrupoSeleccionado && mg.Grupo.CicloId == cicloSeleccionado)
                            .Select(mg => new
                            {
                                MateriasGrupoId = mg.MateriasGrupoId,
                                NombreCompleto = $"{mg.Materia.NombreMateria} - {mg.Grupo.Nombre}"
                            })
                            .ToList(),
                        "MateriasGrupoId",
                        "NombreCompleto"
                    );
                }
                else
                {
                    ViewData["MateriasGrupoId"] = new SelectList(new List<object>(), "MateriasGrupoId", "NombreCompleto");
                }
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

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
            // Redirigir a MateriasInscritas con el ciclo seleccionado
            return RedirectToPage("./MateriasInscritas", new { id = alumnoIdInt, cicloelegido = cicloSeleccionado });
        }
    }
}
