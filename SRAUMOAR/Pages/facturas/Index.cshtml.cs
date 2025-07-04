using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using Newtonsoft.Json;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
using SRAUMOAR.Entidades.Alumnos;
using Newtonsoft.Json.Linq;
namespace SRAUMOAR.Pages.facturas
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IHttpClientFactory _httpClientFactory;
        public IndexModel(SRAUMOAR.Modelos.Contexto context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public IList<Factura> Factura { get;set; } = default!;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public string? EstadoFiltro { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PaginaActual { get; set; } = 1;

        public int TotalPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public int RegistrosPorPagina { get; set; } = 20;

        public async Task OnGetAsync()
        {
            var query = _context.Facturas.AsQueryable();

            // Filtro por fechas - por defecto muestra solo las del día actual
            if (FechaInicio.HasValue)
            {
                query = query.Where(f => f.Fecha >= FechaInicio.Value);
            }

            if (FechaFin.HasValue)
            {
                query = query.Where(f => f.Fecha <= FechaFin.Value.AddDays(1).AddSeconds(-1));
            }

            // Filtro por estado
            if (!string.IsNullOrEmpty(EstadoFiltro))
            {
                switch (EstadoFiltro.ToLower())
                {
                    case "activa":
                        query = query.Where(f => !f.Anulada);
                        break;
                    case "anulada":
                        query = query.Where(f => f.Anulada);
                        break;
                }
            }

            // Calcular total de registros
            TotalRegistros = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / RegistrosPorPagina);

            // Aplicar paginación
            var facturasPaginadas = await query
                .OrderByDescending(f => f.Fecha)
                .Skip((PaginaActual - 1) * RegistrosPorPagina)
                .Take(RegistrosPorPagina)
                .ToListAsync();

            Factura = facturasPaginadas;
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
                
                
                Factura factura1 = await _context.Facturas
                    .FirstOrDefaultAsync(c => c.FacturaId == id);
                Factura factura = await _context.Facturas.FirstOrDefaultAsync(f => f.CodigoGeneracion == factura1.CodigoGeneracion);
                if (string.IsNullOrWhiteSpace(factura?.JsonDte))
                {

                    return OnGetGenerarPDFSinDatos();
                }

                CobroArancel cobroArancel = await _context.CobrosArancel
                 .Include(c => c.Alumno).ThenInclude(a => a.Carrera)
                 .Include(c => c.Ciclo)
                 .FirstOrDefaultAsync(c => c.CodigoGeneracion == factura1.CodigoGeneracion);

                bool existeCobro = cobroArancel != null;
                Alumno alumno=new Alumno();
                if (existeCobro)
                {
                     alumno = cobroArancel.Alumno;
                    // Continúa con tu lógica...
                }
                else
                {
                    var jsonObj = JObject.Parse(factura.JsonDte); // OBTENER EL JSON ORIGINAL PARA LEERLO EN CADA SECCION
                                                                  // Sección de Emisor
                    string correo = jsonObj?["receptor"]?["correo"]?.ToString() ?? "";

                    alumno = _context.Alumno.FirstOrDefault(a => a.Email.Equals(correo));
                }

                var dteJson = factura.JsonDte; // Reemplazar con tu lógica
                var selloRecibido = factura.SelloRecepcion; // Reemplazar con tu lógica
                var tipo = factura.TipoDTE.ToString().PadLeft(2, '0');

                // Datos que necesitas enviar
                var requestData = new
                {
                    dteJson = dteJson,
                    selloRecibido = selloRecibido,
                    tipoDte = tipo,
                    carrera = alumno?.Carrera?.NombreCarrera ?? "-",
                    observacion = cobroArancel?.nota??"-"
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
