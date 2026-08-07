using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class FacturasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IDtePdfService _pdfService;
        private readonly IDteJsonFactory _jsonFactory;

        public FacturasModel(Contexto context, IDtePdfService pdfService, IDteJsonFactory jsonFactory)
        {
            _context = context;
            _pdfService = pdfService;
            _jsonFactory = jsonFactory;
        }
        
        [BindProperty]
        public IList<CobroArancel> CobroArancel { get;set; } = default!;

        [BindProperty(SupportsGet = true)]
        public DateTime FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime FechaFin { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public string? carnetAlumno { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PaginaActual { get; set; } = 1;

        public int TotalPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public int RegistrosPorPagina { get; set; } = 10;
        public decimal TotalMonto { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.CobrosArancel
                .Include(c => c.Alumno)
                .Include(c => c.Ciclo)
                .Where(c => c.Fecha >= FechaInicio && c.Fecha <= FechaFin.AddDays(1).AddSeconds(-1));

            if (!string.IsNullOrEmpty(carnetAlumno))
            {
                query = query.Where(c => c.Alumno.Carnet == carnetAlumno);
            }

            // Obtener total de registros para paginación
            TotalRegistros = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / RegistrosPorPagina);

            // Calcular total del monto de todos los registros filtrados (no solo los de la página actual)
            TotalMonto = await query.SumAsync(c => c.Total);

            // Aplicar paginación
            CobroArancel = await query
                .OrderByDescending(c => c.CobroArancelId)
                .Skip((PaginaActual - 1) * RegistrosPorPagina)
                .Take(RegistrosPorPagina)
                .ToListAsync();
        }
        public IActionResult OnGetGenerarPDFSinDatos()
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            document.Add(new Paragraph("Aviso")
                .SetFontSize(18)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Add(new Paragraph("No hay datos disponibles para generar el PDF.")
                .SetFontSize(12)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Close();
            Response.Headers["Content-Disposition"] = "inline; filename=SinDatos.pdf";
            return File(memoryStream.ToArray(), "application/pdf");
            
        }

        public async Task<IActionResult> OnGetGenerarPdfAsync(int id)
        {
            try
            {
                CobroArancel cobroArancel = await _context.CobrosArancel
                    .Include(c => c.Alumno).ThenInclude(a => a.Carrera)
                    .Include(c => c.Ciclo)
                    .FirstOrDefaultAsync(c => c.CobroArancelId == id);

                Factura factura = await _context.Facturas.FirstOrDefaultAsync(f => f.CodigoGeneracion == cobroArancel.CodigoGeneracion);
                if (factura is null || string.IsNullOrWhiteSpace(factura.JsonDte))
                    return OnGetGenerarPDFSinDatos();

                var root = Newtonsoft.Json.Linq.JObject.Parse(factura.JsonDte);
                var tipoEntrada = factura.TipoDteCodigo
                    ?? root["identificacion"]?.Value<string>("tipoDte")
                    ?? factura.TipoDTE.ToString();
                var tipo = _jsonFactory.ObtenerTipoCanonico(tipoEntrada);
                return File(_pdfService.Generar(tipo, factura.JsonDte, factura.SelloRecepcion), "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR CLIENT] Excepción: {ex}");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}

