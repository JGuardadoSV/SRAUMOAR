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

                string CleanHeaderString(string input)
                {
                    if (string.IsNullOrWhiteSpace(input)) return "";
                    input = input.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                    while (input.Contains("  "))
                    {
                        input = input.Replace("  ", " ");
                    }
                    return input.Trim();
                }

                string dir1 = CleanHeaderString(configs.TryGetValue("DireccionLinea1", out var d1) ? d1 : "");
                string dir2 = CleanHeaderString(configs.TryGetValue("DireccionLinea2", out var d2) ? d2 : "");
                string dir3 = CleanHeaderString(configs.TryGetValue("DireccionLinea3", out var d3) ? d3 : "");
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
                        .ThenInclude(c => c.Facultad)
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
                float anchoCiclo = 1.2f;
                float anchoEstatus = 2.0f; // Aumentado para que "ESTATUS" no se divida
                float anchoCodigo = 1.4f;
                float anchoNombre = 5.4f;  // Ajustado para dar más espacio a Estatus manteniendo la suma en 16.4f
                float anchoUV = 0.8f;
                float anchoNota = 0.8f;
                float anchoLetra = 2.8f;   // Ajustado para dar espacio a Estatus
                float anchoResultado = 2.0f; // Aumentado para que "RESULTADO" no se corte
                
                // ============================================
                // CÁLCULOS ESTADÍSTICOS Y DATOS DINÁMICOS
                // ============================================
                decimal totalUV = historialCiclos.Sum(hc => hc.MateriasHistorial?.Sum(hm => 
                    hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0)) ?? 0);
                
                decimal sumaPromedioPorUV = historialCiclos.Sum(hc => 
                    hc.MateriasHistorial?.Sum(hm => 
                    {
                        decimal uv = hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0);
                        return hm.Promedio * uv;
                    }) ?? 0);
                
                decimal cumVal = 0;
                if (totalUV > 0)
                {
                    cumVal = Math.Round(sumaPromedioPorUV / totalUV, 1);
                }

                // Cantidad de materias cursadas y aprobadas (Promedio >= 7.0 o Aprobada == true)
                int totalMateriasAprobadas = historialCiclos
                    .SelectMany(hc => hc.MateriasHistorial ?? new List<HistorialMateria>())
                    .Count(m => m.Promedio >= 7.0m || m.Aprobada);

                string totalMateriasAprobadasLetras = ConvertirEnteroALetras(totalMateriasAprobadas).ToLower();

                // Género del alumno
                string interesadaTexto = alumno.Genero == 1 ? "la interesada" : "el interesado";

                // Plantilla de respaldo para la introducción si no está configurada en la base de datos
                if (string.IsNullOrWhiteSpace(intro))
                {
                    intro = "El infrascrito Secretario General de la Universidad Monseñor Oscar Arnulfo Romero, distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango, CERTIFICA QUE: {nombrealumno}, con carnet No. {carnet}, es {alumnoalumna} de la {facultad} en la carrera {carrera}, habiendo cursado y aprobado las asignaturas detalladas a continuación:";
                }

                // Limpiar saltos de línea y formatear espacios/puntuación
                intro = intro.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                intro = intro.Replace(",", ", ").Replace(":", ": ");
                while (intro.Contains("  "))
                {
                    intro = intro.Replace("  ", " ");
                }
                intro = intro.Trim();

                // Reemplazar los comodines dinámicos
                string nombreCompleto = $"{alumno.Nombres} {alumno.Apellidos}".ToUpper().Trim();
                string carnet = (alumno.Carnet ?? "").Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(carnet) && !string.IsNullOrWhiteSpace(alumno.Email))
                {
                    int atIndex = alumno.Email.IndexOf('@');
                    if (atIndex > 0)
                    {
                        carnet = alumno.Email.Substring(0, atIndex).Trim().ToUpper();
                    }
                }
                string alumnoAlumna = alumno.Genero == 1 ? "alumna" : "alumno";
                string facultad = (alumno.Carrera?.Facultad?.NombreFacultad ?? "").Trim().ToUpper();
                string carrera = (alumno.Carrera?.NombreCarrera ?? "").Trim().ToUpper();

                intro = intro
                    .Replace("{nombrealumno}", nombreCompleto, StringComparison.OrdinalIgnoreCase)
                    .Replace("{carnet}", carnet, StringComparison.OrdinalIgnoreCase)
                    .Replace("{alumnoalumna}", alumnoAlumna, StringComparison.OrdinalIgnoreCase)
                    .Replace("{facultad}", facultad, StringComparison.OrdinalIgnoreCase)
                    .Replace("{carrera}", carrera, StringComparison.OrdinalIgnoreCase);

                // Nuevos parámetros con fallbacks seguros
                string rectoraNombre = configs.TryGetValue("RectoraNombre", out var rNom) ? rNom : "LICDA. CARMEN NAVAS ESCOBAR DE MEJÍA";
                string rectoraCargo = configs.TryGetValue("RectoraCargo", out var rCar) ? rCar : "RECTORA";
                string confrontadoPor = configs.TryGetValue("ConfrontadoPor", out var conf) ? conf : "LIC. JOSE AUGUSTO HERNANDEZ GONZALEZ";
                string lugarExpedicion = configs.TryGetValue("LugarExpedicion", out var lug) ? lug : "el distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango";
                string rectoraCertificacionTemplate = configs.TryGetValue("RectoraCertificacion", out var rCert) ? rCert : "La infrascrita, Rectora de la Universidad Monseñor Oscar Arnulfo Romero, certifica que la firma que aparece al pie de la certificación global de notas es auténtica y es la misma que usa el {SecretarioNombre}, {SecretarioCargo} de esta universidad.";

                // Fecha actual en letras
                string fechaTexto = ConvertirFechaALetras(DateTime.Now);
                // ============================================

                // Generar PDF usando QuestPDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(30);
                        page.PageColor(Colors.White);
                        
                        // Tipografía por defecto "Arial"
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                        // FOOTER
                        page.Footer().AlignCenter().PaddingBottom(10).Text(x =>
                        {
                            x.Span("Página ").FontSize(9);
                            x.CurrentPageNumber().FontSize(9);
                            x.Span(" de ").FontSize(9);
                            x.TotalPages().FontSize(9);
                        });

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
                                    row.ConstantItem(60).Image(logoPath);
                                }
                                else
                                {
                                    row.ConstantItem(60).Text("");
                                }

                                // Texto centrado
                                row.RelativeItem().AlignCenter().Column(textCol =>
                                {
                                    textCol.Item().AlignCenter().Text("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                                        .Bold()
                                        .FontSize(14)
                                        .AlignCenter();
                                    
                                    if (!string.IsNullOrWhiteSpace(dir1))
                                        textCol.Item().AlignCenter().Text(dir1).FontSize(10f).AlignCenter();
                                    if (!string.IsNullOrWhiteSpace(dir2))
                                        textCol.Item().AlignCenter().Text(dir2).FontSize(10f).AlignCenter();
                                    if (!string.IsNullOrWhiteSpace(dir3))
                                        textCol.Item().AlignCenter().Text(dir3).FontSize(10f).AlignCenter();
                                });
                            });

                            // Linea horizontal de punta a punta
                            col.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(Colors.Black);

                            // Párrafo introductorio
                            if (!string.IsNullOrWhiteSpace(intro))
                            {
                                col.Item().PaddingTop(15).Text(intro)
                                    .Bold()
                                    .FontSize(9.5f)
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

                                    header.Cell().Element(c => HeaderStyle(c, 0)).Text("CICLO").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 1)).Text("ESTATUS").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 2)).Text("CODIGO").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 3)).Text("NOMBRE DE LA ASIGNATURA").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 4)).Text("U.V.").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().ColumnSpan(2).Element(c => HeaderStyle(c, 5)).Text("CALIFICACIÓN").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
                                    header.Cell().Element(c => HeaderStyle(c, 7)).Text("RESULTADO").Bold().FontSize(tamanoFuenteHeader).AlignCenter();
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
                                            string cicloFormateado = ciclo.CicloTexto;
                                            if (cicloFormateado.Length == 7 && cicloFormateado[2] == '-')
                                            {
                                                cicloFormateado = $"{cicloFormateado.Substring(3)}-{cicloFormateado.Substring(0, 2)}";
                                            }
                                            table.Cell().Element(c => CellStyle(c, 0)).Text(cicloFormateado).FontSize(tamanoFuenteCiclo).AlignCenter();
                                            esPrimeraMateriaDelCiclo = false;
                                        }
                                        else
                                        {
                                            table.Cell().Element(c => CellStyle(c, 0)).Text("");
                                        }

                                        // Estatus
                                        string estatusTexto = "";
                                        if (materia.ExamenSuficiencia)
                                        {
                                            estatusTexto = "4";
                                        }
                                        else if (materia.Equivalencia)
                                        {
                                            estatusTexto = materia.EsEquivalenciaInterna ? "2" : "1";
                                        }

                                        table.Cell().Element(c => CellStyle(c, 1)).Text(estatusTexto).FontSize(tamanoFuenteCiclo).AlignCenter();

                                        // CODIGO
                                        string codigo = materia.Materia != null 
                                            ? materia.Materia.CodigoMateria 
                                            : materia.MateriaCodigoLibre ?? "-";
                                        table.Cell().Element(c => CellStyle(c, 2)).Text(codigo).FontSize(tamanoFuenteCodigo).AlignCenter();

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

                                // Fila de CUM
                                table.Cell().ColumnSpan(5)
                                    .BorderTop(0.5f).BorderColor(Colors.Black)
                                    .BorderRight(0.5f).BorderColor(Colors.Black)
                                    .PaddingVertical(3).PaddingHorizontal(2)
                                    .AlignCenter()
                                    .Text("COEFICIENTE DE UNIDADES MÉRITO (CUM) :").Bold().FontSize(9);

                                string cumNumerico = cumVal.ToString("0.0");
                                string cumLetras = ConvertirNumeroALetras(cumVal);

                                table.Cell()
                                    .BorderTop(0.5f).BorderColor(Colors.Black)
                                    .BorderRight(0.5f).BorderColor(Colors.Black)
                                    .PaddingVertical(3).PaddingHorizontal(2)
                                    .AlignCenter()
                                    .Text(cumNumerico).Bold().FontSize(9);

                                table.Cell()
                                    .BorderTop(0.5f).BorderColor(Colors.Black)
                                    .BorderRight(0.5f).BorderColor(Colors.Black)
                                    .PaddingVertical(3).PaddingHorizontal(2)
                                    .AlignCenter()
                                    .Text(cumLetras).Bold().FontSize(9);

                                table.Cell()
                                    .BorderTop(0.5f).BorderColor(Colors.Black)
                                    .PaddingVertical(3).PaddingHorizontal(2)
                                    .Text("");
                            });

                            // 1. Cuadros de Leyendas (Lado a Lado)
                            col.Item().PaddingTop(15).Row(row =>
                            {
                                // Caja izquierda: Ciclo y Estatus
                                row.RelativeItem().Border(0.5f).BorderColor(Colors.Black).Padding(5).Column(boxCol =>
                                {
                                    boxCol.Item().Row(r =>
                                    {
                                        r.RelativeItem(1.2f).Text(t => t.Span("CICLO:").Bold().FontSize(7.5f));
                                        r.RelativeItem(2.8f).Text(t => { t.Span("ESTATUS: ").Bold().FontSize(7.5f); t.Span("1-EQUIVALENCIAS EXTERNA").FontSize(7.5f); });
                                    });
                                    boxCol.Item().Row(r =>
                                    {
                                        r.RelativeItem(1.2f).Text(t => t.Span(" 1- PRIMERO").FontSize(7.5f));
                                        r.RelativeItem(2.8f).Text(t => t.Span("         2-EQUIVALENCIAS INTERNAS").FontSize(7.5f));
                                    });
                                    boxCol.Item().Row(r =>
                                    {
                                        r.RelativeItem(1.2f).Text(t => t.Span(" 2- SEGUNDO").FontSize(7.5f));
                                        r.RelativeItem(2.8f).Text(t => t.Span("         3-PLAN DE ABSORCIÓN").FontSize(7.5f));
                                    });
                                    boxCol.Item().Row(r =>
                                    {
                                        r.RelativeItem(1.2f).Text(t => t.Span(" 3- TERCERO").FontSize(7.5f));
                                        r.RelativeItem(2.8f).Text(t => t.Span("         4-SUFICIENCIA").FontSize(7.5f));
                                    });
                                });

                                // Espaciador
                                row.ConstantItem(15);

                                // Caja derecha: U.V.
                                row.RelativeItem().Border(0.5f).BorderColor(Colors.Black).Padding(5).Text(t =>
                                {
                                    t.Span("U.V. ").Bold().FontSize(7.5f);
                                    t.Span("Sistema de Unidades Valorativas que cuantifica los créditos académicos acumulados por cada estudiante, en base al esfuerzo realizado durante el estudio de la carrera.").FontSize(7.5f);
                                });
                            });

                            // 2. Escala de Calificación (Ancho Completo)
                            col.Item().PaddingTop(10).Border(0.5f).BorderColor(Colors.Black).Padding(5).Text(t =>
                            {
                                t.AlignCenter();
                                t.Span("LA ESCALA DE CALIFICACIÓN ES DE CERO PUNTO CERO (0.0) A DIEZ PUNTO CERO (10.0); LA NOTA MÍNIMA DE APROBACIÓN ES DE SIETE PUNTO CERO (7.0)").Bold().FontSize(8f);
                            });

                            // 3. Párrafo de Cierre 1: Asignaturas amparadas
                            col.Item().PaddingTop(15).Text(t =>
                            {
                                t.Justify();
                                t.Span("Esta certificación global de notas ampara ").FontSize(9.5f);
                                t.Span($"{totalMateriasAprobadasLetras} ({totalMateriasAprobadas})").Bold().FontSize(9.5f);
                                t.Span(" asignaturas cursadas y aprobadas.").FontSize(9.5f);
                            });

                            // 4. Párrafo de Cierre 2: Extensión del documento
                            col.Item().PaddingTop(15).Text(t =>
                            {
                                t.Justify();
                                t.Span("Y para los usos que ").FontSize(9.5f);
                                t.Span(interesadaTexto).FontSize(9.5f);
                                t.Span(" estime conveniente, se le extiende la presente en ").FontSize(9.5f);
                                t.Span(lugarExpedicion).FontSize(9.5f);
                                t.Span($", {fechaTexto}.").FontSize(9.5f);
                            });

                            // 5. Bloque de Firma del Secretario General
                            if (!string.IsNullOrWhiteSpace(firmaNombre) || !string.IsNullOrWhiteSpace(firmaCargo))
                            {
                                col.Item().PaddingTop(25).ShowEntire().Border(0.5f).BorderColor(Colors.Black).MinHeight(90).Padding(10).Column(sgCol =>
                                {
                                    sgCol.Item().Height(30); // Espacio en blanco para firma física y sello
                                    sgCol.Item().AlignCenter().Text(t => t.Span(firmaNombre).Bold().FontSize(9f));
                                    sgCol.Item().AlignCenter().Text(t => t.Span(firmaCargo).FontSize(9f));
                                    if (!string.IsNullOrWhiteSpace(firmaSublinea))
                                    {
                                        sgCol.Item().AlignCenter().Text(t => t.Span(firmaSublinea).FontSize(9f));
                                    }
                                });
                            }

                            // 6. Bloque de Certificación y Firma de la Rectora
                            col.Item().PaddingTop(15).ShowEntire().Border(0.5f).BorderColor(Colors.Black).Padding(10).Column(rCol =>
                            {
                                // Texto de certificación dinámico
                                string certText = rectoraCertificacionTemplate
                                    .Replace("{SecretarioNombre}", firmaNombre)
                                    .Replace("{SecretarioCargo}", firmaCargo);

                                rCol.Item().Text(t => { t.Justify(); t.Span(certText).FontSize(9.5f); });
                                
                                // Espacio para la firma de la Rectora
                                rCol.Item().Height(60);

                                // Nombre y cargo de la Rectora
                                rCol.Item().AlignCenter().Text(t => t.Span(rectoraNombre).Bold().FontSize(9f));
                                rCol.Item().AlignCenter().Text(t => t.Span(rectoraCargo).FontSize(9f));

                                // Espacio antes de confrontado
                                rCol.Item().Height(15);

                                // Leyendas de confrontado y validez
                                rCol.Item().Text(t => t.Span($"CONFRONTADO POR: {confrontadoPor}").Bold().FontSize(8.5f));
                                rCol.Item().Text(t => t.Span("ESTA CERTIFICACIÓN ES VALIDA SÓLO CON LA FIRMA Y EL SELLO AUTORIZADOS").Bold().FontSize(8.5f));
                            });
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

            // Multiplicar por 10 para obtener el décimo
            int decimales = (int)Math.Round(parteDecimal * 10);
            resultado += " PUNTO " + ConvertirEnteroALetras(decimales);

            return resultado;
        }

        private string ConvertirFechaALetras(DateTime fecha)
        {
            int dia = fecha.Day;
            string mes = fecha.ToString("MMMM", new CultureInfo("es-SV")).ToLower();
            int anio = fecha.Year;

            string diaLetras = dia == 1 ? "primer" : ConvertirEnteroALetras(dia).ToLower();
            string diaTexto = dia == 1 ? "al primer día" : $"a los {diaLetras} días";

            string anioLetras = ConvertirEnteroALetras(anio).ToLower();

            return $"{diaTexto} del mes de {mes} del año {anioLetras}";
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

