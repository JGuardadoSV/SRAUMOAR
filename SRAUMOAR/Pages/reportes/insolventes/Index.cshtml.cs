using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace SRAUMOAR.Pages.reportes.insolventes
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;
        private readonly ReporteInsolventesService _reporteService;

        public IndexModel(Contexto context, ReporteInsolventesService reporteService)
        {
            _context = context;
            _reporteService = reporteService;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCarreraId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IncluirAlumnosConBeca { get; set; } = false;



        public List<AlumnoInsolvente> AlumnosInsolventes { get; set; } = new();
        public decimal TotalPendiente { get; set; }
        public decimal TotalMora { get; set; }
        public decimal TotalGeneral { get; set; }
        
        // Propiedades para el resumen de aranceles vencidos
        public List<ArancelVencidoResumen> ArancelesVencidos { get; set; } = new();
        public int TotalArancelesVencidos { get; set; }
        public decimal TotalCostoVencido { get; set; }
        public decimal TotalMoraVencido { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                Console.WriteLine($"Usuario autenticado: {User.Identity?.IsAuthenticated}");
                Console.WriteLine($"Roles del usuario: {string.Join(", ", User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value))}");
                
                await CargarCarrerasAsync();
                await CargarAlumnosInsolventesAsync();
                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar el reporte: {ex.Message}";
                // Log del error para debug
                Console.WriteLine($"Error en OnGetAsync: {ex}");
                return Page();
            }
        }

        public async Task<IActionResult> OnGetGenerarPDFAsync(int? carreraId, bool? incluirAlumnosConBeca)
        {
            try
            {
                // Obtener ciclo actual
                var cicloActual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
                if (cicloActual == null)
                {
                    TempData["Error"] = "No hay un ciclo activo";
                    return RedirectToPage();
                }

                // Obtener aranceles obligatorios que YA VENCIERON
                var arancelesObligatorios = await _context.Aranceles
                    .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                    .ToListAsync();

                if (!arancelesObligatorios.Any())
                {
                    TempData["Error"] = "No hay aranceles obligatorios vencidos para el ciclo actual";
                    return RedirectToPage();
                }

                // Obtener alumnos inscritos, filtrando por beca según la opción
                var query = _context.Inscripciones
                    .Include(i => i.Alumno)
                        .ThenInclude(a => a.Carrera)
                    .Include(i => i.Ciclo)
                    .Where(i => i.CicloId == cicloActual.Id && i.Activa);

                // Filtrar por alumnos con beca según la opción seleccionada
                var incluirBecados = incluirAlumnosConBeca ?? IncluirAlumnosConBeca;
                if (!incluirBecados)
                {
                    // Excluir alumnos que tienen beca en el ciclo actual
                    var alumnosConBeca = await _context.Becados
                        .Where(b => b.CicloId == cicloActual.Id && b.Estado)
                        .Select(b => b.AlumnoId)
                        .ToListAsync();

                    query = query.Where(i => !alumnosConBeca.Contains(i.AlumnoId));
                }

                if (carreraId.HasValue && carreraId.Value > 0)
                {
                    query = query.Where(i => i.Alumno.CarreraId == carreraId.Value);
                }

                var inscripciones = await query.ToListAsync();

                // Obtener grupos para agrupación
                var grupos = await _context.Grupo
                    .Include(g => g.Carrera)
                    .Include(g => g.MateriasGrupos)
                        .ThenInclude(mg => mg.MateriasInscritas)
                            .ThenInclude(mi => mi.Alumno)
                    .Where(g => g.CicloId == cicloActual.Id && g.Activo)
                    .ToListAsync();

                // Procesar alumnos insolventes
                var alumnosInsolventes = await ProcesarAlumnosInsolventesAsync(inscripciones, arancelesObligatorios, cicloActual.Id);

                if (!alumnosInsolventes.Any())
                {
                    TempData["Error"] = "No hay alumnos insolventes para generar el reporte PDF";
                    return RedirectToPage();
                }

                // Generar PDF usando el servicio
                byte[] pdfBytes;
                if (carreraId.HasValue)
                {
                    pdfBytes = await _reporteService.GenerarReporteFiltradoAsync(carreraId, null, incluirBecados);
                }
                else
                {
                    pdfBytes = await _reporteService.GenerarReporteCompletoAsync(incluirBecados);
                }

                var fileName = $"ReporteInsolventes_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
                
                TempData["Success"] = "Reporte PDF generado exitosamente";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte PDF: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync(int? carreraId, bool? incluirAlumnosConBeca)
        {
            ExcelPackage.License.SetNonCommercialOrganization("SRAUMOAR");
            try
            {
                // Obtener datos de alumnos insolventes
                var cicloActual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
                if (cicloActual == null)
                {
                    TempData["Error"] = "No hay un ciclo activo";
                    return RedirectToPage();
                }

                                 // Obtener aranceles obligatorios que YA VENCIERON
                 var arancelesObligatorios = await _context.Aranceles
                     .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                     .ToListAsync();

                                 if (!arancelesObligatorios.Any())
                 {
                     TempData["Error"] = "No hay aranceles obligatorios vencidos para el ciclo actual";
                     return RedirectToPage();
                 }

                                 // Obtener alumnos inscritos, filtrando por beca según la opción
                 var query = _context.Inscripciones
                     .Include(i => i.Alumno)
                         .ThenInclude(a => a.Carrera)
                     .Include(i => i.Ciclo)
                     .Where(i => i.CicloId == cicloActual.Id && i.Activa);

                 // Filtrar por alumnos con beca según la opción seleccionada
                 var incluirBecados = incluirAlumnosConBeca ?? IncluirAlumnosConBeca;
                 if (!incluirBecados)
                 {
                     // Excluir alumnos que tienen beca en el ciclo actual
                     var alumnosConBeca = await _context.Becados
                         .Where(b => b.CicloId == cicloActual.Id && b.Estado)
                         .Select(b => b.AlumnoId)
                         .ToListAsync();

                     query = query.Where(i => !alumnosConBeca.Contains(i.AlumnoId));
                 }

                if (carreraId.HasValue && carreraId.Value > 0)
                {
                    query = query.Where(i => i.Alumno.CarreraId == carreraId.Value);
                }

                var inscripciones = await query.ToListAsync();

                // Obtener grupos para agrupación
                var grupos = await _context.Grupo
                    .Include(g => g.Carrera)
                    .Include(g => g.MateriasGrupos)
                        .ThenInclude(mg => mg.MateriasInscritas)
                            .ThenInclude(mi => mi.Alumno)
                    .Where(g => g.CicloId == cicloActual.Id && g.Activo)
                    .ToListAsync();

                // Procesar alumnos insolventes
                var alumnosInsolventes = await ProcesarAlumnosInsolventesAsync(inscripciones, arancelesObligatorios, cicloActual.Id);

                if (!alumnosInsolventes.Any())
                {
                    TempData["Error"] = "No hay alumnos insolventes para generar el reporte Excel";
                    return RedirectToPage();
                }

                // Generar Excel
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Alumnos Insolventes");

                // Título principal
                worksheet.Cells[1, 1, 1, 7].Merge = true;
                worksheet.Cells[1, 1].Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                worksheet.Cells[2, 1, 2, 7].Merge = true;
                worksheet.Cells[2, 1].Value = "REPORTE DE ALUMNOS INSOLVENTES";
                worksheet.Cells[2, 1].Style.Font.Bold = true;
                worksheet.Cells[2, 1].Style.Font.Size = 14;
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Información del ciclo
                worksheet.Cells[3, 1, 3, 7].Merge = true;
                worksheet.Cells[3, 1].Value = $"Ciclo: {cicloActual.NCiclo}/{cicloActual.anio} - Reporte generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cells[3, 1].Style.Font.Size = 10;
                worksheet.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                int currentRow = 5;
                decimal totalGeneralPendiente = 0;
                decimal totalGeneralMora = 0;
                decimal totalGeneralTotal = 0;

                // Agrupar por carrera
                var carrerasConAlumnos = alumnosInsolventes
                    .GroupBy(a => a.Carrera)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var carreraGrupo in carrerasConAlumnos)
                {
                    // Título de la carrera
                    worksheet.Cells[currentRow, 1, currentRow, 7].Merge = true;
                    worksheet.Cells[currentRow, 1].Value = $"CARRERA: {carreraGrupo.Key?.ToUpper() ?? "SIN CARRERA"}";
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    currentRow++;

                    // Obtener grupos de esta carrera
                    var gruposCarrera = grupos
                        .Where(g => g.Carrera?.NombreCarrera == carreraGrupo.Key)
                        .ToList();

                    decimal totalCarreraPendiente = 0;
                    decimal totalCarreraMora = 0;
                    decimal totalCarreraTotal = 0;

                    foreach (var grupo in gruposCarrera.OrderBy(g => g.Nombre))
                    {
                        // Obtener alumnos de este grupo
                        var alumnosGrupo = grupo.MateriasGrupos != null
                            ? grupo.MateriasGrupos
                                .SelectMany(mg => mg.MateriasInscritas ?? Enumerable.Empty<MateriasInscritas>())
                                .Select(mi => mi.Alumno)
                                .Distinct()
                                .ToList()
                            : new List<Alumno>();

                        var alumnosInsolventesGrupo = alumnosInsolventes
                            .Where(a => alumnosGrupo.Any(ag => ag.AlumnoId == a.AlumnoId))
                            .ToList();

                        if (alumnosInsolventesGrupo.Any())
                        {
                            // Título del grupo
                            worksheet.Cells[currentRow, 1, currentRow, 7].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = $"Grupo: {grupo.Nombre}";
                            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                            worksheet.Cells[currentRow, 1].Style.Font.Size = 11;
                            worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                            currentRow++;

                            // Encabezados de la tabla
                            var headers = new[] { "No.", "Nombre Completo", "Carnet", "Pendiente", "Mora", "Total" };
                            for (int i = 0; i < headers.Length; i++)
                            {
                                worksheet.Cells[currentRow, i + 1].Value = headers[i];
                                worksheet.Cells[currentRow, i + 1].Style.Font.Bold = true;
                                worksheet.Cells[currentRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Cells[currentRow, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                                worksheet.Cells[currentRow, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            }
                            currentRow++;

                            // Datos de alumnos
                            int contador = 1;
                            foreach (var alumno in alumnosInsolventesGrupo.OrderBy(a => a.Apellidos).ThenBy(a => a.Nombres))
                            {
                                worksheet.Cells[currentRow, 1].Value = contador;
                                worksheet.Cells[currentRow, 2].Value = $"{alumno.Apellidos}, {alumno.Nombres}";
                                worksheet.Cells[currentRow, 3].Value = alumno.Carnet ?? alumno.Email?.Split('@')[0] ?? "";
                                worksheet.Cells[currentRow, 4].Value = alumno.TotalPendiente;
                                worksheet.Cells[currentRow, 5].Value = alumno.TotalMora;
                                worksheet.Cells[currentRow, 6].Value = alumno.TotalGeneral;

                                // Formato de moneda para columnas de montos
                                worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "#,##0.00";
                                worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "#,##0.00";
                                worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                                worksheet.Cells[currentRow, 6].Style.Font.Bold = true;

                                // Bordes
                                for (int col = 1; col <= 6; col++)
                                {
                                    worksheet.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                                }

                                contador++;
                                currentRow++;
                            }

                            // Subtotal del grupo
                            var totalGrupoPendiente = alumnosInsolventesGrupo.Sum(a => a.TotalPendiente);
                            var totalGrupoMora = alumnosInsolventesGrupo.Sum(a => a.TotalMora);
                            var totalGrupoTotal = alumnosInsolventesGrupo.Sum(a => a.TotalGeneral);

                            worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = $"Subtotal Grupo {grupo.Nombre}:";
                            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                            worksheet.Cells[currentRow, 4].Value = totalGrupoPendiente;
                            worksheet.Cells[currentRow, 5].Value = totalGrupoMora;
                            worksheet.Cells[currentRow, 6].Value = totalGrupoTotal;
                            worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 6].Style.Font.Bold = true;
                            currentRow++;

                            totalCarreraPendiente += totalGrupoPendiente;
                            totalCarreraMora += totalGrupoMora;
                            totalCarreraTotal += totalGrupoTotal;
                        }
                    }

                    // Total de la carrera
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    worksheet.Cells[currentRow, 1].Value = $"TOTAL CARRERA {carreraGrupo.Key?.ToUpper()}:";
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 11;
                    worksheet.Cells[currentRow, 4].Value = totalCarreraPendiente;
                    worksheet.Cells[currentRow, 5].Value = totalCarreraMora;
                    worksheet.Cells[currentRow, 6].Value = totalCarreraTotal;
                    worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 6].Style.Font.Bold = true;
                    currentRow++;

                    totalGeneralPendiente += totalCarreraPendiente;
                    totalGeneralMora += totalCarreraMora;
                    totalGeneralTotal += totalCarreraTotal;

                    // Línea separadora
                    currentRow++;
                }

                // Total general
                worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                worksheet.Cells[currentRow, 1].Value = "TOTAL GENERAL:";
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                worksheet.Cells[currentRow, 4].Value = totalGeneralPendiente;
                worksheet.Cells[currentRow, 5].Value = totalGeneralMora;
                worksheet.Cells[currentRow, 6].Value = totalGeneralTotal;
                worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 6].Style.Font.Bold = true;

                                 // Autoajustar columnas
                 worksheet.Cells.AutoFitColumns();

                 // Obtener aranceles vencidos para el resumen
                 var arancelesVencidos = await _context.Aranceles
                     .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                     .OrderBy(a => a.FechaFin)
                     .ToListAsync();

                 // Agregar hoja de aranceles vencidos si existen
                 if (arancelesVencidos.Any())
                 {
                     var worksheetVencidos = package.Workbook.Worksheets.Add("Aranceles Vencidos");

                     // Título principal
                     worksheetVencidos.Cells[1, 1, 1, 5].Merge = true;
                     worksheetVencidos.Cells[1, 1].Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
                     worksheetVencidos.Cells[1, 1].Style.Font.Bold = true;
                     worksheetVencidos.Cells[1, 1].Style.Font.Size = 16;
                     worksheetVencidos.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                     worksheetVencidos.Cells[2, 1, 2, 5].Merge = true;
                     worksheetVencidos.Cells[2, 1].Value = "RESUMEN DE ARANCELES VENCIDOS";
                     worksheetVencidos.Cells[2, 1].Style.Font.Bold = true;
                     worksheetVencidos.Cells[2, 1].Style.Font.Size = 14;
                     worksheetVencidos.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                     // Información del ciclo
                     worksheetVencidos.Cells[3, 1, 3, 5].Merge = true;
                     worksheetVencidos.Cells[3, 1].Value = $"Ciclo: {cicloActual.NCiclo}/{cicloActual.anio} - Reporte generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                     worksheetVencidos.Cells[3, 1].Style.Font.Size = 10;
                     worksheetVencidos.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                     // Encabezados
                     var headersVencidos = new[] { "Arancel", "Fecha Vencimiento", "Días Vencido", "Costo", "Total + Mora" };
                     for (int i = 0; i < headersVencidos.Length; i++)
                     {
                         worksheetVencidos.Cells[5, i + 1].Value = headersVencidos[i];
                         worksheetVencidos.Cells[5, i + 1].Style.Font.Bold = true;
                         worksheetVencidos.Cells[5, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                         worksheetVencidos.Cells[5, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                         worksheetVencidos.Cells[5, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                     }

                     // Datos de aranceles vencidos
                     int currentRowVencidos = 6;
                     decimal totalCostoVencido = 0;
                     decimal totalMoraVencido = 0;

                                           // Crear lista de aranceles vencidos para Excel
                      var arancelesVencidosExcel = arancelesVencidos.Select(a => new ArancelVencidoResumen
                      {
                          Nombre = a.Nombre,
                          FechaVencimiento = a.FechaFin.Value,
                          DiasVencido = (DateTime.Now.Date - a.FechaFin.Value.Date).Days,
                          Costo = a.Costo,
                          ValorMora = a.ValorMora
                      }).ToList();

                      foreach (var arancel in arancelesVencidosExcel.OrderBy(a => a.FechaVencimiento))
                     {
                         worksheetVencidos.Cells[currentRowVencidos, 1].Value = arancel.Nombre;
                         worksheetVencidos.Cells[currentRowVencidos, 2].Value = arancel.FechaVencimiento.ToString("dd/MM/yyyy");
                         worksheetVencidos.Cells[currentRowVencidos, 3].Value = $"{arancel.DiasVencido} días";
                         worksheetVencidos.Cells[currentRowVencidos, 4].Value = arancel.Costo;
                         worksheetVencidos.Cells[currentRowVencidos, 5].Value = arancel.Costo + arancel.ValorMora;

                         // Formato de moneda para columnas de montos
                         worksheetVencidos.Cells[currentRowVencidos, 4].Style.Numberformat.Format = "#,##0.00";
                         worksheetVencidos.Cells[currentRowVencidos, 5].Style.Numberformat.Format = "#,##0.00";

                         // Color de fondo según días vencido
                         if (arancel.DiasVencido > 30)
                         {
                             worksheetVencidos.Cells[currentRowVencidos, 1, currentRowVencidos, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                             worksheetVencidos.Cells[currentRowVencidos, 1, currentRowVencidos, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                         }
                         else if (arancel.DiasVencido > 15)
                         {
                             worksheetVencidos.Cells[currentRowVencidos, 1, currentRowVencidos, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                             worksheetVencidos.Cells[currentRowVencidos, 1, currentRowVencidos, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                         }

                         // Bordes
                         for (int col = 1; col <= 5; col++)
                         {
                             worksheetVencidos.Cells[currentRowVencidos, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                         }

                         totalCostoVencido += arancel.Costo;
                         totalMoraVencido += arancel.ValorMora;
                         currentRowVencidos++;
                     }

                     // Totales
                     worksheetVencidos.Cells[currentRowVencidos, 1, currentRowVencidos, 3].Merge = true;
                     worksheetVencidos.Cells[currentRowVencidos, 1].Value = "TOTALES:";
                     worksheetVencidos.Cells[currentRowVencidos, 1].Style.Font.Bold = true;
                     worksheetVencidos.Cells[currentRowVencidos, 4].Value = totalCostoVencido;
                     worksheetVencidos.Cells[currentRowVencidos, 5].Value = totalCostoVencido + totalMoraVencido;
                     worksheetVencidos.Cells[currentRowVencidos, 4].Style.Numberformat.Format = "#,##0.00";
                     worksheetVencidos.Cells[currentRowVencidos, 5].Style.Numberformat.Format = "#,##0.00";
                     worksheetVencidos.Cells[currentRowVencidos, 5].Style.Font.Bold = true;

                     // Autoajustar columnas
                     worksheetVencidos.Cells.AutoFitColumns();
                 }

                 var fileName = $"ReporteInsolventes_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var content = package.GetAsByteArray();
                
                Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
                TempData["Success"] = "Reporte Excel generado exitosamente";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte Excel: {ex.Message}";
                return RedirectToPage();
            }
        }

        private async Task CargarCarrerasAsync()
        {
            var carreras = await _context.Carreras
                .Where(c => c.Activa)
                .OrderBy(c => c.NombreCarrera)
                .ToListAsync();

            ViewData["Carreras"] = new SelectList(carreras, "CarreraId", "NombreCarrera");
        }

        private async Task CargarAlumnosInsolventesAsync()
        {
            Console.WriteLine("Iniciando carga de alumnos insolventes...");
            
            var cicloActual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
            if (cicloActual == null)
            {
                TempData["Error"] = "No hay un ciclo activo";
                Console.WriteLine("No se encontró ciclo activo");
                return;
            }
            
            Console.WriteLine($"Ciclo activo encontrado: {cicloActual.NCiclo}");

                         // Obtener aranceles obligatorios del ciclo actual que YA VENCIERON
             var arancelesObligatorios = await _context.Aranceles
                 .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                 .ToListAsync();

                         Console.WriteLine($"Aranceles obligatorios vencidos encontrados: {arancelesObligatorios.Count}");
            foreach (var arancel in arancelesObligatorios)
            {
                                 Console.WriteLine($"- {arancel.Nombre}: Vencido el {arancel.FechaFin:dd/MM/yyyy}");
            }

                             // Obtener aranceles vencidos para el resumen
                 var arancelesVencidos = await _context.Aranceles
                     .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                     .OrderBy(a => a.FechaFin)
                     .ToListAsync();

                 Console.WriteLine($"\n=== RESUMEN DE ARANCELES VENCIDOS ===");
                 Console.WriteLine($"Total aranceles vencidos: {arancelesVencidos.Count}");
                 foreach (var arancel in arancelesVencidos)
                 {
                     var diasVencido = (DateTime.Now.Date - arancel.FechaFin.Value.Date).Days;
                     Console.WriteLine($"- {arancel.Nombre}: Vencido el {arancel.FechaFin:dd/MM/yyyy} (hace {diasVencido} días) - Costo: ${arancel.Costo:F2} - Mora: ${arancel.ValorMora:F2}");
                 }
                 Console.WriteLine($"=== FIN RESUMEN ===\n");

                 // Guardar información de aranceles vencidos para mostrar en la vista
                 ArancelesVencidos = arancelesVencidos.Select(a => new ArancelVencidoResumen
                 {
                     Nombre = a.Nombre,
                     FechaVencimiento = a.FechaFin.Value,
                     DiasVencido = (DateTime.Now.Date - a.FechaFin.Value.Date).Days,
                     Costo = a.Costo,
                     ValorMora = a.ValorMora
                 }).ToList();

                 TotalArancelesVencidos = ArancelesVencidos.Count;
                 TotalCostoVencido = ArancelesVencidos.Sum(a => a.Costo);
                 TotalMoraVencido = ArancelesVencidos.Sum(a => a.ValorMora);

                 // Crear lista de aranceles vencidos para Excel
                 var arancelesVencidosExcel = arancelesVencidos.Select(a => new ArancelVencidoResumen
                 {
                     Nombre = a.Nombre,
                     FechaVencimiento = a.FechaFin.Value,
                     DiasVencido = (DateTime.Now.Date - a.FechaFin.Value.Date).Days,
                     Costo = a.Costo,
                     ValorMora = a.ValorMora
                 }).ToList();

                         if (!arancelesObligatorios.Any())
             {
                 TempData["Warning"] = "No hay aranceles obligatorios vencidos para el ciclo actual";
                 return;
             }

                         // Obtener alumnos inscritos en el ciclo actual, excluyendo los que tienen beca
             var query = _context.Inscripciones
                 .Include(i => i.Alumno)
                     .ThenInclude(a => a.Carrera)
                 .Include(i => i.Ciclo)
                 .Where(i => i.CicloId == cicloActual.Id && i.Activa);

             // Filtrar por alumnos con beca según la opción seleccionada
             if (!IncluirAlumnosConBeca)
             {
                 // Excluir alumnos que tienen beca en el ciclo actual
                 var alumnosConBeca = await _context.Becados
                     .Where(b => b.CicloId == cicloActual.Id && b.Estado)
                     .Select(b => b.AlumnoId)
                     .ToListAsync();

                 query = query.Where(i => !alumnosConBeca.Contains(i.AlumnoId));
             }

            if (SelectedCarreraId.HasValue && SelectedCarreraId.Value > 0)
            {
                query = query.Where(i => i.Alumno.CarreraId == SelectedCarreraId.Value);
            }

            var inscripciones = await query.ToListAsync();

            // Procesar alumnos insolventes
            AlumnosInsolventes = await ProcesarAlumnosInsolventesAsync(inscripciones, arancelesObligatorios, cicloActual.Id);



            // Calcular totales
            TotalPendiente = AlumnosInsolventes.Sum(a => a.TotalPendiente);
            TotalMora = AlumnosInsolventes.Sum(a => a.TotalMora);
            TotalGeneral = AlumnosInsolventes.Sum(a => a.TotalGeneral);
        }

        private async Task<List<AlumnoInsolvente>> ProcesarAlumnosInsolventesAsync(
            List<Inscripcion> inscripciones, 
            List<Arancel> arancelesObligatorios, 
            int cicloId)
        {
            var alumnosInsolventes = new List<AlumnoInsolvente>();

            // Obtener todos los pagos de una vez para evitar consultas en bucle
            var pagosExistentes = await _context.CobrosArancel
                .Include(ca => ca.DetallesCobroArancel)
                .Where(ca => ca.CicloId == cicloId)
                .ToListAsync();

            // Crear un diccionario para acceso rápido
            var pagosPorAlumno = pagosExistentes
                .GroupBy(ca => ca.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(ca => ca.DetallesCobroArancel)
                          .Select(dca => dca.ArancelId)
                          .ToHashSet()
                );

            foreach (var inscripcion in inscripciones)
            {
                var alumno = inscripcion.Alumno;
                
                // Excluir alumnos con excepciones
                if (alumno.PermiteInscripcionSinPago)
                    continue;

                var arancelesPendientes = new List<ArancelPendiente>();

                // Obtener aranceles pagados por este alumno
                var arancelesPagados = pagosPorAlumno.GetValueOrDefault(alumno.AlumnoId, new HashSet<int>());

                foreach (var arancel in arancelesObligatorios)
                {
                    // Verificar si el alumno ya pagó este arancel
                    if (!arancelesPagados.Contains(arancel.ArancelId))
                    {
                        // Calcular mora solo si el alumno no está exento y el arancel ya venció
                        var mora = 0m;
                        if (!alumno.ExentoMora && arancel.FechaFin.HasValue && arancel.FechaFin.Value.Date < DateTime.Now.Date)
                        {
                            mora = arancel.ValorMora;
                        }

                        arancelesPendientes.Add(new ArancelPendiente
                        {
                            ArancelId = arancel.ArancelId,
                            NombreArancel = arancel.Nombre,
                            CostoOriginal = arancel.Costo,
                            Mora = mora,
                            TotalConMora = arancel.Costo + mora,
                            FechaVencimiento = arancel.FechaFin,
                            EstaVencido = arancel.EstaVencido
                        });
                    }
                }

                if (arancelesPendientes.Any())
                {
                    alumnosInsolventes.Add(new AlumnoInsolvente
                    {
                        AlumnoId = alumno.AlumnoId,
                        Nombres = alumno.Nombres,
                        Apellidos = alumno.Apellidos,
                        Carrera = alumno.Carrera?.NombreCarrera,
                        Carnet = alumno.Carnet,
                        Email = alumno.Email,
                        ArancelesPendientes = arancelesPendientes,
                        TotalPendiente = arancelesPendientes.Sum(ap => ap.CostoOriginal),
                        TotalMora = arancelesPendientes.Sum(ap => ap.Mora),
                        TotalGeneral = arancelesPendientes.Sum(ap => ap.TotalConMora)
                    });
                }
            }

            return alumnosInsolventes;
        }
    }

    // Clase para el resumen de aranceles vencidos
    public class ArancelVencidoResumen
    {
        public string Nombre { get; set; } = "";
        public DateTime FechaVencimiento { get; set; }
        public int DiasVencido { get; set; }
        public decimal Costo { get; set; }
        public decimal ValorMora { get; set; }
    }
}
