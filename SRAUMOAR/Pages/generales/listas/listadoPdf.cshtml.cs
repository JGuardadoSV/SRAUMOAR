using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades.Procesos;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using iText.Layout;
namespace SRAUMOAR.Pages.generales.listas
{
    public class listadoPdfModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public listadoPdfModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademicas { get; set; } = default!;
        public Grupo Grupo { get; set; } = default!;
        public string NombreMateria { get; set; } = default!;
        public bool lista { get; set; }
        public int idgrupo { get; set; } = default!;

        private async Task<string> ObtenerNombreMateriaAsync(int inscripcionMateriaId)
        {
            return await _context.MateriasGrupo
                .Include(im => im.Materia)
                .Where(im => im.MateriasGrupoId == inscripcionMateriaId)
                .Select(im => im.Materia.NombreMateria)
                .FirstOrDefaultAsync();
        }
        public IActionResult OnGet(int id)
        {
            try
            {
                // Obtener datos (usando la misma lógica que ya tienes)
                var cicloactual = _context.Ciclos.Where(x => x.Activo == true).First();

                var nombreMateria = ObtenerNombreMateriaAsync(id).Result;

                var grupo = _context.MateriasGrupo
                    .Include(g => g.Grupo)
                        .ThenInclude(g => g.Carrera)
                    .Include(g => g.Docente)
                .Where(mg => mg.MateriasGrupoId == id)
                    .Select(mg => mg.Grupo)
                    .FirstOrDefault() ?? new Grupo();

                var materiasInscritas = _context.MateriasInscritas
                    .Include(m => m.Alumno)
                .Include(m => m.MateriasGrupo)
                    .Where(m => m.MateriasGrupoId == id)
                    .ToList();

                // Obtener información del docente
                var materiaGrupo = _context.MateriasGrupo
                    .Include(mg => mg.Docente)
                    .FirstOrDefault(mg => mg.MateriasGrupoId == id);

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

                // Texto del encabezado
                var textCell = new Cell().SetBorder(Border.NO_BORDER);
                textCell.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                textCell.Add(new Paragraph("LISTA DE ASISTENCIA")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                headerTable.AddCell(textCell);

                document.Add(headerTable);
                document.Add(new Paragraph(" "));
                float[] columnWidths = { 400f, 400f }; // Ambas columnas de 300 puntos
                // INFORMACIÓN DE LA MATERIA EN DOS COLUMNAS
                var infoTable = new Table(columnWidths);
                infoTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Columna izquierda
                var columnaIzq = new Cell().SetBorder(Border.NO_BORDER);
                columnaIzq.Add(new Paragraph("Materia: " + (nombreMateria ?? "No especificada"))
                    .SetFontSize(11));
                columnaIzq.Add(new Paragraph("Grupo: " + (grupo?.Nombre ?? "No especificado"))
                    .SetFontSize(11));
                columnaIzq.Add(new Paragraph("Fecha: " + DateTime.Now.ToShortDateString())
                    .SetFontSize(11));

                // Formatear hora si existe
                string horaTexto = "No especificada";
                if (materiaGrupo != null)
                {
                    horaTexto = materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraInicio) + " - " +
                               materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraFin);
                }
                columnaIzq.Add(new Paragraph("Hora: " + horaTexto).SetFontSize(11));

                // Columna derecha
                var columnaDer = new Cell().SetBorder(Border.NO_BORDER);
                columnaDer.Add(new Paragraph("Carrera: " + (grupo?.Carrera?.NombreCarrera ?? "No especificada"))
                    .SetFontSize(11));

                string docenteTexto = "No asignado";
                if (materiaGrupo?.Docente != null)
                {
                    docenteTexto = $"{materiaGrupo.Docente.Nombres} {materiaGrupo.Docente.Apellidos}";
                }
                columnaDer.Add(new Paragraph("Docente: " + docenteTexto).SetFontSize(11));
                columnaDer.Add(new Paragraph("Aula: " + (materiaGrupo?.Aula ?? "No asignada"))
                    .SetFontSize(11));

                infoTable.AddCell(columnaIzq);
                infoTable.AddCell(columnaDer);
                document.Add(infoTable);
                document.Add(new Paragraph(" "));

                // TABLA DE ESTUDIANTES
                var tablaEstudiantes = new Table(new float[] { 0.5f, 3f, 2f });
                tablaEstudiantes.SetWidth(UnitValue.CreatePercentValue(100));

                // Headers
                tablaEstudiantes.AddHeaderCell(new Cell()
                    .Add(new Paragraph("N°").SetFontSize(12))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetBorder(new SolidBorder(1)));

                tablaEstudiantes.AddHeaderCell(new Cell()
                    .Add(new Paragraph("NOMBRE DEL ESTUDIANTE").SetFontSize(12))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetBorder(new SolidBorder(1)));

                tablaEstudiantes.AddHeaderCell(new Cell()
                    .Add(new Paragraph("FIRMA").SetFontSize(12))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetBorder(new SolidBorder(1)));

                // Filas de estudiantes
                int correlativo = 1;
                int totalHombres = 0;
                int totalMujeres = 0;

                foreach (var item in materiasInscritas)
                {
                    // Número correlativo
                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(correlativo.ToString()).SetFontSize(10))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(new SolidBorder(1)));

                    // Nombre del estudiante
                    string nombreCompleto = $"{item?.Alumno?.Nombres ?? ""} {item?.Alumno?.Apellidos ?? ""}";
                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(nombreCompleto).SetFontSize(10))
                        .SetBorder(new SolidBorder(1)));

                    // Celda para firma (vacía)
                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(" ").SetFontSize(10))
                        .SetHeight(25)
                        .SetBorder(new SolidBorder(1)));

                    // Contar por género (asumiendo que tienes un campo Genero en Alumno)
                    if (item?.Alumno?.Genero == 0) // suponiendo que 1 es el código para masculino
                        totalHombres++;
                    else if (item?.Alumno?.Genero == 1) // suponiendo que 2 es el código para femenino
                        totalMujeres++;

                    correlativo++;
                }

                document.Add(tablaEstudiantes);
                document.Add(new Paragraph(" "));

                // RESUMEN Y TOTALES
                var resumenTable = new Table(4);
                resumenTable.SetWidth(UnitValue.CreatePercentValue(100));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph("Total de estudiantes: " + materiasInscritas.Count).SetFontSize(11))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph("Hombres: " + totalHombres).SetFontSize(11))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph("Mujeres: " + totalMujeres).SetFontSize(11))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph("Faltantes: ____").SetFontSize(11))
                    .SetBorder(Border.NO_BORDER));

                document.Add(resumenTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));

                // SECCIÓN DE FIRMAS
                var firmasTable = new Table(2);
                firmasTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Espacios para las firmas
                firmasTable.AddCell(new Cell().Add(new Paragraph("\n\n\n"))
                    .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell().Add(new Paragraph("\n\n\n"))
                    .SetBorder(Border.NO_BORDER));

                // Líneas para firmar
                firmasTable.AddCell(new Cell()
                    .Add(new Paragraph("_".PadRight(40, '_')).SetFontSize(10))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell()
                    .Add(new Paragraph("_".PadRight(40, '_')).SetFontSize(10))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(Border.NO_BORDER));

                // Textos de las firmas
                firmasTable.AddCell(new Cell()
                    .Add(new Paragraph("FIRMA DEL DOCENTE").SetFontSize(10))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(Border.NO_BORDER));
                firmasTable.AddCell(new Cell()
                    .Add(new Paragraph("SELLO DE LA INSTITUCIÓN").SetFontSize(10))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(Border.NO_BORDER));

                // Agregar nombre del docente debajo de su firma
                if (materiaGrupo?.Docente != null)
                {
                    firmasTable.AddCell(new Cell()
                        .Add(new Paragraph($"{materiaGrupo.Docente.Nombres} {materiaGrupo.Docente.Apellidos}")
                            .SetFontSize(9))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(Border.NO_BORDER));
                }
                else
                {
                    firmasTable.AddCell(new Cell().Add(new Paragraph("")).SetBorder(Border.NO_BORDER));
                }

                firmasTable.AddCell(new Cell().Add(new Paragraph("")).SetBorder(Border.NO_BORDER));

                document.Add(firmasTable);

                // Fecha y ubicación
                document.Add(new Paragraph(" "));
                string fechaTexto = DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));
                document.Add(new Paragraph($"Distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango, {fechaTexto}")
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Close();
                var pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar PDF: {ex.Message}");
            }
        }
    }
}
