using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace SRAUMOAR.Pages.becados
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Becados> Becados { get; set; } = default!;
        // Paginación
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15; // Puedes ajustar el tamaño de página aquí
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        
        // Propiedades para estadísticas totales (sin paginación)
        public int TotalBecas { get; set; }
        public int TotalBecasCompletas { get; set; }
        public int TotalBecasParciales { get; set; }
        public int TotalBecasActivas { get; set; }
        
        // Propiedades para los filtros
        [BindProperty(SupportsGet = true)]
        public int? CarreraId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? EntidadBecaId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? TipoBeca { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? Estado { get; set; }

        // Listas para los dropdowns de filtros
        public SelectList Carreras { get; set; } = default!;
        public SelectList EntidadesBeca { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // Cargar las listas para los filtros
            Carreras = new SelectList(await _context.Carreras
                .Where(c => c.Activa == true)
                .OrderBy(c => c.NombreCarrera)
                .ToListAsync(), "CarreraId", "NombreCarrera");
                
            EntidadesBeca = new SelectList(await _context.InstitucionesBeca
                .OrderBy(e => e.Nombre)
                .ToListAsync(), "EntidadBecaId", "Nombre");

            // Construir la consulta base
            var query = _context.Becados
                .Include(b => b.Alumno)
                    .ThenInclude(a => a.Carrera)
                .Include(b => b.Ciclo)
                .Include(b => b.EntidadBeca)
                .Where(x => x.Ciclo.Activo == true);

            // Aplicar filtros
            if (CarreraId.HasValue)
            {
                query = query.Where(b => b.Alumno.CarreraId == CarreraId.Value);
            }

            if (EntidadBecaId.HasValue)
            {
                query = query.Where(b => b.EntidadBecaId == EntidadBecaId.Value);
            }

            if (!string.IsNullOrEmpty(TipoBeca))
            {
                if (TipoBeca == "Completa")
                {
                    query = query.Where(b => b.TipoBeca == 1);
                }
                else if (TipoBeca == "Parcial")
                {
                    query = query.Where(b => b.TipoBeca == 2);
                }
            }

            if (!string.IsNullOrEmpty(Estado))
            {
                if (Estado == "Activo")
                {
                    query = query.Where(b => b.Estado == true);
                }
                else if (Estado == "Inactivo")
                {
                    query = query.Where(b => b.Estado == false);
                }
            }

            // Calcular estadísticas totales (sin paginación)
            TotalBecas = await query.CountAsync();
            TotalBecasCompletas = await query.CountAsync(b => b.TipoBeca == 1);
            TotalBecasParciales = await query.CountAsync(b => b.TipoBeca == 2);
            TotalBecasActivas = await query.CountAsync(b => b.Estado == true);

            // Total de registros para paginación
            TotalRecords = TotalBecas;
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (PageNumber < 1) PageNumber = 1;
            if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

            // Ejecutar la consulta paginada
            Becados = await query
                .OrderBy(b => b.Alumno.Carrera.NombreCarrera)
                .ThenBy(b => b.Alumno.Apellidos)
                .ThenBy(b => b.Alumno.Nombres)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnGetGenerarReporteExcelAsync()
        {
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");
            try
            {
                // Obtener la lista filtrada de becados (sin paginación)
                var query = _context.Becados
                    .Include(b => b.Alumno)
                        .ThenInclude(a => a.Carrera)
                    .Include(b => b.Ciclo)
                    .Include(b => b.EntidadBeca)
                    .Where(x => x.Ciclo.Activo == true);

                if (CarreraId.HasValue)
                    query = query.Where(b => b.Alumno.CarreraId == CarreraId.Value);
                if (EntidadBecaId.HasValue)
                    query = query.Where(b => b.EntidadBecaId == EntidadBecaId.Value);
                if (!string.IsNullOrEmpty(TipoBeca))
                {
                    if (TipoBeca == "Completa")
                        query = query.Where(b => b.TipoBeca == 1);
                    else if (TipoBeca == "Parcial")
                        query = query.Where(b => b.TipoBeca == 2);
                }
                if (!string.IsNullOrEmpty(Estado))
                {
                    if (Estado == "Activo")
                        query = query.Where(b => b.Estado == true);
                    else if (Estado == "Inactivo")
                        query = query.Where(b => b.Estado == false);
                }

                var becados = await query
                    .OrderBy(b => b.Alumno.Carrera.NombreCarrera)
                    .ThenBy(b => b.Alumno.Apellidos)
                    .ThenBy(b => b.Alumno.Nombres)
                    .ToListAsync();

                if (!becados.Any())
                {
                    TempData["Error"] = "No hay datos para generar el reporte Excel";
                    return RedirectToPage();
                }

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Becados");

                // Título y subtítulo
                worksheet.Cells[1, 1, 1, 9].Merge = true;
                worksheet.Cells[1, 1].Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[2, 1, 2, 9].Merge = true;
                worksheet.Cells[2, 1].Value = "Reporte de Becados";
                worksheet.Cells[2, 1].Style.Font.Bold = true;
                worksheet.Cells[2, 1].Style.Font.Size = 13;
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Encabezados
                var headers = new[] {
                    "#", "Alumno", "Carnet", "Carrera", "Tipo de Beca", "Entidad", "Ciclo", "Fecha Registro", "Estado"
                };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[3, i + 1].Value = headers[i];
                    worksheet.Cells[3, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                    worksheet.Cells[3, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // Datos
                for (int row = 0; row < becados.Count; row++)
                {
                    var b = becados[row];
                    var excelRow = row + 4;
                    worksheet.Cells[excelRow, 1].Value = row + 1;
                    worksheet.Cells[excelRow, 2].Value = $"{b.Alumno.Nombres} {b.Alumno.Apellidos}";
                    
                    // Obtener carnet: si tiene valor, usarlo; si no, extraerlo del correo
                    string carnet = string.Empty;
                    if (!string.IsNullOrWhiteSpace(b.Alumno.Carnet))
                    {
                        carnet = b.Alumno.Carnet;
                    }
                    else if (!string.IsNullOrWhiteSpace(b.Alumno.Email) && b.Alumno.Email.Contains("@umoar.edu.sv"))
                    {
                        // Extraer lo que está antes del @ del correo
                        var emailParts = b.Alumno.Email.Split('@');
                        if (emailParts.Length > 0)
                        {
                            carnet = emailParts[0];
                        }
                    }
                    worksheet.Cells[excelRow, 3].Value = carnet;
                    
                    worksheet.Cells[excelRow, 4].Value = b.Alumno.Carrera?.NombreCarrera;
                    worksheet.Cells[excelRow, 5].Value = b.TipoBeca == 1 ? "Completa" : "Parcial";
                    worksheet.Cells[excelRow, 6].Value = b.EntidadBeca?.Nombre;
                    worksheet.Cells[excelRow, 7].Value = $"Ciclo {b.Ciclo?.NCiclo}/{b.Ciclo?.anio}";
                    worksheet.Cells[excelRow, 8].Value = b.FechaRegistro.ToString("dd/MM/yyyy");
                    worksheet.Cells[excelRow, 9].Value = b.Estado ? "Activo" : "Inactivo";
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cells[excelRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var excelBytes = package.GetAsByteArray();
                var fileName = $"ReporteBecados_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte Excel: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
