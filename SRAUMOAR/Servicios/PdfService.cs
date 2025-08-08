
    using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using System.Text;
using SRAUMOAR.Pages.reportes.matricula_sin_inscripcion;

namespace SRAUMOAR.Servicios
{
    public class PdfService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public PdfService(IServiceProvider serviceProvider, IRazorViewEngine razorViewEngine, ITempDataProvider tempDataProvider)
        {
            _serviceProvider = serviceProvider;
            _razorViewEngine = razorViewEngine;
            _tempDataProvider = tempDataProvider;
        }

        public async Task<string> RenderViewToStringAsync<T>(string viewName, T model, ViewDataDictionary viewData = null)
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            using var sw = new StringWriter();
            var viewResult = _razorViewEngine.FindView(actionContext, viewName, false);

            if (!viewResult.Success)
            {
                throw new ArgumentNullException($"No se encontró la vista '{viewName}'");
            }

            var viewDictionary = viewData ?? new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            viewDictionary.Model = model;

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }

        public async Task<string> GenerarHtmlReporteMatriculaSinInscripcion(
            List<IndexModel.AlumnoConMatricula> alumnos,
            int totalAlumnos,
            decimal totalMatricula,
            int? carreraId,
            bool incluirAlumnosConBeca)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Reporte de Alumnos con Matrícula Sin Inscripción</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #333; text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; }");
            html.AppendLine("h2 { color: #666; margin-top: 20px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("th { background-color: #f2f2f2; font-weight: bold; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine(".text-right { text-align: right; }");
            html.AppendLine(".text-center { text-align: center; }");
            html.AppendLine(".summary { background-color: #e7f3ff; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            html.AppendLine(".filters { background-color: #fff3cd; padding: 10px; border-radius: 5px; margin: 10px 0; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<h1>Reporte de Alumnos con Matrícula Sin Inscripción</h1>");
            html.AppendLine($"<p class='text-center'><strong>Fecha de generación:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
            
            // Información de filtros aplicados
            html.AppendLine("<div class='filters'>");
            html.AppendLine("<h3>Filtros Aplicados:</h3>");
            html.AppendLine("<ul>");
            if (carreraId.HasValue && carreraId.Value > 0)
            {
                html.AppendLine($"<li><strong>Carrera:</strong> Filtrada por ID: {carreraId.Value}</li>");
            }
            else
            {
                html.AppendLine("<li><strong>Carrera:</strong> Todas las carreras</li>");
            }
            html.AppendLine($"<li><strong>Incluir alumnos con beca:</strong> {(incluirAlumnosConBeca ? "Sí" : "No")}</li>");
            html.AppendLine("</ul>");
            html.AppendLine("</div>");
            
            // Resumen
            html.AppendLine("<div class='summary'>");
            html.AppendLine("<h2>Resumen del Reporte</h2>");
            html.AppendLine($"<p><strong>Total de alumnos con matrícula sin inscripción:</strong> {totalAlumnos}</p>");
            html.AppendLine($"<p><strong>Total de matrículas pagadas:</strong> ${totalMatricula:F2}</p>");
            html.AppendLine("</div>");
            
            if (alumnos.Any())
            {
                html.AppendLine("<h2>Detalle de Alumnos</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<thead>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>No.</th>");
                html.AppendLine("<th>Nombre Completo</th>");
                html.AppendLine("<th>Carrera</th>");
                html.AppendLine("<th>Carnet</th>");
                html.AppendLine("<th>Email</th>");
                html.AppendLine("<th>Fecha Pago Matrícula</th>");
                html.AppendLine("<th class='text-right'>Monto Matrícula</th>");
                html.AppendLine("<th>Código Generación</th>");
                html.AppendLine("</tr>");
                html.AppendLine("</thead>");
                html.AppendLine("<tbody>");
                
                for (int i = 0; i < alumnos.Count; i++)
                {
                    var alumno = alumnos[i];
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{i + 1}</td>");
                    html.AppendLine($"<td>{alumno.Apellidos}, {alumno.Nombres}</td>");
                    html.AppendLine($"<td>{alumno.Carrera}</td>");
                    html.AppendLine($"<td>{alumno.Carnet}</td>");
                    html.AppendLine($"<td>{alumno.Email}</td>");
                    html.AppendLine($"<td>{alumno.FechaPagoMatricula:dd/MM/yyyy}</td>");
                    html.AppendLine($"<td class='text-right'>${alumno.MontoMatricula:F2}</td>");
                    html.AppendLine($"<td>{alumno.CodigoGeneracion}</td>");
                    html.AppendLine("</tr>");
                }
                
                html.AppendLine("</tbody>");
                html.AppendLine("</table>");
            }
            else
            {
                html.AppendLine("<div class='summary'>");
                html.AppendLine("<h2>No se encontraron resultados</h2>");
                html.AppendLine("<p>No hay alumnos que cumplan con los criterios especificados.</p>");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
    }
}
