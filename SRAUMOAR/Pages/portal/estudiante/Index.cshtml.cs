using iText.Kernel.Pdf;
using iText.Layout.Element;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Newtonsoft.Json;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
namespace SRAUMOAR.Pages.portal.estudiante
{

    //Allow only Estudiantes rol
    [Authorize(Roles = "Estudiantes")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(SRAUMOAR.Modelos.Contexto context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }
        public Alumno Alumno { get; set; } = default!;
        public Ciclo Ciclo { get; set; } = default!;
        [BindProperty]
        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<Arancel> Arancel { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademica { get; set; } = default!;
        public IList<DetallesCobroArancel> DetallesCobroArancel { get; set; } = default!;
        public IActionResult OnGet()
        {
            var userId = User.FindFirstValue("UserId") ?? "0"; 
            int idusuario = int.Parse(userId);
            int rol = _context.Usuarios.Where(x => x.IdUsuario == idusuario).First().NivelAccesoId;
            this.Alumno = _context.Alumno.Include(c=>c.Carrera).Where(c=>c.UsuarioId == idusuario).First();

            Ciclo = _context.Ciclos.Where(x => x.Activo == true).First();

            //seleccionar todas las materias inscritas por el alumno
            MateriasInscritas = _context.MateriasInscritas
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Materia)
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Docente)
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Grupo)  // Agregamos esta línea para incluir el Grupo
     .Where(mi => mi.MateriasGrupo.Grupo.CicloId == Ciclo.Id &&
                  mi.Alumno.AlumnoId == Alumno.AlumnoId)
     .ToList();

            // Consulta modificada para manejar múltiples pagos
            Arancel =  _context.Aranceles.Where(x => x.Ciclo.Id == Ciclo.Id)
                 .Include(a => a.Ciclo).ToList();

            DetallesCobroArancel = _context.DetallesCobrosArancel
                .Include(x => x.CobroArancel)
                .Include(x => x.Arancel)
                .Where(x => x.CobroArancel.CicloId == Ciclo.Id && x.CobroArancel.AlumnoId == Alumno.AlumnoId).ToList();


            ActividadAcademica =  _context.ActividadesAcademicas
                .Include(a => a.Arancel)
                .Include(a => a.Ciclo).Where(c => c.CicloId == Ciclo.Id).ToList();
            // var alumnos = _context.Alumno.Include(c => c.Carrera).Where(c => c.UsuarioId == idusuario).First();


            // var x = 10;
            return Page();

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
                    .Include(c => c.Alumno)
                    .Include(c => c.Ciclo)
                    .FirstOrDefaultAsync(c => c.CobroArancelId == id);

                if ((cobroArancel==null))
                {

                    return OnGetGenerarPDFSinDatos();
                }


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
                    tipoDte = tipo
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
