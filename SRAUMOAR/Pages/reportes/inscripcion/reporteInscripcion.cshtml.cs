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
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Servicios;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
namespace SRAUMOAR.Pages.reportes.inscripcion
{
    public class reporteInscripcionModel : PageModel
    {
        public Alumno Alumno { get; set; }
        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public reporteInscripcionModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public IActionResult OnGet(int? id)
        {
            try
            {

                var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
                Alumno = _context.Alumno.Include(a => a.Carrera).ThenInclude(c => c.Facultad).Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();

                MateriasInscritas = _context.MateriasInscritas
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Materia)
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Docente)
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Grupo)
                        .ThenInclude(ps => ps.Pensum)
                    .Where(mi => mi.MateriasGrupo.Grupo.CicloId == cicloactual &&
                                 mi.Alumno.AlumnoId == Alumno.AlumnoId)
                    .ToList();

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
                textCell.Add(new Paragraph("INSCRIPCION DE MATERIAS CICLO 02-2025").SetFontSize(11).SetTextAlignment(TextAlignment.CENTER));
                headerTable.AddCell(textCell);

                document.Add(headerTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));

                // DATOS DEL ALUMNO
                float[] columnWidths = { 500f, 300f }; // Ambas columnas de 300 puntos

                var datosTable = new Table(columnWidths);
                datosTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Columna izquierda
                var columnaIzq = new Cell();
                columnaIzq.Add(new Paragraph("Carnet: " + (string.IsNullOrEmpty(Alumno.Carnet) ?
    (string.IsNullOrEmpty(Alumno.Email) ? "-" : Alumno.Email.Split('@')[0]) :
    Alumno.Carnet)).SetFontSize(13));
                columnaIzq.Add(new Paragraph("Nombre: " + Alumno.Nombres + " " + Alumno.Apellidos).SetFontSize(11));
                columnaIzq.Add(new Paragraph("Facultad: " + Alumno.Carrera.Facultad.NombreFacultad).SetFontSize(11));
                columnaIzq.Add(new Paragraph("Carrera: " + Alumno.Carrera.NombreCarrera).SetFontSize(11));
                columnaIzq.Add(new Paragraph("Plan: " + MateriasInscritas.First().MateriasGrupo.Grupo.Pensum.NombrePensum).SetFontSize(11));
                columnaIzq.SetBorder(Border.NO_BORDER);

                // Columna derecha
                var columnaDer = new Cell();
                columnaDer.Add(new Paragraph("Dirección: "+Alumno.DireccionDeResidencia).SetFontSize(11));
                columnaDer.Add(new Paragraph("Ciclo a cursar: 02-25").SetFontSize(11));
                columnaDer.Add(new Paragraph("Teléfono fijo: " + (Alumno.TelefonoPrimario ?? "-")).SetFontSize(11));
                columnaDer.Add(new Paragraph("Teléfono móvil: " + (Alumno.TelefonoSecundario ?? "-")).SetFontSize(11));
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
                var i = 0;
                foreach(var mat in MateriasInscritas)
                
                {

                    var prerrequisitos = _context.MateriasPrerrequisitos
                    .Where(p => p.MateriaId == mat.MateriasGrupo.MateriaId)
                    .Select(p => new {
                        p.PrerrequisoMateria.MateriaId,
                        p.PrerrequisoMateria.CodigoMateria,
                        p.PrerrequisoMateria.NombreMateria,
                        p.PrerrequisoMateria.Ciclo,
                        p.PrerrequisoMateria.uv,
                        PensumNombre = p.PrerrequisoMateria.Pensum.NombrePensum
                    })
                    .ToList();


                    i++;           
                    
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(i.ToString())
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.CENTER).SetBorder(Border.NO_BORDER));

                    // Código
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(mat.MateriasGrupo.Materia.CodigoMateria)
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Nombre asignatura
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(mat.MateriasGrupo.Materia.NombreMateria)
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Matrícula
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph("1")
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

                    // Pre-Requisito datos (solo COD y NOMBRE sin título)
                    var preReqDataTable = new Table(2);
                    preReqDataTable.SetWidth(UnitValue.CreatePercentValue(100));

                    // Código final recomendado
                    preReqDataTable.AddCell(new Cell().Add(new Paragraph(
                        prerrequisitos.Any() ? prerrequisitos.First().CodigoMateria : "-")
                        .SetFontSize(10))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(Border.NO_BORDER));

                    preReqDataTable.AddCell(new Cell().Add(new Paragraph(
                        prerrequisitos.Any() ? prerrequisitos.First().NombreMateria : "-")
                        .SetFontSize(10))
                        .SetBorder(Border.NO_BORDER));

                    tablaAsignaturas.AddCell(new Cell().Add(preReqDataTable).SetBorder(Border.NO_BORDER));

                    // Día
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(((DiaSemana)mat.MateriasGrupo.Dia).ToString()
)
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Hora
                    string horaFormateada = mat.MateriasGrupo.FormatearHora12Horas(mat.MateriasGrupo.HoraInicio) + " - " + mat.MateriasGrupo.FormatearHora12Horas(mat.MateriasGrupo.HoraFin);
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(horaFormateada)
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetBorder(Border.NO_BORDER));

                    // Grupo
                    tablaAsignaturas.AddCell(new Cell().Add(new Paragraph(mat.MateriasGrupo.Grupo.Nombre)
                        .SetFontSize(10)) // Tamaño de fuente
                        .SetTextAlignment(TextAlignment.CENTER).SetBorder(Border.NO_BORDER));
                }



                document.Add(tablaAsignaturas);
                document.Add(new Paragraph("_".PadRight(75, '_'))
                .SetTextAlignment(TextAlignment.CENTER));


                //****************************
                // SECCIÓN DE FIRMAS
              
                //*****************************************************************
                // Simulación de fecha obtenida de la base de datos
                string fechaDB = DateTime.Now.ToString("yyyy-MM-dd");

                // Extraer solo la parte de la fecha
                DateTime fecha = DateTime.ParseExact(fechaDB.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Formatear al estilo deseado
                string fechaTexto = fecha.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));

                // Agregar al documento
                document.Add(new Paragraph($"Distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango, {fechaTexto}"));
                document.Add(new Paragraph(" "));
                
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
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph("DESPUÉS DE SER REVISADO POR EL ASESOR, PRESENTAR ESTE FORMATO EN REGISTRO ACADÉMICO Y PARA CUALQUIER OBSERVACIÓN SE LO HARÁ EL DIRECTOR DE REGISTRO ACADÉMICO").SetFontSize(10));

                

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
