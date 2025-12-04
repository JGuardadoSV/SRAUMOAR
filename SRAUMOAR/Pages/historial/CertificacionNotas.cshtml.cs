using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using iText.IO.Image;
using System.Globalization;
using System.IO;

namespace SRAUMOAR.Pages.historial
{
    public class CertificacionNotasModel : PageModel
    {
        private readonly Contexto _context;
        private PdfFont _fontNormal;
        private PdfFont _fontBold;

        public CertificacionNotasModel(Contexto context)
        {
            _context = context;
        }

        private void InitializeFonts()
        {
            _fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            _fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
        }

        public IActionResult OnGet(int? alumnoId, int? carreraId = null)
        {
            try
            {
                InitializeFonts();

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

                // Generar PDF
                using var memoryStream = new MemoryStream();
                using var writer = new PdfWriter(memoryStream);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                // Configurar márgenes
                document.SetMargins(40, 40, 40, 40);

                // ENCABEZADO
                var headerTable = new Table(2);
                headerTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Logo a la izquierda
                try
                {
                    var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                    if (System.IO.File.Exists(logoPath))
                    {
                        var logo = new Image(ImageDataFactory.Create(logoPath));
                        logo.SetWidth(80);
                        headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.TOP));
                    }
                    else
                    {
                        headerTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                    }
                }
                catch
                {
                    headerTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                }

                // Texto centrado - Nombre de universidad y direcciones
                var textCell = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
                textCell.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .SetFont(_fontBold)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(5));
                
                // 3 líneas de direcciones (reservar espacio - el usuario las completará)
                textCell.Add(new Paragraph("")
                    .SetFont(_fontNormal)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(3));
                textCell.Add(new Paragraph("")
                    .SetFont(_fontNormal)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(3));
                textCell.Add(new Paragraph("")
                    .SetFont(_fontNormal)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER));

                headerTable.AddCell(textCell);
                document.Add(headerTable);
                document.Add(new Paragraph(" ").SetMarginBottom(10));

                // Línea vertical (usando una línea horizontal como separador)
                document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                    .SetFontSize(8)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(15));

                // Párrafo de texto justificado en negrita (reservar espacio)
                var parrafoTexto = new Paragraph("")
                    .SetFont(_fontBold)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                    .SetMarginBottom(15);
                document.Add(parrafoTexto);

                // ============================================
                // CONFIGURACIÓN PERSONALIZABLE DE LA TABLA
                // ============================================
                // Tamaños de fuente
                float tamanoFuenteHeader = 10f;
                float tamanoFuenteCiclo = 9f;
                float tamanoFuenteCodigo = 9f;
                float tamanoFuenteNombre = 8.5f;  // Reducido para nombres largos
                float tamanoFuenteUV = 9f;
                float tamanoFuenteNota = 9f;
                float tamanoFuenteLetra = 7.5f;   // Reducido para texto largo
                float tamanoFuenteResultado = 9f;
                
                // Anchos de columnas (valores relativos, suman aproximadamente 8.5)
                float anchoCiclo = 1f;
                float anchoEstatus = 0.8f;
                float anchoCodigo = 1f;
                float anchoNombre = 3.5f;  // Aumentado para nombres largos
                float anchoUV = 0.7f;
                float anchoNota = 0.8f;
                float anchoLetra = 0.7f;
                float anchoResultado = 1.2f;
                
                // ============================================
                
                // TABLA DE MATERIAS
                var tablaMaterias = new Table(new float[] { 
                    anchoCiclo, anchoEstatus, anchoCodigo, anchoNombre, anchoUV, anchoNota, anchoLetra, anchoResultado 
                });
                tablaMaterias.SetWidth(UnitValue.CreatePercentValue(100));

                // HEADER
                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("Ciclo")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("Estatus")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("CODIGO")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("Nombre de la asignatura")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("U.V.")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                // Header de Calificación (fusionada 2 columnas)
                var calificacionHeader = new Cell(1, 2) // Fusiona 2 columnas
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1));
                calificacionHeader.Add(new Paragraph("Calificación")
                    .SetFontSize(tamanoFuenteHeader)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(_fontBold));
                tablaMaterias.AddHeaderCell(calificacionHeader);

                tablaMaterias.AddHeaderCell(new Cell()
                    .Add(new Paragraph("Resultado")
                        .SetFontSize(tamanoFuenteHeader)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetFont(_fontBold)
                    .SetBorderLeft(new SolidBorder(1))
                    .SetBorderRight(new SolidBorder(1))
                    .SetBorderTop(new SolidBorder(1))
                    .SetBorderBottom(new SolidBorder(1)));

                // DATOS
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
                        bool esUltimaFila = contadorMaterias == totalMaterias;
                        
                        // Ciclo (solo en la primera fila de cada ciclo)
                        if (esPrimeraMateriaDelCiclo)
                        {
                            tablaMaterias.AddCell(new Cell()
                                .Add(new Paragraph(ciclo.CicloTexto)
                                    .SetFontSize(tamanoFuenteCiclo))
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetBorderLeft(new SolidBorder(1))
                                .SetBorderRight(new SolidBorder(1))
                                .SetBorderTop(Border.NO_BORDER)
                                .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));
                            esPrimeraMateriaDelCiclo = false;
                        }
                        else
                        {
                            // Celda vacía para mantener la estructura
                            tablaMaterias.AddCell(new Cell()
                                .SetBorderLeft(new SolidBorder(1))
                                .SetBorderRight(new SolidBorder(1))
                                .SetBorderTop(Border.NO_BORDER)
                                .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));
                        }

                        // Estatus (vacío)
                        tablaMaterias.AddCell(new Cell()
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));

                        // CODIGO (alineado a la izquierda)
                        string codigo = materia.Materia != null 
                            ? materia.Materia.CodigoMateria 
                            : materia.MateriaCodigoLibre ?? "-";
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(codigo)
                                .SetFontSize(tamanoFuenteCodigo))
                            .SetTextAlignment(TextAlignment.LEFT)
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));

                        // Nombre de la asignatura
                        string nombreMateria = materia.Materia != null 
                            ? materia.Materia.NombreMateria 
                            : materia.MateriaNombreLibre ?? "-";
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(nombreMateria)
                                .SetFontSize(tamanoFuenteNombre))
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));

                        // U.V.
                        int uv = materia.Materia != null 
                            ? materia.Materia.uv 
                            : materia.MateriaUnidadesValorativasLibre ?? 0;
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(uv.ToString())
                                .SetFontSize(tamanoFuenteUV))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));

                        // Calificación (dividida en 2 columnas: NOTA y letra)
                        decimal promedio = materia.Promedio;
                        // Formato: 1 decimal máximo (7.8, 9.0, 10.0)
                        string notaNumerica = promedio.ToString("0.0");
                        string notaLetras = ConvertirNumeroALetras(promedio);
                        
                        // Columna NOTA (solo el número)
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(notaNumerica)
                                .SetFontSize(tamanoFuenteNota))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));
                        
                        // Columna Letra (solo el texto)
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(notaLetras)
                                .SetFontSize(tamanoFuenteLetra))
                            .SetTextAlignment(TextAlignment.LEFT)
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));

                        // Resultado (APROBADA si 7.0-10.0, REPROBADA si no)
                        string resultado = promedio >= 7.0m && promedio <= 10.0m ? "APROBADA" : "REPROBADA";
                        tablaMaterias.AddCell(new Cell()
                            .Add(new Paragraph(resultado)
                                .SetFontSize(tamanoFuenteResultado))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetBorderLeft(new SolidBorder(1))
                            .SetBorderRight(new SolidBorder(1))
                            .SetBorderTop(Border.NO_BORDER)
                            .SetBorderBottom(esUltimaFila ? new SolidBorder(1) : Border.NO_BORDER));
                    }
                    
                    cicloAnterior = ciclo.CicloTexto;
                }

                document.Add(tablaMaterias);
                document.Add(new Paragraph(" ").SetMarginBottom(10));

                // Pie de página con información del alumno
                document.Add(new Paragraph($"Alumno: {alumno.Nombres} {alumno.Apellidos}")
                    .SetFont(_fontNormal)
                    .SetFontSize(10));
                
                if (alumno.Carrera != null)
                {
                    document.Add(new Paragraph($"Carrera: {alumno.Carrera.NombreCarrera}")
                        .SetFont(_fontNormal)
                        .SetFontSize(10));
                }

                document.Close();
                var pdfBytes = memoryStream.ToArray();
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

