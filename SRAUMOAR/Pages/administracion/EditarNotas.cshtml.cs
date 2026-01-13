using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            public string NombreDocente { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(int cicloelegido = 0)
        {
            // Obtener ciclo actual
            var cicloActual = await _context.Ciclos
                .Where(x => x.Activo == true)
                .FirstOrDefaultAsync();

            // Determinar qué ciclo usar: el elegido si se proporciona y existe, sino el actual
            int cicloSeleccionado = cicloActual?.Id ?? 0;
            if (cicloelegido > 0)
            {
                // Verificar que el ciclo elegido existe en la base de datos
                var cicloExiste = await _context.Ciclos.AnyAsync(c => c.Id == cicloelegido);
                if (cicloExiste)
                {
                    cicloSeleccionado = cicloelegido;
                }
            }

            // Cargar SelectList de ciclos para la vista
            ViewData["CicloId"] = new SelectList(
                _context.Ciclos
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => new {
                    Id = c.Id,
                    Nombre = c.NCiclo+" - "+c.anio
                })
                , "Id", "Nombre", cicloSeleccionado);

            if (cicloSeleccionado == 0)
            {
                GruposConMaterias = new List<GrupoConMaterias>();
                return;
            }

            GruposConMaterias = await _context.MateriasGrupo
                .Include(mg => mg.Grupo)
                    .ThenInclude(g => g.Carrera)
                .Include(mg => mg.Grupo)
                    .ThenInclude(g => g.Docente)
                .Include(mg => mg.Materia)
                .Where(mg => mg.Grupo.CicloId == cicloSeleccionado)
                .GroupBy(mg => new { 
                    mg.Grupo.GrupoId, 
                    mg.Grupo.Nombre, 
                    mg.Grupo.Carrera.NombreCarrera,
                    DocenteNombres = mg.Grupo.Docente != null ? mg.Grupo.Docente.Nombres : "",
                    DocenteApellidos = mg.Grupo.Docente != null ? mg.Grupo.Docente.Apellidos : ""
                })
                .Select(g => new GrupoConMaterias
                {
                    GrupoId = g.Key.GrupoId,
                    NombreGrupo = g.Key.Nombre,
                    NombreCarrera = g.Key.NombreCarrera,
                    NombreDocente = !string.IsNullOrEmpty(g.Key.DocenteNombres) 
                        ? $"{g.Key.DocenteNombres} {g.Key.DocenteApellidos}".Trim()
                        : "Sin asignar",
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
