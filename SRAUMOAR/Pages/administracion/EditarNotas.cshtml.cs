using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class EditarNotasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditarNotasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<GrupoConMaterias> GruposConMaterias { get; set; } = default!;

        public class GrupoConMaterias
        {
            public int GrupoId { get; set; }
            public string NombreGrupo { get; set; } = string.Empty;
            public string NombreCarrera { get; set; } = string.Empty;
            public int CantidadMaterias { get; set; }
            public int CantidadEstudiantes { get; set; }
        }

        public async Task OnGetAsync()
        {
            var cicloActual = await _context.Ciclos
                .Where(x => x.Activo == true)
                .FirstOrDefaultAsync();

            if (cicloActual == null)
            {
                GruposConMaterias = new List<GrupoConMaterias>();
                return;
            }

            GruposConMaterias = await _context.MateriasGrupo
                .Include(mg => mg.Grupo)
                    .ThenInclude(g => g.Carrera)
                .Include(mg => mg.Materia)
                .Where(mg => mg.Grupo.CicloId == cicloActual.Id)
                .GroupBy(mg => new { 
                    mg.Grupo.GrupoId, 
                    mg.Grupo.Nombre, 
                    mg.Grupo.Carrera.NombreCarrera 
                })
                .Select(g => new GrupoConMaterias
                {
                    GrupoId = g.Key.GrupoId,
                    NombreGrupo = g.Key.Nombre,
                    NombreCarrera = g.Key.NombreCarrera,
                    CantidadMaterias = g.Count(),
                    CantidadEstudiantes = _context.MateriasInscritas
                        .Where(mi => mi.MateriasGrupo.GrupoId == g.Key.GrupoId)
                        .Select(mi => mi.AlumnoId)
                        .Distinct()
                        .Count()
                })
                .OrderBy(g => g.NombreGrupo)
                .ToListAsync();
        }
    }
}
