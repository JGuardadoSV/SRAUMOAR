using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Pages.grupos
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public Grupo Grupo { get; set; } = default!;
        public IList<Alumno> AlumnosInscritos { get; set; } = new List<Alumno>();
        public IList<MateriaDelGrupo> MateriasDelGrupo { get; set; } = new List<MateriaDelGrupo>();
        public IList<ActividadAcademica> ActividadesAcademicas { get; set; } = new List<ActividadAcademica>();
        public IList<EstudianteConNotas> EstudiantesConNotas { get; set; } = new List<EstudianteConNotas>();

        public class EstudianteConNotas
        {
            public int AlumnoId { get; set; }
            public string NombreCompleto { get; set; } = string.Empty; // Apellidos Nombres
            public IList<Notas> Notas { get; set; } = new List<Notas>();
            public List<MateriasInscritas> MateriasInscritas { get; set; } = new List<MateriasInscritas>();
        }

        public class MateriaDelGrupo
        {
            public int MateriasGrupoId { get; set; }
            public string NombreMateria { get; set; } = string.Empty;
            public string CodigoMateria { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .Include(g => g.Docente)
                .FirstOrDefaultAsync(m => m.GrupoId == id);
            if (grupo == null)
            {
                return NotFound();
            }
            else
            {
                Grupo = grupo;
            }

            // Materias del grupo
            MateriasDelGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                .Where(mg => mg.GrupoId == Grupo.GrupoId)
                .Select(mg => new MateriaDelGrupo
                {
                    MateriasGrupoId = mg.MateriasGrupoId,
                    NombreMateria = mg.Materia!.NombreMateria!,
                    CodigoMateria = mg.Materia!.CodigoMateria!
                })
                .ToListAsync();

            // Actividades académicas del ciclo, ordenadas por FechaInicio
            ActividadesAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == Grupo.CicloId)
                .OrderBy(a => a.FechaInicio)
                .ToListAsync();

            // Estudiantes con notas por grupo
            var materiasInscritas = await _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.MateriasGrupo)
                .Include(mi => mi.Notas) .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupo!.GrupoId == Grupo.GrupoId)
                .ToListAsync();

            // Lista simple de alumnos inscritos (para otros usos)
            AlumnosInscritos = materiasInscritas
                .Where(mi => mi.Alumno != null)
                .Select(mi => mi.Alumno!)
                .Distinct()
                .OrderBy(a => a.Apellidos).ThenBy(a => a.Nombres)
                .ToList();

            EstudiantesConNotas = materiasInscritas
                .GroupBy(mi => mi.AlumnoId)
                .Select(g => new EstudianteConNotas
                {
                    AlumnoId = g.Key,
                    NombreCompleto = ($"{g.First().Alumno!.Apellidos} {g.First().Alumno!.Nombres}").Trim(),
                    Notas = g.SelectMany(mi => mi.Notas ?? new List<Notas>()).ToList(),
                    MateriasInscritas = g.ToList()
                })
                .OrderBy(e => e.NombreCompleto)
                .ToList();
            return Page();
        }
    }
}
