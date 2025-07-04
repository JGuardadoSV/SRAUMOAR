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

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class FacturasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public FacturasModel(SRAUMOAR.Modelos.Contexto context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
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
                // Aquí puedes agregar tu lógica para obtener el JSON y sello
                // Por ahora uso valores de ejemplo
                CobroArancel cobroArancel = await _context.CobrosArancel
                    .Include(c => c.Alumno).ThenInclude(a => a.Carrera)
                    .Include(c => c.Ciclo)
                    .FirstOrDefaultAsync(c => c.CobroArancelId == id);

                Alumno alumno = cobroArancel.Alumno;

                Factura factura = await _context.Facturas.FirstOrDefaultAsync(f => f.CodigoGeneracion == cobroArancel.CodigoGeneracion);
                if (string.IsNullOrWhiteSpace(factura?.JsonDte))
                {

                   return OnGetGenerarPDFSinDatos();
                }
              

                var dteJson = factura.JsonDte; // Reemplazar con tu lógica
                var selloRecibido = factura.SelloRecepcion; // Reemplazar con tu lógica
                var tipo = factura.TipoDTE.ToString().PadLeft(2, '0');

                // Datos que necesitas enviar
                var requestData = new
                {
                    dteJson = dteJson,
                    selloRecibido = selloRecibido,
                    tipoDte =tipo,
                    carrera= alumno?.Carrera?.NombreCarrera ?? "-",
                    observacion=cobroArancel.nota ?? "-",
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                //var response = await client.PostAsync("https://localhost:7122/api/generar-pdf", content);
                var response = await client.PostAsync("http://207.58.153.147:7122/api/generar-pdf", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();

                    //Console.WriteLine($"[DEBUG CLIENT] PDF recibido, tamaño: {pdfBytes.Length} bytes");

                    // Respuesta más simple - deja que la API maneje los headers
                    return File(pdfBytes, "application/pdf");
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR CLIENT] Error de API: {errorMessage}");
                    TempData["Error"] = $"Error al generar PDF: {errorMessage}";
                    return RedirectToPage();
                }
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
