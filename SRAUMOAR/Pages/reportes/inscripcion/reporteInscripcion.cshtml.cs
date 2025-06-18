using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Image;
using iText.Layout.Borders;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using System.Globalization;

namespace SRAUMOAR.Pages.reportes.inscripcion
{
    public class reporteInscripcionModel : PageModel
    {
        public IActionResult OnGet()
        {
            try
            {
                using var memoryStream = new MemoryStream();
                using var writer = new PdfWriter(memoryStream);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                // ENCABEZADO
                var headerTable = new Table(2);
                headerTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Logo
                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                var logo = new Image(ImageDataFactory.Create(logoPath));
                logo.SetWidth(60);
                headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER));

                // Texto centrado - 3 líneas
                var textCell = new Cell().SetBorder(Border.NO_BORDER);
                textCell.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO").SetFontSize(14).SetTextAlignment(TextAlignment.CENTER));
                textCell.Add(new Paragraph("ADMINISTRACION DE REGISTRO ACADÉMICO").SetFontSize(12).SetTextAlignment(TextAlignment.CENTER));
                textCell.Add(new Paragraph("INSCRIPCION DE MATERIAS CICLO 01-2025").SetFontSize(11).SetTextAlignment(TextAlignment.CENTER));
                headerTable.AddCell(textCell);

                document.Add(headerTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));

                // DATOS DEL ALUMNO
                var datosTable = new Table(2);
                datosTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Columna izquierda
                var columnaIzq = new Cell();
                columnaIzq.Add(new Paragraph("Carnet: ").SetFontSize(13));
                columnaIzq.Add(new Paragraph("Nombre: ").SetFontSize(13));
                columnaIzq.Add(new Paragraph("Facultad: ").SetFontSize(13));
                columnaIzq.Add(new Paragraph("Carrera: ").SetFontSize(13));
                columnaIzq.Add(new Paragraph("Plan: ").SetFontSize(13));
                columnaIzq.SetBorder(Border.NO_BORDER);

                // Columna derecha
                var columnaDer = new Cell();
                columnaDer.Add(new Paragraph("Dirección: ").SetFontSize(13));
                columnaDer.Add(new Paragraph("Ciclo a cursar: ").SetFontSize(13));
                columnaDer.Add(new Paragraph("Teléfono fijo: ").SetFontSize(13));
                columnaDer.Add(new Paragraph("Teléfono móvil: ").SetFontSize(13));
                columnaDer.SetBorder(Border.NO_BORDER);

                datosTable.AddCell(columnaIzq);
                datosTable.AddCell(columnaDer);
                document.Add(datosTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));
                //********************************************************************
                // TABLA DE ASIGNATURAS
                var tablaAsignaturas = new Table(new float[] { 0.5f, 1f, 2f, 1f, 2f, 1f, 1f, 1f });
                tablaAsignaturas.SetWidth(UnitValue.CreatePercentValue(100));

                // HEADER con bordes - tratamiento especial para Pre-Requisito
                string[] headers = { "N°", "Código", "Nombre de la asignatura", "Matrícula", "", "Día", "Hora", "Grupo" };
                for (int h = 0; h < headers.Length; h++)
                {
                    if (h == 4) // Columna Pre-Requisito
                    {
                        var preReqHeaderCell = new Cell().SetBorder(Border.NO_BORDER);

                        // Título "Pre-Requisito"
                        preReqHeaderCell.Add(new Paragraph("Pre-Requisito")
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                        // Subtabla COD y NOMBRE
                        var headerSubTable = new Table(2);
                        headerSubTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph("COD").SetBorder(Border.NO_BORDER))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                            );
                        headerSubTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph("NOMBRE").SetBorder(Border.NO_BORDER))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                            );

                        preReqHeaderCell.Add(headerSubTable);
                        tablaAsignaturas.AddHeaderCell(preReqHeaderCell.SetBorder(new SolidBorder(0)));
                    }
                    else
                    {
                        tablaAsignaturas.AddHeaderCell(new Cell()
                            .Add(new Paragraph(headers[h]).SetFontSize(12)
                            .SetTextAlignment(TextAlignment.CENTER))
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                            .SetBorder(new SolidBorder(1)));
                    }
                }

                for (int i = 1; i <= 5; i++)
                {
                    // N°
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(i.ToString())
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.CENTER).SetBorder(Border.NO_BORDER));

                    // Código
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph($"MAT{100 + i}")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Nombre asignatura
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph($"Matemática {i}")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Matrícula
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph("$25.00")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

                    // Pre-Requisito datos (solo COD y NOMBRE sin título)
                    var preReqDataTable = new Table(2);
                    preReqDataTable.SetWidth(UnitValue.CreatePercentValue(100));

                    preReqDataTable.AddCell(new Cell().Add(new Paragraph($"BACH001")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(Border.NO_BORDER));

                    preReqDataTable.AddCell(new Cell().Add(new Paragraph("Matematica")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    tablaAsignaturas.AddCell(new Cell().Add(preReqDataTable).SetBorder(Border.NO_BORDER));

                    // Día
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph("Lunes")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Hora
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph("8:00-9:00")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Grupo
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph("A")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.CENTER).SetBorder(Border.NO_BORDER));
                }



                document.Add(tablaAsignaturas);
                document.Add(new Paragraph("_".PadRight(75, '_'))
                .SetTextAlignment(TextAlignment.CENTER));


                //****************************
                // SECCIÓN DE FIRMAS
                #region FIRMAS
                var firmasTable = new Table(3);
                firmasTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Espacios para las firmas
                firmasTable.AddCell(new Cell().Add(new Paragraph("\n\n\n"))
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("\n\n\n"))
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("\n\n\n"))
                   .SetBorder(Border.NO_BORDER));

                // Líneas para firmar
                firmasTable.AddCell(new Cell().Add(new Paragraph("_".PadRight(30, '_')).SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("_".PadRight(30, '_')).SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("_".PadRight(30, '_')).SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));

                // Textos de las firmas
                firmasTable.AddCell(new Cell().Add(new Paragraph("FIRMA DEL ESTUDIANTE").SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("FIRMA DEL ASESOR").SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("SELLO REGISTRO ACADEMICO").SetFontSize(10))
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetBorder(Border.NO_BORDER));

                document.Add(firmasTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));
                //*****************************************************************
                // Simulación de fecha obtenida de la base de datos
                string fechaDB = "2024-12-13 19:45:50.9776656";

                // Extraer solo la parte de la fecha
                DateTime fecha = DateTime.ParseExact(fechaDB.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Formatear al estilo deseado
                string fechaTexto = fecha.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));

                // Agregar al documento
                document.Add(new Paragraph($"Distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango, {fechaTexto}"));
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph("DESPUÉS DE SER REVISADO POR EL ASESOR, PRESENTAR ESTE FORMATO EN REGISTRO ACADÉMICO Y PARA CUALQUIER OBSERVACIÓN SE LO HARÁ EL DIRECTOR DE REGISTRO ACADÉMICO"));

                #endregion

                //********************************************************************
                document.Close();
                var pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                // En caso de error, retornar un mensaje
                return BadRequest($"Error al generar PDF: {ex.Message}");
            }
        }
    }
}
