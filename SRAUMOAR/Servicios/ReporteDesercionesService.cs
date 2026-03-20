using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios
{
    public class ReporteDesercionesService
    {
        private readonly Contexto _context;

        public ReporteDesercionesService(Contexto context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerarReporteExcelAsync(int cicloId, int? causaDesercionId = null)
        {
            var ciclo = await _context.Ciclos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cicloId);

            if (ciclo == null)
            {
                throw new InvalidOperationException("No se encontró el ciclo seleccionado.");
            }

            var query = _context.DesercionesAlumno
                .AsNoTracking()
                .Include(d => d.Alumno)
                    .ThenInclude(a => a!.Carrera)
                .Include(d => d.CausaDesercion)
                .Include(d => d.Ciclo)
                .Where(d => d.CicloId == cicloId);

            if (causaDesercionId.HasValue && causaDesercionId.Value > 0)
            {
                query = query.Where(d => d.CausaDesercionId == causaDesercionId.Value);
            }

            var deserciones = await query
                .OrderBy(d => d.Alumno!.Apellidos)
                .ThenBy(d => d.Alumno!.Nombres)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Deserciones");

            worksheet.Cell(1, 1).Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
            worksheet.Range(1, 1, 1, 7).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(2, 1).Value = $"Reporte de deserciones y retiros - Ciclo {ciclo.NCiclo}/{ciclo.anio}";
            worksheet.Range(2, 1, 2, 7).Merge().Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(3, 1, 3, 7).Merge();

            var headers = new[]
            {
                "No.",
                "Carnet",
                "Alumno",
                "Carrera",
                "Causa",
                "Observación",
                "Fecha registro"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var row = 6;
            for (int i = 0; i < deserciones.Count; i++)
            {
                var item = deserciones[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = ObtenerCarnet(item.Alumno);
                worksheet.Cell(row, 3).Value = $"{item.Alumno?.Apellidos}, {item.Alumno?.Nombres}";
                worksheet.Cell(row, 4).Value = item.Alumno?.Carrera?.NombreCarrera ?? string.Empty;
                worksheet.Cell(row, 5).Value = item.CausaDesercion?.Nombre ?? string.Empty;
                worksheet.Cell(row, 6).Value = item.Observacion ?? string.Empty;
                worksheet.Cell(row, 7).Value = item.FechaRegistro.ToString("dd/MM/yyyy HH:mm");
                row++;
            }

            var resumenInicio = row + 2;
            worksheet.Cell(resumenInicio, 1).Value = "RESUMEN";
            worksheet.Cell(resumenInicio, 1).Style.Font.Bold = true;
            worksheet.Cell(resumenInicio + 1, 1).Value = "Total registros:";
            worksheet.Cell(resumenInicio + 1, 2).Value = deserciones.Count;

            var resumenPorCausa = deserciones
                .GroupBy(d => d.CausaDesercion?.Nombre ?? "Sin causa")
                .OrderBy(g => g.Key)
                .ToList();

            worksheet.Cell(resumenInicio + 3, 1).Value = "Totales por causa";
            worksheet.Cell(resumenInicio + 3, 1).Style.Font.Bold = true;
            worksheet.Cell(resumenInicio + 4, 1).Value = "Causa";
            worksheet.Cell(resumenInicio + 4, 2).Value = "Cantidad";
            worksheet.Range(resumenInicio + 4, 1, resumenInicio + 4, 2).Style.Font.Bold = true;

            var resumenRow = resumenInicio + 5;
            foreach (var grupo in resumenPorCausa)
            {
                worksheet.Cell(resumenRow, 1).Value = grupo.Key;
                worksheet.Cell(resumenRow, 2).Value = grupo.Count();
                resumenRow++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string ObtenerCarnet(Entidades.Alumnos.Alumno? alumno)
        {
            if (alumno == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(alumno.Carnet))
            {
                return alumno.Carnet;
            }

            if (!string.IsNullOrWhiteSpace(alumno.Email) && alumno.Email.Contains('@'))
            {
                return alumno.Email.Split('@')[0];
            }

            return string.Empty;
        }
    }
}
