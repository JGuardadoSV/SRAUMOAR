using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using ClosedXML.Excel;

namespace SRAUMOAR.Servicios
{
    public class ReporteAlumnosService
    {
        private readonly Contexto _context;

        public ReporteAlumnosService(Contexto context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerarReporteCompletoAsync()
        {
            try
            {
                // Obtener todos los alumnos con sus relaciones
                var alumnos = await _context.Alumno
                    .Include(a => a.Carrera)
                    .OrderBy(a => a.Apellidos)
                    .ThenBy(a => a.Nombres)
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Alumnos");

                // Encabezados principales - Solo las columnas esenciales
                var headers = new[]
                {
                    "No.", "Nombres", "Apellidos", "Carrera", "Fecha Nacimiento", "Email", "DUI", 
                    "Teléfono Primario", "Dirección", "Género", "Carnet", "Estado Civil"
                };

                // Aplicar encabezados
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Datos de los alumnos
                for (int i = 0; i < alumnos.Count; i++)
                {
                    var alumno = alumnos[i];
                    var row = i + 2;

                    worksheet.Cell(row, 1).Value = i + 1; // No.
                    worksheet.Cell(row, 2).Value = alumno.Nombres ?? ""; // Nombres
                    worksheet.Cell(row, 3).Value = alumno.Apellidos ?? ""; // Apellidos
                    worksheet.Cell(row, 4).Value = alumno.Carrera?.NombreCarrera ?? ""; // Carrera
                    worksheet.Cell(row, 5).Value = alumno.FechaDeNacimiento.ToString("dd/MM/yyyy"); // Fecha Nacimiento
                    worksheet.Cell(row, 6).Value = alumno.Email ?? ""; // Email
                    worksheet.Cell(row, 7).Value = alumno.DUI ?? ""; // DUI
                    worksheet.Cell(row, 8).Value = alumno.TelefonoPrimario ?? ""; // Teléfono Primario
                    worksheet.Cell(row, 9).Value = alumno.DireccionDeResidencia ?? ""; // Dirección
                    worksheet.Cell(row, 10).Value = GetGeneroText(alumno.Genero); // Género
                    worksheet.Cell(row, 11).Value = GetCarnetFromEmail(alumno); // Carnet (antes del @ del email)
                    worksheet.Cell(row, 12).Value = GetEstadoCivilText(alumno.Casado); // Estado Civil

                    // Aplicar bordes a toda la fila
                    var rowRange = worksheet.Range(row, 1, row, headers.Length);
                    rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // Agregar resumen
                var summaryRow = alumnos.Count + 3;
                worksheet.Cell(summaryRow, 1).Value = "RESUMEN";
                worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;
                worksheet.Cell(summaryRow, 1).Style.Font.FontSize = 14;

                worksheet.Cell(summaryRow + 1, 1).Value = "Total de alumnos:";
                worksheet.Cell(summaryRow + 1, 2).Value = alumnos.Count;
                worksheet.Cell(summaryRow + 1, 1).Style.Font.Bold = true;

                // Estadísticas por carrera
                var carreras = alumnos.GroupBy(a => a.Carrera?.NombreCarrera ?? "Sin carrera")
                                     .OrderBy(g => g.Key)
                                     .ToList();

                worksheet.Cell(summaryRow + 3, 1).Value = "ESTADÍSTICAS POR CARRERA";
                worksheet.Cell(summaryRow + 3, 1).Style.Font.Bold = true;
                worksheet.Cell(summaryRow + 3, 1).Style.Font.FontSize = 12;

                worksheet.Cell(summaryRow + 4, 1).Value = "Carrera";
                worksheet.Cell(summaryRow + 4, 2).Value = "Cantidad";
                worksheet.Cell(summaryRow + 4, 1).Style.Font.Bold = true;
                worksheet.Cell(summaryRow + 4, 2).Style.Font.Bold = true;

                int carreraRow = summaryRow + 5;
                foreach (var carrera in carreras)
                {
                    // Concatenar nombre de carrera con cantidad para evitar cortes
                    worksheet.Cell(carreraRow, 1).Value = $"{carrera.Key} ({carrera.Count()})";
                    carreraRow++;
                }

                // Guardar en memoria
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar reporte de alumnos: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerarReporteFiltradoAsync(int? carreraId, int? estado, bool? ingresoPorEquivalencias, bool? inscritosEnCicloActivo)
        {
            try
            {
                var query = _context.Alumno
                    .Include(a => a.Carrera)
                    .AsQueryable();

                // Aplicar filtros
                if (carreraId.HasValue)
                    query = query.Where(a => a.CarreraId == carreraId.Value);

                if (estado.HasValue)
                    query = query.Where(a => a.Estado == estado.Value);

                if (ingresoPorEquivalencias.HasValue)
                    query = query.Where(a => a.IngresoPorEquivalencias == ingresoPorEquivalencias.Value);

                // Filtro por inscripción en ciclo activo
                if (inscritosEnCicloActivo.HasValue)
                {
                    var cicloActivo = await _context.Ciclos.Where(c => c.Activo).FirstOrDefaultAsync();
                    if (cicloActivo != null)
                    {
                        var alumnosInscritosIds = await _context.Inscripciones
                            .Where(i => i.CicloId == cicloActivo.Id && i.Activa)
                            .Select(i => i.AlumnoId)
                            .ToListAsync();

                        if (inscritosEnCicloActivo.Value)
                        {
                            // Solo alumnos inscritos en el ciclo activo
                            query = query.Where(a => alumnosInscritosIds.Contains(a.AlumnoId));
                        }
                        else
                        {
                            // Solo alumnos NO inscritos en el ciclo activo
                            query = query.Where(a => !alumnosInscritosIds.Contains(a.AlumnoId));
                        }
                    }
                }

                var alumnos = await query
                    .OrderBy(a => a.Apellidos)
                    .ThenBy(a => a.Nombres)
                    .ToListAsync();

                // Generar reporte con los datos filtrados
                return await GenerarReporteConDatosAsync(alumnos);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar reporte filtrado de alumnos: {ex.Message}", ex);
            }
        }

        private string GetGeneroText(int genero)
        {
            return genero switch
            {
                1 => "Femenino",
                0 => "Masculino",
                _ => "-"
            };
        }

        private string GetEstadoText(int estado)
        {
            return estado switch
            {
                1 => "Activo",
                2 => "Inactivo",
                _ => "Desconocido"
            };
        }

        private string GetCarnetFromEmail(Alumno alumno)
        {
            // Si tiene carnet asignado, usarlo; si no, extraer del email
            if (!string.IsNullOrEmpty(alumno.Carnet))
                return alumno.Carnet;
            
            if (!string.IsNullOrEmpty(alumno.Email))
            {
                var atIndex = alumno.Email.IndexOf('@');
                if (atIndex > 0)
                    return alumno.Email.Substring(0, atIndex);
            }
            
            return "";
        }

        private string GetEstadoCivilText(bool casado)
        {
            return casado ? "Casado" : "Soltero";
        }

        private async Task<byte[]> GenerarReporteConDatosAsync(List<Alumno> alumnos)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Alumnos");

                // Encabezados
                string[] headers = {
                    "No.", "Nombres", "Apellidos", "Carrera", "Fecha Nacimiento", 
                    "Email", "DUI", "Teléfono Primario", "Dirección", "Género", 
                    "Carnet", "Estado Civil"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Datos
                int row = 2;
                foreach (var alumno in alumnos)
                {
                    worksheet.Cell(row, 1).Value = row - 1; // Número secuencial
                    worksheet.Cell(row, 2).Value = alumno.Nombres ?? "";
                    worksheet.Cell(row, 3).Value = alumno.Apellidos ?? "";
                    worksheet.Cell(row, 4).Value = alumno.Carrera?.NombreCarrera ?? "";
                    worksheet.Cell(row, 5).Value = alumno.FechaDeNacimiento.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 6).Value = alumno.Email ?? "";
                    worksheet.Cell(row, 7).Value = alumno.DUI ?? "";
                    worksheet.Cell(row, 8).Value = alumno.TelefonoPrimario ?? "";
                    worksheet.Cell(row, 9).Value = alumno.DireccionDeResidencia ?? "";
                    worksheet.Cell(row, 10).Value = GetGeneroText(alumno.Genero);
                    worksheet.Cell(row, 11).Value = GetCarnetFromEmail(alumno);
                    worksheet.Cell(row, 12).Value = GetEstadoCivilText(alumno.Casado);
                    row++;
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // Agregar resumen
                var summaryRow = alumnos.Count + 3;
                worksheet.Cell(summaryRow, 1).Value = "RESUMEN";
                worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;
                worksheet.Cell(summaryRow, 1).Style.Font.FontSize = 14;

                worksheet.Cell(summaryRow + 1, 1).Value = "Total de alumnos:";
                worksheet.Cell(summaryRow + 1, 2).Value = alumnos.Count;
                worksheet.Cell(summaryRow + 1, 1).Style.Font.Bold = true;

                // Estadísticas por carrera
                var carreras = alumnos.GroupBy(a => a.Carrera?.NombreCarrera ?? "Sin carrera")
                                     .OrderBy(g => g.Key)
                                     .ToList();

                worksheet.Cell(summaryRow + 3, 1).Value = "ESTADÍSTICAS POR CARRERA";
                worksheet.Cell(summaryRow + 3, 1).Style.Font.Bold = true;
                worksheet.Cell(summaryRow + 3, 1).Style.Font.FontSize = 12;

                worksheet.Cell(summaryRow + 4, 1).Value = "Carrera";
                worksheet.Cell(summaryRow + 4, 1).Style.Font.Bold = true;

                int carreraRow = summaryRow + 5;
                foreach (var carrera in carreras)
                {
                    // Concatenar nombre de carrera con cantidad para evitar cortes
                    worksheet.Cell(carreraRow, 1).Value = $"{carrera.Key} ({carrera.Count()})";
                    carreraRow++;
                }

                // Guardar en memoria
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar reporte de alumnos: {ex.Message}", ex);
            }
        }
    }
}
