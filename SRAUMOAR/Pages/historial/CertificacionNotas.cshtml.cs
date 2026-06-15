using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SRAUMOAR.Pages.historial
{
    public class CertificacionNotasModel : PageModel
    {
        private readonly Contexto _context;

        public CertificacionNotasModel(Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet(int? alumnoId, int? carreraId = null)
        {
            try
            {
                // Obtener configuraciones de reportes de manera segura
                var configs = new Dictionary<string, string>();
                try
                {
                    configs = _context.ConfiguracionesReportes
                        .Where(c => c.Reporte == "CertificacionNotas")
                        .ToDictionary(c => c.Clave, c => c.Valor);
                }
                catch
                {
                    // Fallback a diccionario vacío si la tabla aún no existe en la base de datos
                }

                string dir1 = configs.TryGetValue("DireccionLinea1", out var d1) ? d1 : "";
                string dir2 = configs.TryGetValue("DireccionLinea2", out var d2) ? d2 : "";
                string dir3 = configs.TryGetValue("DireccionLinea3", out var d3) ? d3 : "";
                string intro = configs.TryGetValue("Introduccion", out var inText) ? inText : "";
                string firmaNombre = configs.TryGetValue("FirmaNombre", out var fNom) ? fNom : "";
                string firmaCargo = configs.TryGetValue("FirmaCargo", out var fCar) ? fCar : "";
                string firmaSublinea = configs.TryGetValue("FirmaSublinea", out var fSub) ? fSub : "";

                if (alumnoId == null)
                {
                    return BadRequest("Debe especificar un alumno");
                }

                // Obtener información del alumno
                var alumno = _context.Alumno
                    .Include(a => a.Carrera)
                    .FirstOrDefault(a => a.AlumnoId == alumnoId.Value);

                if (alumno == null)
                {
                    return NotFound("Alumno no encontrado");
                }

                // Obtener historial académico del alumno
                var historialAcademico = _context.HistorialAcademico
                    .Include(h => h.CiclosHistorial)
                        .ThenInclude(hc => hc.Pensum)
                    .Include(h => h.CiclosHistorial)
                        .ThenInclude(hc => hc.MateriasHistorial)
                            .ThenInclude(hm => hm.Materia)
                    .Include(h => h.Carrera)
                    .Where(h => h.AlumnoId == alumnoId.Value)
                    .ToList();

                // Si se especifica carrera, filtrar por ella
                if (carreraId.HasValue)
                {
                    historialAcademico = historialAcademico
                        .Where(h => h.CarreraId == carreraId.Value)
                        .ToList();
                }

                if (!historialAcademico.Any())
                {
                    return BadRequest("No se encontró historial académico para este alumno");
                }

                // Obtener todos los ciclos y materias
                var historialCiclos = historialAcademico
                    .SelectMany(h => h.CiclosHistorial ?? new List<HistorialCiclo>())
                    .OrderBy(c => c.CicloTexto)
                    .ToList();

                if (!historialCiclos.Any())
                {
                    return BadRequest("No se encontraron ciclos registrados para este alumno");
                }

                // ============================================
                // CONFIGURACIÓN PERSONALIZABLE DE LA TABLA
                // ============================================
                // Tamaños de fuente
                float tamanoFuenteHeader = 9f;    // Reducido a 9f
                float tamanoFuenteCiclo = 9f;
                float tamanoFuenteCodigo = 9f;
                float tamanoFuenteNombre = 8.5f;  // Reducido para nombres largos
                float tamanoFuenteUV = 9f;
                float tamanoFuenteNota = 9f;
                float tamanoFuenteLetra = 7.5f;   // Reducido para texto largo
                float tamanoFuenteResultado = 9f;
                
                // Anchos de columnas (valores relativos, suman 16.4f)
                float anchoCiclo = 1.0f;
                float anchoEstatus = 1.2f;
                float anchoCodigo = 1.6f;
                float anchoNombre = 5.5f;  // Aumentado para nombres largos
                float anchoUV = 0.9f;
                float anchoNota = 0.9f;
                float anchoLetra = 3.5f;   // Aumentado significativamente para evitar saltos de línea en letras largas
                float anchoResultado = 1.8f;
                
                // ============================================

                // Generar PDF usando QuestPDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(40);
                        page.PageColor(Colors.White);
                        
                        // Tipografía por defecto "Times New Roman"
                        page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(10));

                        // CONTENIDO
                        page.Content().Column(col =>
                        {
                            // MEMBRETE DE LA UNIVERSIDAD (Solo en la primera página)
                            col.Item().Row(row =>
                            {
                                // Logo a la izquierda
                                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                                if (System.IO.File.Exists(logoPath))
                                {
                                    row.ConstantItem(80).Image(logoPath);
                                }
                                else
                                {
                                    row.ConstantItem(80).Text("");
                                }

                                // Texto centrado
                                row.RelativeItem().AlignCenter().Column(textCol =>
                                {
                                    textCol.Item().AlignCenter().Text("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                                        .Bold()
                                        .FontSize(14)
                                        .AlignCenter();
                                    
                                    if (!string.IsNullOrWhiteSpace(dir1))
                                        textCol.Item().AlignCenter().Text(dir1).FontSize(11).AlignCenter();
                                    if (!string.IsNullOrWhiteSpace(dir2))
                                        textCol.Item().AlignCenter().Text(dir2).FontSize(11).AlignCenter();
                                    if (!string.IsNullOrWhiteSpace(dir3))
                                        textCol.Item().AlignCenter().Text(dir3).FontSize(11).AlignCenter();
                                });
                            });

                            // Párrafo introductorio
                            if (!string.IsNullOrWhiteSpace(intro))
                            {
                                col.Item().PaddingTop(15).Text(intro)
                                    .Bold()
                                    .FontSize(11)
                                    .Justify();
                            }

                            // Espacio antes de la tabla y la tabla misma (envuelta en un borde externo)
                            col.Item().PaddingTop(15).Border(0.5f).BorderColor(Colors.Black).Table(table =>
                            {
                                // Definición de columnas
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(anchoCiclo);
                                    columns.RelativeColumn(anchoEstatus);
                                    columns.RelativeColumn(anchoCodigo);
                                    columns.RelativeColumn(anchoNombre);
                                    columns.RelativeColumn(anchoUV);
                                    columns.RelativeColumn(anchoNota);
                                    columns.RelativeColumn(anchoLetra);
                                    columns.RelativeColumn(anchoResultado);
                                });

                                // Encabezado de la Tabla
                                table.Header(header =>
                                {
                                    IContainer HeaderStyle(IContainer cellContainer, int columnIndex)
                                    {
                                        var styled = cellContainer.BorderBottom(0.5f).BorderColor(Colors.Black);
                                        if (columnIndex < 7)
                                        {
                                            styled = styled.BorderRight(0.5f).BorderColor(Colors.Black);
                                        }
                                        return styled.PaddingVertical(3).PaddingHorizontal(2);
                                    }

                                    header.Cell().Element(c => HeaderStyle(c, 0)).Text("Ciclo").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 1)).Text("Estatus").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 2)).Text("CODIGO").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 3)).Text("Nombre de la asignatura").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 4)).Text("U.V.").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().ColumnSpan(2).Element(c => HeaderStyle(c, 5)).Text("Calificación").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 7)).Text("Resultado").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                });

                                // Datos de la Tabla
                                string cicloAnterior = "";
                                int totalMaterias = historialCiclos.Sum(c => c.MateriasHistorial?.Count ?? 0);
                                int contadorMaterias = 0;

                                foreach (var ciclo in historialCiclos)
                                {
                                    var materias = ciclo.MateriasHistorial?
                                        .OrderBy(m => m.Materia != null ? m.Materia.NombreMateria : m.MateriaNombreLibre ?? "")
                                        .ToList() ?? new List<HistorialMateria>();

                                    bool esPrimeraMateriaDelCiclo = ciclo.CicloTexto != cicloAnterior;

                                    foreach (var materia in materias)
                                    {
                                        contadorMaterias++;

                                        IContainer CellStyle(IContainer cellContainer, int columnIndex)
                                        {
                                            var styled = cellContainer;
                                            if (columnIndex < 7)
                                            {
                                                styled = styled.BorderRight(0.5f).BorderColor(Colors.Black);
                                            }
                                            return styled.PaddingVertical(2).PaddingHorizontal(2);
                                        }

                                        // Ciclo
                                        if (esPrimeraMateriaDelCiclo)
                                        {
                                            table.Cell().Element(c => CellStyle(c, 0)).Text(ciclo.CicloTexto).FontSize(tamanoFuenteCiclo).AlignCenter();
                                            esPrimeraMateriaDelCiclo = false;
                                        }
                                        else
                                        {
                                            table.Cell().Element(c => CellStyle(c, 0)).Text("");
                                        }

                                        // Estatus (vacío)
                                        table.Cell().Element(c => CellStyle(c, 1)).Text("");

                                        // CODIGO
                                        string codigo = materia.Materia != null 
                                            ? materia.Materia.CodigoMateria 
                                            : materia.MateriaCodigoLibre ?? "-";
                                        table.Cell().Element(c => CellStyle(c, 2)).Text(codigo).FontSize(tamanoFuenteCodigo).AlignLeft();

                                        // Nombre de la asignatura
                                        string nombreMateria = materia.Materia != null 
                                            ? materia.Materia.NombreMateria 
                                            : materia.MateriaNombreLibre ?? "-";
                                        table.Cell().Element(c => CellStyle(c, 3)).Text(nombreMateria).FontSize(tamanoFuenteNombre).AlignLeft();

                                        // U.V.
                                        int uv = materia.Materia != null 
                                            ? materia.Materia.uv 
                                            : materia.MateriaUnidadesValorativasLibre ?? 0;
                                        table.Cell().Element(c => CellStyle(c, 4)).Text(uv.ToString()).FontSize(tamanoFuenteUV).AlignCenter();

                                        // Calificación (Nota y Letra)
                                        decimal promedio = materia.Promedio;
                                        string notaNumerica = promedio.ToString("0.0");
                                        string notaLetras = ConvertirNumeroALetras(promedio);

                                        table.Cell().Element(c => CellStyle(c, 5)).Text(notaNumerica).FontSize(tamanoFuenteNota).AlignCenter();
                                        table.Cell().Element(c => CellStyle(c, 6)).Text(notaLetras).FontSize(tamanoFuenteLetra).AlignLeft();

                                        // Resultado
                                        string resultado = promedio >= 7.0m && promedio <= 10.0m ? "APROBADA" : "REPROBADA";
                                        table.Cell().Element(c => CellStyle(c, 7)).Text(resultado).FontSize(tamanoFuenteResultado).AlignCenter();
                                    }

                                    cicloAnterior = ciclo.CicloTexto;
                                }
                            });

                            // Pie de página de datos del alumno
                            col.Item().PaddingTop(15).Column(alumnoInfoCol =>
                            {
                                alumnoInfoCol.Item().Text($"Alumno: {alumno.Nombres} {alumno.Apellidos}")
                                    .FontSize(10);
                                
                                if (alumno.Carrera != null)
                                {
                                    alumnoInfoCol.Item().Text($"Carrera: {alumno.Carrera.NombreCarrera}")
                                        .FontSize(10);
                                }
                            });

                            // Bloque de Firma si está configurado
                            if (!string.IsNullOrWhiteSpace(firmaNombre) || !string.IsNullOrWhiteSpace(firmaCargo))
                            {
                                col.Item().PaddingTop(30).AlignCenter().Width(250).Column(firmaCol =>
                                {
                                    if (!string.IsNullOrWhiteSpace(firmaNombre))
                                    {
                                        firmaCol.Item().AlignCenter().Text("f. _________________________________________")
                                            .FontSize(10);
                                        
                                        firmaCol.Item().PaddingTop(2).AlignCenter().Text(firmaNombre)
                                            .Bold()
                                            .FontSize(10);
                                    }
                                    
                                    if (!string.IsNullOrWhiteSpace(firmaCargo))
                                    {
                                        firmaCol.Item().PaddingTop(2).AlignCenter().Text(firmaCargo)
                                            .FontSize(10);
                                    }
                                    
                                    if (!string.IsNullOrWhiteSpace(firmaSublinea))
                                    {
                                        firmaCol.Item().PaddingTop(2).AlignCenter().Text(firmaSublinea)
                                            .FontSize(10);
                                    }
                                });
                            }
                        });
                    });
                });

                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar PDF: {ex.Message}");
            }
        }

        private string ConvertirNumeroALetras(decimal numero)
        {
            // Redondear a 1 decimal
            numero = Math.Round(numero, 1);
            
            int parteEntera = (int)Math.Floor(numero);
            decimal parteDecimal = numero - parteEntera;

            string resultado = ConvertirEnteroALetras(parteEntera);

            // Agregar parte decimal si existe
            if (parteDecimal > 0)
            {
                // Convertir la parte decimal correctamente
                // Ejemplo: 7.4 -> parte decimal = 0.4 -> convertir 4
                // Ejemplo: 5.8 -> parte decimal = 0.8 -> convertir 8
                // Ejemplo: 9.0 -> no hay decimal
                
                // Multiplicar por 10 para obtener el décimo
                int decimales = (int)Math.Round(parteDecimal * 10);
                
                // Solo puede ser un dígito (0-9) porque redondeamos a 1 decimal
                if (decimales > 0)
                {
                    resultado += " PUNTO " + ConvertirEnteroALetras(decimales);
                }
            }

            return resultado;
        }

        private string ConvertirEnteroALetras(int numero)
        {
            if (numero == 0) return "CERO";
            if (numero < 0) return "MENOS " + ConvertirEnteroALetras(-numero);

            string resultado = "";

            if (numero >= 1000)
            {
                int miles = numero / 1000;
                resultado = ConvertirEnteroALetras(miles) + " MIL";
                numero = numero % 1000;
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
                else if (centenas == 1)
                {
                    resultado += "CIENTO";
                }
                else
                {
                    string[] centenasTexto = { "", "", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };
                    resultado += centenasTexto[centenas];
                }
                numero = numero % 100;
                if (numero > 0) resultado += " ";
            }

            if (numero >= 1 && numero <= 15)
            {
                string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", 
                                     "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE" };
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

        private string ConvertirUnidad(int numero)
        {
            if (numero == 0) return "";
            string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            return unidades[numero];
        }
    }
}

