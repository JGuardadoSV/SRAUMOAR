using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.historial.EstudiosEquivalencia
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class VerModel : PageModel
    {
        private readonly Contexto _context;

        public VerModel(Contexto context)
        {
            _context = context;
        }

        public EstudioEquivalencia Estudio { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var estudio = await _context.EstudiosEquivalencia
                .Include(e => e.Alumno)
                .Include(e => e.Detalles!)
                    .ThenInclude(d => d.MateriaDestino)
                .Include(e => e.Detalles!)
                    .ThenInclude(d => d.MateriasOrigen)
                .FirstOrDefaultAsync(e => e.EstudioEquivalenciaId == id.Value);

            if (estudio == null)
            {
                return NotFound();
            }

            Estudio = estudio;
            return Page();
        }

        public async Task<IActionResult> OnPostAprobarAsync(int id)
        {
            var estudio = await _context.EstudiosEquivalencia
                .Include(e => e.Detalles)
                .FirstOrDefaultAsync(e => e.EstudioEquivalenciaId == id);

            if (estudio == null)
            {
                return NotFound();
            }

            if (estudio.Estado != "Borrador")
            {
                TempData["ErrorMessage"] = "Solo se pueden aprobar estudios en estado Borrador.";
                return RedirectToPage(new { id = id });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Obtener el alumno y su carrera
                var alumno = await _context.Alumno.FindAsync(estudio.AlumnoId);
                if (alumno == null)
                {
                    throw new InvalidOperationException("El alumno asociado no existe.");
                }

                // 2. Buscar o crear HistorialAcademico para este alumno y su carrera
                var historial = await _context.HistorialAcademico
                    .FirstOrDefaultAsync(h => h.AlumnoId == estudio.AlumnoId && h.CarreraId == alumno.CarreraId);

                if (historial == null)
                {
                    historial = new HistorialAcademico
                    {
                        AlumnoId = estudio.AlumnoId,
                        CarreraId = alumno.CarreraId,
                        FechaRegistro = DateTime.Now
                    };
                    _context.HistorialAcademico.Add(historial);
                    await _context.SaveChangesAsync(); // Generar ID
                }

                // 3. Buscar o crear HistorialCiclo con texto "EQUIV"
                var ciclo = await _context.HistorialCiclo
                    .FirstOrDefaultAsync(c => c.HistorialAcademicoId == historial.HistorialAcademicoId && c.CicloTexto == "EQUIV");

                bool esInterno = estudio.UniversidadOrigen != null && 
                                 (estudio.UniversidadOrigen.Contains("MONSEÑOR OSCAR ARNULFO ROMERO", StringComparison.OrdinalIgnoreCase) || 
                                  estudio.UniversidadOrigen.Contains("UMOAR", StringComparison.OrdinalIgnoreCase));

                if (ciclo == null)
                {
                    ciclo = new HistorialCiclo
                    {
                        HistorialAcademicoId = historial.HistorialAcademicoId,
                        CicloTexto = "EQUIV",
                        PensumNombreLibre = esInterno ? "Equivalencia Interna" : "Equivalencia Externa",
                        PensumId = null,
                        FechaRegistro = DateTime.Now
                    };
                    _context.HistorialCiclo.Add(ciclo);
                    await _context.SaveChangesAsync(); // Generar ID
                }
                else if (esInterno && (ciclo.PensumNombreLibre == "Equivalencia Externa" || string.IsNullOrEmpty(ciclo.PensumNombreLibre)))
                {
                    // Si ya existía el ciclo pero tiene descripción de externa o vacía, la actualizamos a interna o mixta
                    ciclo.PensumNombreLibre = "Equivalencia Interna";
                    _context.HistorialCiclo.Update(ciclo);
                }

                // 4. Agregar cada materia homologada al historial
                foreach (var det in estudio.Detalles ?? new List<DetalleEquivalencia>())
                {
                    // Evitar duplicar la misma materia en el ciclo de equivalencias
                    bool yaExiste = await _context.HistorialMateria
                        .AnyAsync(hm => hm.HistorialCicloId == ciclo.HistorialCicloId && hm.MateriaId == det.MateriaDestinoId);

                    if (!yaExiste)
                    {
                        var historialMateria = new HistorialMateria
                        {
                            HistorialCicloId = ciclo.HistorialCicloId,
                            MateriaId = det.MateriaDestinoId,
                            Promedio = det.NotaEquivalencia,
                            Aprobada = true,
                            Equivalencia = true,
                            EsEquivalenciaInterna = esInterno,
                            ExamenSuficiencia = false,
                            FechaRegistro = DateTime.Now,
                            // Inicializar notas con la nota de equivalencia
                            Nota1 = det.NotaEquivalencia,
                            Nota2 = det.NotaEquivalencia,
                            Nota3 = det.NotaEquivalencia,
                            Nota4 = det.NotaEquivalencia,
                            Nota5 = det.NotaEquivalencia,
                            Nota6 = det.NotaEquivalencia
                        };
                        _context.HistorialMateria.Add(historialMateria);
                    }
                }

                // 5. Actualizar estado del estudio
                estudio.Estado = "Aprobado";
                estudio.FechaAprobacion = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Estudio aprobado y materias registradas en el historial académico correctamente.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Ocurrió un error al aprobar el estudio: " + ex.Message;
            }

            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostRechazarAsync(int id)
        {
            var estudio = await _context.EstudiosEquivalencia.FindAsync(id);
            if (estudio == null)
            {
                return NotFound();
            }

            if (estudio.Estado != "Borrador")
            {
                TempData["ErrorMessage"] = "Solo se pueden rechazar estudios en estado Borrador.";
                return RedirectToPage(new { id = id });
            }

            estudio.Estado = "Rechazado";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "El estudio de equivalencia ha sido rechazado.";
            return RedirectToPage(new { id = id });
        }
    }
}
