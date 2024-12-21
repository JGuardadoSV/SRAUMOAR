using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.inscripcion
{
    public class MateriasInscritasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public MateriasInscritasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasInscritas> MateriasInscritas { get;set; } = default!;
        public Alumno Alumno { get; set; }
        public IActionResult OnGet(int id)
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault()?? new Alumno() ;

                    MateriasInscritas = _context.MateriasInscritas
             .Include(mi => mi.MateriasGrupo)
                 .ThenInclude(mg => mg.Materia)
             .Include(mi => mi.MateriasGrupo)
                 .ThenInclude(mg => mg.Docente)
             .Include(mi => mi.MateriasGrupo)
                 .ThenInclude(mg => mg.Grupo)  // Agregamos esta línea para incluir el Grupo
             .Where(mi => mi.MateriasGrupo.Grupo.CicloId == cicloactual &&
                          mi.Alumno.AlumnoId == Alumno.AlumnoId)
             .ToList();


            return Page();
            //MateriasInscritas = await _context.MateriasInscritas
            //    .Include(m => m.Alumno)
            //    .Include(m => m.MateriasGrupo)
            //    .Where(x => x.AlumnoId == id)
            //    .ToListAsync();
        }
    }
}
