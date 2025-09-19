using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Administracion;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class GestionarNotasDuplicadasModel : PageModel
    {
        private readonly Contexto _context;

        public GestionarNotasDuplicadasModel(Contexto context)
        {
            _context = context;
        }

        public IList<NotaDuplicada> NotasDuplicadas { get; set; } = new List<NotaDuplicada>();
        public int TotalDuplicados { get; set; }
        public int TotalRegistros { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarNotasDuplicadas();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarNotaAsync(int notasId)
        {
            try
            {
                var nota = await _context.Notas.FindAsync(notasId);
                if (nota == null)
                {
                    TempData["ErrorMessage"] = "La nota no fue encontrada.";
                    return RedirectToPage();
                }

                // Obtener información de la nota antes de eliminar para el mensaje
                var infoNota = await _context.Notas
                    .Include(n => n.MateriasInscritas)
                        .ThenInclude(mi => mi.Alumno)
                    .Include(n => n.ActividadAcademica)
                    .FirstOrDefaultAsync(n => n.NotasId == notasId);

                _context.Notas.Remove(nota);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Nota eliminada exitosamente. Alumno: {infoNota?.MateriasInscritas?.Alumno?.Nombres} {infoNota?.MateriasInscritas?.Alumno?.Apellidos}, Actividad: {infoNota?.ActividadAcademica?.Nombre}";
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al eliminar la nota: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostEliminarDuplicadosAsync(int materiasInscritasId, int actividadAcademicaId)
        {
            try
            {
                // Obtener todas las notas duplicadas para esta combinación
                var notasDuplicadas = await _context.Notas
                    .Where(n => n.MateriasInscritasId == materiasInscritasId && 
                               n.ActividadAcademicaId == actividadAcademicaId)
                    .OrderByDescending(n => n.FechaRegistro)
                    .ToListAsync();

                if (notasDuplicadas.Count <= 1)
                {
                    TempData["ErrorMessage"] = "No hay duplicados para eliminar.";
                    return RedirectToPage();
                }

                // Mantener solo la nota más reciente
                var notaMasReciente = notasDuplicadas.First();
                var duplicados = notasDuplicadas.Skip(1);

                _context.Notas.RemoveRange(duplicados);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Se eliminaron {duplicados.Count()} notas duplicadas. Se mantuvo la nota más reciente.";
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al eliminar duplicados: {ex.Message}";
                return RedirectToPage();
            }
        }

        private async Task CargarNotasDuplicadas()
        {
            // Consulta SQL para obtener notas duplicadas
            var query = @"
                SELECT 
    n.NotasId,
    n.MateriasInscritasId,
    n.ActividadAcademicaId,
    n.Nota,
    n.FechaRegistro,
    a.Nombres + ' ' + a.Apellidos as NombreAlumno,
    aa.Nombre as NombreActividad,
    aa.TipoActividad,
    gm.MateriaId,
    m.NombreMateria,
    g.Nombre as NombreGrupo,
    COUNT(*) OVER (PARTITION BY n.MateriasInscritasId, n.ActividadAcademicaId) as CantidadDuplicados,
    CASE 
        WHEN n.FechaRegistro = MAX(n.FechaRegistro) OVER (PARTITION BY n.MateriasInscritasId, n.ActividadAcademicaId) 
        THEN 1 ELSE 0 
    END as EsMasReciente
FROM Notas n
INNER JOIN MateriasInscritas mi ON n.MateriasInscritasId = mi.MateriasInscritasId
INNER JOIN Alumno a ON mi.AlumnoId = a.AlumnoId
INNER JOIN ActividadesAcademicas aa ON n.ActividadAcademicaId = aa.ActividadAcademicaId
INNER JOIN GruposMaterias gm ON mi.MateriasGrupoId = gm.MateriasGrupoId
INNER JOIN Materias m ON gm.MateriaId = m.MateriaId
INNER JOIN grupos g ON gm.GrupoId = g.GrupoId
WHERE EXISTS (
    SELECT 1 
    FROM Notas n2 
    WHERE n2.MateriasInscritasId = n.MateriasInscritasId 
    AND n2.ActividadAcademicaId = n.ActividadAcademicaId 
    AND n2.NotasId != n.NotasId
)
ORDER BY n.MateriasInscritasId, n.ActividadAcademicaId, n.FechaRegistro DESC;";

            var notas = await _context.Database.SqlQueryRaw<NotaDuplicada>(query).ToListAsync();
            
            NotasDuplicadas = notas;
            TotalDuplicados = notas.GroupBy(n => new { n.MateriasInscritasId, n.ActividadAcademicaId }).Count();
            TotalRegistros = notas.Count;
        }
    }
}
