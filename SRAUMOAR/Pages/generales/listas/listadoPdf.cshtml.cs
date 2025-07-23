using iText.IO.Font;
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
using Microsoft.EntityFrameworkCore;
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
                // Obtener datos
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

                var materiaGrupo = _context.MateriasGrupo
                    .Include(mg => mg.Docente)
                    .FirstOrDefault(mg => mg.MateriasGrupoId == id);

                using var memoryStream = new MemoryStream();

                // Configurar el PdfWriter con opciones espec�ficas
                var writerProperties = new WriterProperties();
                writerProperties.SetCompressionLevel(9);

                using var writer = new PdfWriter(memoryStream, writerProperties);
                using var pdf = new PdfDocument(writer);

                // Configurar metadatos del PDF
                var info = pdf.GetDocumentInfo();
                info.SetTitle("Lista de Asistencia");
                info.SetAuthor("SRAUMOAR");

                using var document = new Document(pdf);

                // IMPORTANTE: Crear las fuentes con el encoding correcto
                PdfFont normalFont;
                PdfFont boldFont;
                normalFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                boldFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
                

                // Establecer la fuente por defecto para todo el documento
                document.SetFont(normalFont);

                // ENCABEZADO
                var headerTable = new Table(2);
                headerTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Logo
                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = new Image(ImageDataFactory.Create(logoPath));
                    logo.SetWidth(60);
                    headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER));
                }
                else
                {
                    headerTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                }

                // Texto del encabezado - Asegurarse de establecer la fuente
                var textCell = new Cell().SetBorder(Border.NO_BORDER);
                var titleParagraph = new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(boldFont);
                textCell.Add(titleParagraph);

                var subtitleParagraph = new Paragraph("LISTA DE ASISTENCIA")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFont(boldFont);
                textCell.Add(subtitleParagraph);
                headerTable.AddCell(textCell);

                document.Add(headerTable);
                document.Add(new Paragraph(" ").SetFont(normalFont));

                // INFORMACIÓN DE LA MATERIA
                float[] columnWidths = { 400f, 400f };
                var infoTable = new Table(columnWidths);
                infoTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Columna izquierda
                var columnaIzq = new Cell().SetBorder(Border.NO_BORDER);
                columnaIzq.Add(new Paragraph($"Materia: {nombreMateria ?? "No especificada"}")
                    .SetFontSize(11).SetFont(normalFont));
                columnaIzq.Add(new Paragraph($"Grupo: {grupo?.Nombre ?? "No especificado"}")
                    .SetFontSize(11).SetFont(normalFont));
                columnaIzq.Add(new Paragraph($"Fecha:    /     / 2025")
                    .SetFontSize(11).SetFont(normalFont));

                string horaTexto = "No especificada";
                if (materiaGrupo != null)
                {
                    horaTexto = $"{materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraInicio)} - {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraFin)}";
                }
                columnaIzq.Add(new Paragraph($"Hora: {horaTexto}")
                    .SetFontSize(11).SetFont(normalFont));

                // Columna derecha
                var columnaDer = new Cell().SetBorder(Border.NO_BORDER);
                columnaDer.Add(new Paragraph($"Carrera: {grupo?.Carrera?.NombreCarrera ?? "No especificada"}")
                    .SetFontSize(11).SetFont(normalFont));

                string docenteTexto = "No asignado";
                if (materiaGrupo?.Docente != null)
                {
                    docenteTexto = $"{materiaGrupo.Docente.Nombres} {materiaGrupo.Docente.Apellidos}";
                }
                columnaDer.Add(new Paragraph($"Docente: {docenteTexto}")
                    .SetFontSize(11).SetFont(normalFont));
                columnaDer.Add(new Paragraph($"Aula: {materiaGrupo?.Aula ?? "No asignada"}")
                    .SetFontSize(11).SetFont(normalFont));
                columnaDer.Add(new Paragraph("Firma: __________________")
                    .SetFontSize(11).SetFont(normalFont));

                infoTable.AddCell(columnaIzq);
                infoTable.AddCell(columnaDer);
                document.Add(infoTable);
                document.Add(new Paragraph(" ").SetFont(normalFont));

                // TABLA DE ESTUDIANTES
                var tablaEstudiantes = new Table(new float[] { 0.5f, 3f, 2f });
                tablaEstudiantes.SetWidth(UnitValue.CreatePercentValue(100));

                // Headers con fuente bold
                var headerNo = new Cell()
                    .Add(new Paragraph("Nº").SetFontSize(12).SetFont(boldFont))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(1));
                tablaEstudiantes.AddHeaderCell(headerNo);

                var headerNombre = new Cell()
                    .Add(new Paragraph("NOMBRE DEL ESTUDIANTE").SetFontSize(12).SetFont(boldFont))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(1));
                tablaEstudiantes.AddHeaderCell(headerNombre);

                var headerFirma = new Cell()
                    .Add(new Paragraph("FIRMA").SetFontSize(12).SetFont(boldFont))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(1));
                tablaEstudiantes.AddHeaderCell(headerFirma);

                // Filas de estudiantes
                int correlativo = 1;
                int totalHombres = 0;
                int totalMujeres = 0;

                foreach (var item in materiasInscritas)
                {
                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(correlativo.ToString()).SetFontSize(10).SetFont(normalFont))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(new SolidBorder(1)));

                    string nombreCompleto = $"{item?.Alumno?.Nombres ?? ""} {item?.Alumno?.Apellidos ?? ""}";
                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(nombreCompleto).SetFontSize(10).SetFont(normalFont))
                        .SetBorder(new SolidBorder(1)));

                    tablaEstudiantes.AddCell(new Cell()
                        .Add(new Paragraph(" ").SetFontSize(10).SetFont(normalFont))
                        .SetHeight(25)
                        .SetBorder(new SolidBorder(1)));

                    if (item?.Alumno?.Genero == 0)
                        totalHombres++;
                    else if (item?.Alumno?.Genero == 1)
                        totalMujeres++;

                    correlativo++;
                }

                document.Add(tablaEstudiantes);
                document.Add(new Paragraph(" ").SetFont(normalFont));

                // RESUMEN Y TOTALES
                var resumenTable = new Table(4);
                resumenTable.SetWidth(UnitValue.CreatePercentValue(100));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph($"Total de estudiantes: {materiasInscritas.Count}")
                        .SetFontSize(11).SetFont(normalFont))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph($"Hombres: {totalHombres}")
                        .SetFontSize(11).SetFont(normalFont))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph($"Mujeres: {totalMujeres}")
                        .SetFontSize(11).SetFont(normalFont))
                    .SetBorder(Border.NO_BORDER));

                resumenTable.AddCell(new Cell()
                    .Add(new Paragraph("Faltantes: ____")
                        .SetFontSize(11).SetFont(normalFont))
                    .SetBorder(Border.NO_BORDER));

                document.Add(resumenTable);

                document.Close();
                var pdfBytes = memoryStream.ToArray();

                // Establecer el nombre del archivo con encoding correcto
                var fileName = "Lista_Asistencia.pdf";
                Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");

                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                // Para debugging
                return BadRequest($"Error al generar PDF: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
    }
}