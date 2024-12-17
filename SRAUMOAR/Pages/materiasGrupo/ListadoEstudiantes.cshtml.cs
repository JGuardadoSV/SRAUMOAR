using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
{
    public class ListadoEstudiantesModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public ListadoEstudiantesModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasInscritas> MateriasInscritas { get;set; } = default!;
        public Grupo Grupo { get; set; } = default!;
        public string NombreMateria { get; set; } = default!;
        public async Task OnGetAsync(int id)
        {
            var cicloactual= _context.Ciclos.Where(x=>x.Activo==true).First();
            NombreMateria = await ObtenerNombreMateriaAsync(id);
           
            Grupo =  await _context.MateriasInscritas
                        .Include(mi => mi.MateriasGrupo)
                            .ThenInclude(g => g.Grupo)
                            .ThenInclude(mi => mi.Carrera)
                        .Include(mi => mi.MateriasGrupo)
                            .ThenInclude(g => g.Grupo)
                            .ThenInclude(mi => mi.Docente)
                        .Where(mi => mi.MateriasGrupoId == id)
                        .Select(mi => mi.MateriasGrupo.Grupo)
                        .FirstOrDefaultAsync() ?? new Grupo(); // Proporciona un valor por defecto

            MateriasInscritas = await _context.MateriasInscritas
                .Include(m => m.Alumno)
                .Include(m => m.MateriasGrupo)
                .Where(m => m.MateriasGrupoId == id)
                .ToListAsync();
        }

        private async Task<string> ObtenerNombreMateriaAsync(int inscripcionMateriaId)
        {
            return await _context.MateriasInscritas
                .Include(im => im.MateriasGrupo)
                .Where(im => im.MateriasGrupoId == inscripcionMateriaId)
                .Select(im => im.MateriasGrupo.Materia.NombreMateria)
                .FirstOrDefaultAsync();
        }
    }
}
