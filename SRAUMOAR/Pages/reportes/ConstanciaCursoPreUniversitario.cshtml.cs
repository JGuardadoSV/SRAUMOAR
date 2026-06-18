using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SRAUMOAR.Modelos;
using System.Globalization;
using System.IO;

namespace SRAUMOAR.Pages.reportes
{
    public class ConstanciaCursoPreUniversitarioModel : PageModel
    {
        private readonly Contexto _context;

        public ConstanciaCursoPreUniversitarioModel(Contexto context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int? alumnoId, DateTime? fechaInicio, DateTime? fechaFin, DateTime? fechaExpedicion = null)
        {
            try
            {
                if (!alumnoId.HasValue || alumnoId.Value <= 0)
                {
                    return BadRequest("Debe especificar un alumno.");
                }

                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                {
                    return BadRequest("Debe especificar la fecha de inicio y la fecha de finalización del curso.");
                }

                if (fechaFin.Value.Date < fechaInicio.Value.Date)
                {
                    return BadRequest("La fecha de finalización no puede ser anterior a la fecha de inicio.");
                }

                var alumno = await _context.Alumno
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId.Value);

                if (alumno == null)
                {
                    return NotFound("Alumno no encontrado.");
                }

                var configs = await CargarConfiguracionesAsync();

                string dir1 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea1", ""));
                string dir2 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea2", ""));
                string dir3 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea3", ""));

                string tituloArea = GetConfig(configs, "ConstanciaCursoPreUniversitario", "TituloArea", "RECTORIA");
                string tituloAreaEspaciado = EspaciarTitulo(tituloArea);
                string tituloReporte = GetConfig(configs, "ConstanciaCursoPreUniversitario", "TituloReporte", "CONSTANCIA DE CURSO PRE UNIVERSITARIO");
                string lugarExpedicion = GetConfig(configs, "ConstanciaCursoPreUniversitario", "LugarExpedicion", "Tejutla, Chalatenango");
                string firmaNombre = GetConfig(configs, "ConstanciaCursoPreUniversitario", "FirmaNombre", "");
                string firmaCargo = GetConfig(configs, "ConstanciaCursoPreUniversitario", "FirmaCargo", "RECTOR");

                string cuerpo = GetConfig(configs, "ConstanciaCursoPreUniversitario", "Cuerpo",
                    "El Infrascrito Rector de la Universidad Monseñor Oscar Arnulfo Romero, por la presente hace constar que: {nombrealumno}, realizó y aprobó su curso pre-universitario, impartido en esta institución desde el día {fechaInicioCurso} al {fechaFinCurso}.");

                string cierre = GetConfig(configs, "ConstanciaCursoPreUniversitario", "Cierre",
                    "Y para ser presentada a la Administración de Registro Académico, se extiende la presente Constancia, en {lugarExpedicion} {fechaExpedicion}.");

                var fechaDocumento = fechaExpedicion?.Date ?? DateTime.Now.Date;
                string nombreCompleto = $"{alumno.Nombres} {alumno.Apellidos}".ToUpper().Trim();

                cuerpo = ReemplazarPlaceholders(cuerpo, nombreCompleto, fechaInicio.Value.Date, fechaFin.Value.Date, fechaDocumento, lugarExpedicion);
                cierre = ReemplazarPlaceholders(cierre, nombreCompleto, fechaInicio.Value.Date, fechaFin.Value.Date, fechaDocumento, lugarExpedicion);

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(45);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(12));

                        page.Content().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                                if (System.IO.File.Exists(logoPath))
                                {
                                    row.ConstantItem(62).Image(logoPath);
                                }
                                else
                                {
                                    row.ConstantItem(62).Text("");
                                }

                                row.RelativeItem().AlignCenter().Column(textCol =>
                                {
                                    textCol.Item().AlignCenter().Text("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                                        .Bold()
                                        .FontSize(14);

                                    if (!string.IsNullOrWhiteSpace(dir1))
                                        textCol.Item().AlignCenter().Text(dir1).FontSize(9.5f);
                                    if (!string.IsNullOrWhiteSpace(dir2))
                                        textCol.Item().AlignCenter().Text(dir2).FontSize(9.5f);
                                    if (!string.IsNullOrWhiteSpace(dir3))
                                        textCol.Item().AlignCenter().Text(dir3).FontSize(9.5f);
                                });
                            });

                            col.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(Colors.Black);

                            col.Item().PaddingTop(4).AlignCenter().Text(tituloAreaEspaciado)
                                .Bold()
                                .FontSize(13);

                            col.Item().PaddingTop(28).AlignCenter().Text(tituloReporte)
                                .Bold()
                                .FontSize(13);

                            col.Item().PaddingTop(55).Text(cuerpo)
                                .FontSize(12)
                                .LineHeight(1.65f)
                                .Justify();

                            col.Item().PaddingTop(28).Text(cierre)
                                .FontSize(12)
                                .LineHeight(1.65f)
                                .Justify();

                            col.Item().PaddingTop(85).ShowEntire().Column(firma =>
                            {
                                firma.Item().AlignCenter().Width(260).LineHorizontal(1).LineColor(Colors.Black);

                                if (!string.IsNullOrWhiteSpace(firmaNombre))
                                {
                                    firma.Item().PaddingTop(5).AlignCenter().Text(firmaNombre.ToUpper()).Bold().FontSize(10);
                                }

                                if (!string.IsNullOrWhiteSpace(firmaCargo))
                                {
                                    firma.Item().AlignCenter().Text(firmaCargo.ToUpper()).FontSize(10);
                                }
                            });
                        });
                    });
                });

                return File(document.GeneratePdf(), "application/pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar PDF: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, Dictionary<string, string>>> CargarConfiguracionesAsync()
        {
            try
            {
                return await _context.ConfiguracionesReportes
                    .AsNoTracking()
                    .Where(c => c.Reporte == "CertificacionNotas" || c.Reporte == "ConstanciaCursoPreUniversitario")
                    .GroupBy(c => c.Reporte)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.ToDictionary(c => c.Clave, c => c.Valor ?? ""));
            }
            catch
            {
                return new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private static string GetConfig(Dictionary<string, Dictionary<string, string>> configs, string reporte, string clave, string defaultValue)
        {
            if (configs.TryGetValue(reporte, out var reporteConfigs) &&
                reporteConfigs.TryGetValue(clave, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return defaultValue;
        }

        private static string CleanHeaderString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            input = input.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            while (input.Contains("  "))
            {
                input = input.Replace("  ", " ");
            }

            return input.Trim();
        }

        private static string ReemplazarPlaceholders(
            string plantilla,
            string nombreAlumno,
            DateTime fechaInicio,
            DateTime fechaFin,
            DateTime fechaExpedicion,
            string lugarExpedicion)
        {
            return CleanHeaderString(plantilla)
                .Replace("{nombrealumno}", nombreAlumno, StringComparison.OrdinalIgnoreCase)
                .Replace("{fechaInicioCurso}", FormatearFechaCurso(fechaInicio), StringComparison.OrdinalIgnoreCase)
                .Replace("{fechaFinCurso}", FormatearFechaCurso(fechaFin), StringComparison.OrdinalIgnoreCase)
                .Replace("{fechaExpedicion}", ConvertirFechaALetras(fechaExpedicion), StringComparison.OrdinalIgnoreCase)
                .Replace("{lugarExpedicion}", lugarExpedicion, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatearFechaCurso(DateTime fecha)
        {
            var cultura = new CultureInfo("es-SV");
            return fecha.ToString("dddd dd 'de' MMMM 'de' yyyy", cultura).ToLower();
        }

        private static string EspaciarTitulo(string titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return "";
            return string.Join(" ", titulo.Trim().ToUpper().ToCharArray());
        }

        private static string ConvertirFechaALetras(DateTime fecha)
        {
            int dia = fecha.Day;
            string mes = fecha.ToString("MMMM", new CultureInfo("es-SV")).ToLower();
            int anio = fecha.Year;

            string diaLetras = dia == 1 ? "primer" : ConvertirEnteroALetras(dia).ToLower();
            string diaTexto = dia == 1 ? "al primer día" : $"a los {diaLetras} días";
            string anioLetras = ConvertirEnteroALetras(anio).ToLower();

            return $"{diaTexto} del mes de {mes} del año {anioLetras}";
        }

        private static string ConvertirEnteroALetras(int numero)
        {
            if (numero == 0) return "CERO";
            if (numero < 0) return "MENOS " + ConvertirEnteroALetras(-numero);

            string resultado = "";

            if (numero >= 1000)
            {
                int miles = numero / 1000;
                resultado = ConvertirEnteroALetras(miles) + " MIL";
                numero %= 1000;
                if (numero > 0) resultado += " ";
            }

            if (numero >= 100)
            {
                int centenas = numero / 100;
                if (centenas == 1 && numero % 100 == 0)
                {
                    resultado += "CIEN";
                    return resultado;
                }

                resultado += centenas == 1
                    ? "CIENTO"
                    : new[] { "", "", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" }[centenas];

                numero %= 100;
                if (numero > 0) resultado += " ";
            }

            if (numero >= 1 && numero <= 15)
            {
                string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE" };
                resultado += unidades[numero];
            }
            else if (numero >= 16 && numero < 20)
            {
                resultado += "DIECI" + ConvertirUnidad(numero % 10);
            }
            else if (numero >= 20 && numero < 30)
            {
                resultado += numero == 20 ? "VEINTE" : "VEINTI" + ConvertirUnidad(numero % 10);
            }
            else if (numero >= 30)
            {
                int decenas = numero / 10;
                int unidades = numero % 10;
                string[] decenasTexto = { "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
                resultado += decenasTexto[decenas];
                if (unidades > 0)
                {
                    resultado += " Y " + ConvertirUnidad(unidades);
                }
            }

            return resultado.Trim();
        }

        private static string ConvertirUnidad(int numero)
        {
            if (numero == 0) return "";
            string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            return unidades[numero];
        }
    }
}
