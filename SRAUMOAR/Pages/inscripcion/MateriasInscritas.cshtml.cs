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
        public async Task OnGetAsync(int id)
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault()?? new Alumno() ;

            MateriasInscritas = await _context.MateriasInscritas
             .Include(mi => mi.MateriasGrupo)
                 .ThenInclude(mg => mg.Materia)
             .Include(mi => mi.MateriasGrupo)
                 .ThenInclude(mg => mg.Grupo)
                 .ThenInclude(mg=>mg.Docente)
             .Where(mi => mi.MateriasGrupo.Grupo.CicloId == cicloactual)
             .ToListAsync();

            //MateriasInscritas = await _context.MateriasInscritas
            //    .Include(m => m.Alumno)
            //    .Include(m => m.MateriasGrupo)
            //    .Where(x => x.AlumnoId == id)
            //    .ToListAsync();
        }
    }
}
