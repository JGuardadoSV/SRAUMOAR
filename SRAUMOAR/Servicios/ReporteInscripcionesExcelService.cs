using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios
{
    public class ReporteInscripcionesExcelService
    {
        private readonly Contexto _context;

        public ReporteInscripcionesExcelService(Contexto context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerarExcelAsync(int cicloId, int? carreraId, string? genero)
        {
            var ciclo = await _context.Ciclos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cicloId);

            if (ciclo == null)
            {
                throw new InvalidOperationException("No se encontró el ciclo seleccionado.");
            }

            var query = _context.Inscripciones
                .AsNoTracking()
                .Include(i => i.Alumno)
                    .ThenInclude(a => a!.Carrera)
                .Include(i => i.Ciclo)
                .Where(i => i.CicloId == cicloId && i.Activa);

            if (carreraId.HasValue && carreraId.Value > 0)
            {
                query = query.Where(i => i.Alumno != null && i.Alumno.CarreraId == carreraId.Value);
            }

            if (!string.IsNullOrWhiteSpace(genero) && int.TryParse(genero, out var generoInt))
            {
                query = query.Where(i => i.Alumno != null && i.Alumno.Genero == generoInt);
            }

            var inscripciones = await query
                .OrderBy(i => i.Alumno!.Carrera!.NombreCarrera)
                .ThenBy(i => (i.Alumno!.Apellidos ?? string.Empty).Trim())
                .ThenBy(i => (i.Alumno!.Nombres ?? string.Empty).Trim())
                .ToListAsync();

            var inscripcionesUnicas = inscripciones
                .Where(i => i.Alumno != null)
                .GroupBy(i => i.AlumnoId)
                .Select(g => g
                    .OrderByDescending(i => i.Fecha)
                    .ThenByDescending(i => i.InscripcionId)
                    .First())
                .ToList();

            var alumnoIds = inscripcionesUnicas
                .Select(i => i.AlumnoId)
                .Distinct()
                .ToList();

            var becasPorAlumno = await _context.Becados
                .AsNoTracking()
                .Where(b => b.CicloId == cicloId && b.Estado && alumnoIds.Contains(b.AlumnoId))
                .OrderByDescending(b => b.FechaRegistro)
                .ToListAsync();

            var tipoBecaPorAlumno = becasPorAlumno
                .GroupBy(b => b.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => ResolverTipoBeca(g.First().TipoBeca));

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Inscripciones");
            ConfigurarColumnas(worksheet);

            var currentRow = 1;
            currentRow = EscribirEncabezado(worksheet, currentRow, ciclo, carreraId, genero);

            var gruposCarrera = inscripcionesUnicas
                .GroupBy(i => new
                {
                    CarreraId = i.Alumno?.CarreraId ?? 0,
                    NombreCarrera = i.Alumno?.Carrera?.NombreCarrera ?? "SIN CARRERA"
                })
                .OrderBy(g => g.Key.NombreCarrera)
                .ToList();

            if (gruposCarrera.Count == 0)
            {
                worksheet.Cell(currentRow, 1).Value = "No hay inscripciones para los filtros seleccionados.";
                worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            else
            {
                foreach (var carreraGroup in gruposCarrera)
                {
                    currentRow = EscribirTituloCarrera(worksheet, currentRow, carreraGroup.Key.NombreCarrera);
                    currentRow = EscribirEncabezadosTabla(worksheet, currentRow);

                    var correlativo = 1;
                    foreach (var inscripcion in carreraGroup
                        .OrderBy(i => (i.Alumno!.Apellidos ?? string.Empty).Trim())
                        .ThenBy(i => (i.Alumno!.Nombres ?? string.Empty).Trim()))
                    {
                        var alumno = inscripcion.Alumno!;
                        worksheet.Cell(currentRow, 1).Value = correlativo;
                        worksheet.Cell(currentRow, 2).Value = $"{alumno.Apellidos}, {alumno.Nombres}";
                        worksheet.Cell(currentRow, 3).Value = ObtenerCarnet(alumno.Carnet, alumno.Email);
                        worksheet.Cell(currentRow, 4).Value = alumno.FechaDeNacimiento;
                        worksheet.Cell(currentRow, 4).Style.DateFormat.Format = "dd/MM/yyyy";
                        worksheet.Cell(currentRow, 5).Value = CalcularEdad(alumno.FechaDeNacimiento, DateTime.Today);
                        worksheet.Cell(currentRow, 6).Value = ObtenerGeneroTexto(alumno.Genero);
                        worksheet.Cell(currentRow, 7).Value = tipoBecaPorAlumno.TryGetValue(alumno.AlumnoId, out var tipoBeca)
                            ? tipoBeca
                            : "No";

                        worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                        correlativo++;
                    }

                    worksheet.Cell(currentRow, 1).Value = "Total carrera";
                    worksheet.Range(currentRow, 1, currentRow, 6).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
                    worksheet.Cell(currentRow, 7).Value = carreraGroup.Count();
                    worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
                    worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    currentRow += 2;
                }
            }

            worksheet.SheetView.FreezeRows(5);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void ConfigurarColumnas(IXLWorksheet worksheet)
        {
            worksheet.Column(1).Width = 8;
            worksheet.Column(2).Width = 38;
            worksheet.Column(3).Width = 16;
            worksheet.Column(4).Width = 16;
            worksheet.Column(5).Width = 10;
            worksheet.Column(6).Width = 12;
            worksheet.Column(7).Width = 14;
        }

        private int EscribirEncabezado(IXLWorksheet worksheet, int currentRow, Entidades.Procesos.Ciclo ciclo, int? carreraId, string? genero)
        {
            worksheet.Cell(currentRow, 1).Value = "REPORTE DE INSCRIPCIONES";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = $"Ciclo consultado: {ciclo.NCiclo} - {ciclo.anio}";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = ConstruirDescripcionFiltros(carreraId, genero);
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = $"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow += 2;

            return currentRow;
        }

        private static int EscribirTituloCarrera(IXLWorksheet worksheet, int currentRow, string nombreCarrera)
        {
            worksheet.Cell(currentRow, 1).Value = nombreCarrera;
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
            worksheet.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            return currentRow + 1;
        }

        private static int EscribirEncabezadosTabla(IXLWorksheet worksheet, int currentRow)
        {
            var headers = new[]
            {
                "No.",
                "Nombre Completo",
                "Carnet",
                "Fecha Nacimiento",
                "Edad",
                "Género",
                "Beca"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                var cell = worksheet.Cell(currentRow, column + 1);
                cell.Value = headers[column];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            return currentRow + 1;
        }

        private string ConstruirDescripcionFiltros(int? carreraId, string? genero)
        {
            var carreraTexto = "Todas las carreras";
            if (carreraId.HasValue && carreraId.Value > 0)
            {
                carreraTexto = _context.Carreras
                    .AsNoTracking()
                    .Where(c => c.CarreraId == carreraId.Value)
                    .Select(c => c.NombreCarrera)
                    .FirstOrDefault() ?? carreraTexto;
            }

            var generoTexto = genero switch
            {
                "0" => "Hombres",
                "1" => "Mujeres",
                _ => "Ambos"
            };

            return $"Filtros: Carrera = {carreraTexto} | Género = {generoTexto}";
        }

        private static int CalcularEdad(DateTime fechaNacimiento, DateTime fechaReferencia)
        {
            var edad = fechaReferencia.Year - fechaNacimiento.Year;
            if (fechaNacimiento.Date > fechaReferencia.AddYears(-edad))
            {
                edad--;
            }

            return edad;
        }

        private static string ResolverTipoBeca(int tipoBeca)
        {
            return tipoBeca switch
            {
                1 => "Total",
                2 => "Parcial",
                _ => "Si"
            };
        }

        private static string ObtenerGeneroTexto(int genero)
        {
            return genero switch
            {
                0 => "Hombre",
                1 => "Mujer",
                _ => "Otro"
            };
        }

        private static string ObtenerCarnet(string? carnet, string? email)
        {
            if (!string.IsNullOrWhiteSpace(carnet))
            {
                return carnet;
            }

            if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            {
                return email.Split('@')[0];
            }

            return string.Empty;
        }
    }
}
