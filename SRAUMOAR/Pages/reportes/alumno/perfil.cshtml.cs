using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades.Alumnos;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades.Alumnos;
using Microsoft.EntityFrameworkCore;

namespace SRAUMOAR.Pages.reportes.alumno
{
    public class perfilModel : PageModel
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SRAUMOAR.Modelos.Contexto _context;
       
        public perfilModel(IWebHostEnvironment webHostEnvironment,SRAUMOAR.Modelos.Contexto context)
        {
           _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        public IActionResult OnGet(int id)
        {
            var alumno = _context.Alumno
    .Include(a => a.Municipio) // Carga el Municipio relacionado
    .ThenInclude(m => m.Distrito) // Carga el Distrito relacionado
    .ThenInclude(d => d.Departamento) // Carga la Provincia relacionada
    .Include(a => a.Carrera)
    .FirstOrDefault(x => x.AlumnoId == id);

            if (alumno == null)
                return NotFound();

            var pdfBytes = GenerarPdfAlumno(alumno);
            var stream = new MemoryStream(pdfBytes);
            return File(pdfBytes, "application/pdf");

            //return new FileStreamResult(stream, "application/pdf")
            //{
            //    FileDownloadName = $"Alumno_{alumno.Nombres}_{alumno.Apellidos}.pdf",
            //    EnableRangeProcessing = true
            //};
        }


        private byte[] GenerarPdfAlumno(Alumno alumno)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new PdfWriter(memoryStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

            // Configurar márgenes
            document.SetMargins(40, 40, 40, 40);

            // Configurar fuentes
            PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            PdfFont fontTitle = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // Colores personalizados
            Color colorPrimario = new DeviceRgb(0, 74, 0); // Verde oscuro profundo
            Color colorSecundario = new DeviceRgb(0, 0, 0); // Marrón cálido (tierra)
            Color colorTexto = new DeviceRgb(0, 0, 0); // Gris claro para buena legibilidad
            Color colorFondo = new DeviceRgb(255, 255, 255); // Verde muy suave para fondo sutil


            // === HEADER CON LOGO ===
            var headerTable = new Table(new float[] { 2, 5, 2 });
            headerTable.SetWidth(UnitValue.CreatePercentValue(100));

            // Logo
            try
            {
                string logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "logoUmoar.jpg");
                if (System.IO.File.Exists(logoPath))
                {
                    ImageData imageData = ImageDataFactory.Create(logoPath);
                    Image logo = new Image(imageData);
                    logo.SetWidth(80);
                    logo.SetHeight(80);
                    headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER));
                }
                else
                {
                    headerTable.AddCell(new Cell().Add(new Paragraph("LOGO")).SetBorder(Border.NO_BORDER));
                }
            }
            catch
            {
                headerTable.AddCell(new Cell().Add(new Paragraph("LOGO")).SetBorder(Border.NO_BORDER));
            }

            // Título principal
            var titleCell = new Cell()
                .Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .SetFont(fontTitle)
                    .SetFontSize(14)
                    .SetFontColor(colorPrimario)
                    .SetTextAlignment(TextAlignment.CENTER))
                .Add(new Paragraph("Ficha de Información del Estudiante")
                    .SetFont(fontRegular)
                    .SetFontSize(12)
                    .SetFontColor(colorSecundario)
                    .SetTextAlignment(TextAlignment.CENTER))
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.CENTER);

            headerTable.AddCell(titleCell);

            // Fecha
            headerTable.AddCell(new Cell()
                .Add(new Paragraph($"Fecha Registro: {alumno.FechaDeRegistro.ToString("dd/MM/yyyy")}")
                    .SetFont(fontRegular)
                    .SetFontSize(10)
                    .SetFontColor(colorTexto))
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.RIGHT));

            document.Add(headerTable);

            // Línea decorativa
            document.Add(new Paragraph()
                .SetHeight(10)
                .SetBackgroundColor(colorPrimario)
                .SetMarginTop(15)
                .SetMarginBottom(5));

            // === INFORMACIÓN PERSONAL ===
            var seccionPersonal = CrearSeccion("INFORMACIÓN PERSONAL", colorPrimario, fontBold);
            document.Add(seccionPersonal);

            var tablaPersonal = new Table(new float[] { 1, 2, 1, 2 });
            tablaPersonal.SetWidth(UnitValue.CreatePercentValue(100));
            tablaPersonal.SetMarginBottom(5);

            // Fila 1
            AgregarCampo(tablaPersonal, "Nombres:", alumno.Nombres ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaPersonal, "Apellidos:", alumno.Apellidos ?? "N/A", fontBold, fontRegular, colorSecundario);

            // Fila 2
            AgregarCampo(tablaPersonal, "DUI:", alumno.DUI ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaPersonal, "Fecha de Nacimiento:", alumno.FechaDeNacimiento.ToString("dd/MM/yyyy"), fontBold, fontRegular, colorSecundario);

            // Fila 3
            AgregarCampo(tablaPersonal, "Email:", alumno.Email ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaPersonal, "Género:", ObtenerGenero(alumno.Genero), fontBold, fontRegular, colorSecundario);

            // Fila 4
            AgregarCampo(tablaPersonal, "Lugar de Nacimiento:",
            !string.IsNullOrEmpty(alumno.MunicipioNacimiento) || !string.IsNullOrEmpty(alumno.DepartamentoNacimiento)
                ? $"{alumno.MunicipioNacimiento ?? ""} {alumno.DepartamentoNacimiento ?? ""}".Trim()
                : "N/A",
    fontBold, fontRegular, colorSecundario);


            // Fila 5
            AgregarCampo(tablaPersonal, "Estado Civil:",
    alumno.Casado ? "Casado" : "Soltero",
    fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaPersonal, "Estudios Financiados por:", alumno.EstudiosFinanciadoPor ?? "N/A", fontBold, fontRegular, colorSecundario);

            document.Add(tablaPersonal);

            // Determinar símbolos individuales
            string partidaSimbolo = alumno.PPartida ? "[X]" : "[  ]";
            //string duiSimbolo = alumno.PDUI ? "[X]" : "[ ]";
            string tituloSimbolo = alumno.PTitulo ? "[X]" : "[  ]";
            string orina = alumno.PExamenOrina ? "[X]" : "[  ]";
            string paes = alumno.PPaes ? "[X]" : "[  ]";
            string equivalencia = alumno.PSolicitudEquivalencia ? "[X]" : "[  ]";
            string hemograma = alumno.PHemograma ? "[X]" : "[  ]";
            string fotos = alumno.PFotografias ? "[X]" : "[  ]";
            string curso = alumno.PPreuniversitario ? "[X]" : "[  ]";


            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Crear título
            Paragraph titulo = new Paragraph("DOCUMENTOS PRESENTADOS:")
                .SetFont(font)
                .SetFontSize(12)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);
            document.Add(titulo);

            // Crear tabla 3x3
            Table tabla = new Table(3);
            tabla.SetWidth(UnitValue.CreatePercentValue(100));

            // Fila 1
            tabla.AddCell(new Cell().Add(new Paragraph($"{partidaSimbolo} Partida").SetFont(fontNormal).SetFontSize(10)));
            tabla.AddCell(new Cell().Add(new Paragraph($"{tituloSimbolo} Título").SetFont(fontNormal).SetFontSize(10)));
            tabla.AddCell(new Cell().Add(new Paragraph($"{orina} Examen Orina").SetFont(fontNormal).SetFontSize(10)));

            // Fila 2
            tabla.AddCell(new Cell().Add(new Paragraph($"{hemograma} Hemograma").SetFont(fontNormal).SetFontSize(10)));
            tabla.AddCell(new Cell().Add(new Paragraph($"{paes} PAES").SetFont(fontNormal).SetFontSize(10)));
            tabla.AddCell(new Cell().Add(new Paragraph($"{equivalencia} Pago estudio de equivalencia").SetFont(fontNormal).SetFontSize(10)));

            // Fila 3
            tabla.AddCell(new Cell().Add(new Paragraph($"{curso} Preuniversitario").SetFont(fontNormal).SetFontSize(10)));
            tabla.AddCell(new Cell().Add(new Paragraph($"{fotos} Fotografías").SetFont(fontNormal).SetFontSize(10)));
            

            // Quitar bordes si quieres
            tabla.SetBorder(Border.NO_BORDER);
            foreach (var cell in tabla.GetChildren())
            {
                ((Cell)cell).SetBorder(Border.NO_BORDER);
            }

            document.Add(tabla);

            // === INFORMACIÓN DE CONTACTO ===
            var seccionContacto = CrearSeccion("INFORMACIÓN DE CONTACTO", colorPrimario, fontBold);
            document.Add(seccionContacto);

            var tablaContacto = new Table(new float[] { 1, 2, 1, 2 });
            tablaContacto.SetWidth(UnitValue.CreatePercentValue(100));
            tablaContacto.SetMarginBottom(5);

            AgregarCampo(tablaContacto, "Teléfono Primario:", alumno.TelefonoPrimario ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaContacto, "WhatsApp:", alumno.Whatsapp ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaContacto, "Teléfono Secundario:", alumno.TelefonoSecundario ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaContacto, "Estado:", alumno.Estado == 1 ? "Activo" : "Inactivo", fontBold, fontRegular, colorSecundario);

            document.Add(tablaContacto);

            // === CONTACTO DE EMERGENCIA ===
            var seccionEmergencia = CrearSeccion("CONTACTO DE EMERGENCIA", colorPrimario, fontBold);
            document.Add(seccionEmergencia);

            var tablaEmergencia = new Table(new float[] { 1, 3 });
            tablaEmergencia.SetWidth(UnitValue.CreatePercentValue(100));
            tablaEmergencia.SetMarginBottom(5);

            AgregarCampo(tablaEmergencia, "Contacto:", alumno.ContactoDeEmergencia ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaEmergencia, "Teléfono:", alumno.NumeroDeEmergencia ?? "N/A", fontBold, fontRegular, colorSecundario);

            document.Add(tablaEmergencia);

            // === DIRECCIÓN ===
            var seccionDireccion = CrearSeccion("DIRECCIÓN DE RESIDENCIA", colorPrimario, fontBold);
            document.Add(seccionDireccion);
            string direccion = alumno.DireccionDeResidencia + ", "+alumno.Municipio.NombreMunicipio+", "+alumno.Municipio.Distrito.NombreDistrito + ", " + alumno.Municipio.Distrito.Departamento.NombreDepartamento;
            var direccionParagraph = new Paragraph(direccion ?? "N/A")
                .SetFont(fontRegular)
                .SetFontSize(11)
                .SetFontColor(colorTexto)
                .SetBackgroundColor(colorFondo)
                .SetPadding(10)
                .SetBorder(new SolidBorder(colorPrimario, 1))
                .SetMarginBottom(5);

            document.Add(direccionParagraph);

            // === INFORMACIÓN ACADÉMICA ===
            var seccionAcademica = CrearSeccion("INFORMACIÓN ACADÉMICA", colorPrimario, fontBold);
            document.Add(seccionAcademica);

            var tablaAcademica = new Table(new float[] { 1, 2, 1, 2 });
            tablaAcademica.SetWidth(UnitValue.CreatePercentValue(100));

            AgregarCampo(tablaAcademica, "Carrera:", alumno.Carrera.NombreCarrera, fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaAcademica, "Carnet:", alumno.Carnet ?? "N/A", fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaAcademica, "Fecha de Registro:", alumno.FechaDeRegistro.ToString("dd/MM/yyyy"), fontBold, fontRegular, colorSecundario);
            AgregarCampo(tablaAcademica, "Ingreso por Equivalencias:", alumno.IngresoPorEquivalencias ? "Sí" : "No", fontBold, fontRegular, colorSecundario);

            document.Add(tablaAcademica);

            // === FOOTER ===
            document.Add(new Paragraph()
                .SetHeight(1)
                .SetBackgroundColor(colorPrimario)
                .SetMarginTop(30));

            var footerText = new Paragraph("Documento generado automáticamente por el Sistema UMOAR")
                .SetFont(fontRegular)
                .SetFontSize(8)
                .SetFontColor(colorSecundario)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(10);

            document.Add(footerText);

            document.Close();
            return memoryStream.ToArray();
        }

        private Paragraph CrearSeccion(string titulo, Color color, PdfFont font)
        {
            return new Paragraph(titulo)
                .SetFont(font)
                .SetFontSize(14)
                .SetFontColor(color)
                .SetMarginTop(20)
                .SetMarginBottom(5)
                .SetPaddingLeft(10)
                .SetBorderLeft(new SolidBorder(color, 3));
        }

        private void AgregarCampo(Table tabla, string etiqueta, string valor, PdfFont fontBold, PdfFont fontRegular, Color colorSecundario)
        {
            // Celda de etiqueta
            tabla.AddCell(new Cell()
                .Add(new Paragraph(etiqueta)
                    .SetFont(fontBold)
                    .SetFontSize(10)
                    .SetFontColor(colorSecundario))
                .SetBorder(Border.NO_BORDER)
                .SetPaddingBottom(2));

            // Celda de valor
            tabla.AddCell(new Cell()
                .Add(new Paragraph(valor)
                    .SetFont(fontRegular)
                    .SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPaddingBottom(2));
        }

        private string ObtenerGenero(int genero)
        {
            return genero switch
            {
                0 => "Masculino",
                1 => "Femenino",
                3 => "Otro",
                _ => "No especificado"
            };
        }
      
    }
}