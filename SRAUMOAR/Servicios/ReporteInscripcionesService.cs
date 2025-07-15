using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Alumnos;
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
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Draw;

namespace SRAUMOAR.Servicios
{
    public class ReporteInscripcionesService
    {
        private readonly Contexto _context;
        private readonly PdfFont _fontNormal;
        private readonly PdfFont _fontBold;
        private readonly PdfFont _fontTitle;

        public ReporteInscripcionesService(Contexto context)
        {
            _context = context;
            _fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            _fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
            _fontTitle = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
        }

        public async Task<byte[]> GenerarReporteCompletoAsync()
        {
            return await GenerarReporteAsync(null, null, false);
        }

        public async Task<byte[]> GenerarReporteFiltradoAsync(int? carreraId, string genero)
        {
            return await GenerarReporteAsync(carreraId, genero, true);
        }

        private async Task<byte[]> GenerarReporteAsync(int? carreraId, string genero, bool esFiltrado)
        {
            try
            {
                var cicloactual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
                if (cicloactual == null)
                {
                    throw new InvalidOperationException("No hay un ciclo activo");
                }

                // Obtener inscripciones con filtros
                var query = _context.Inscripciones
                    .Include(i => i.Alumno)
                    .ThenInclude(a => a.Carrera)
                    .Include(i => i.Ciclo)
                    .Where(i => i.CicloId == cicloactual.Id && i.Activa);

                if (carreraId.HasValue && carreraId.Value > 0)
                {
                    query = query.Where(i => i.Alumno.CarreraId == carreraId.Value);
                }

                if (!string.IsNullOrEmpty(genero))
                {
                    if (int.TryParse(genero, out int generoInt))
                    {
                        query = query.Where(i => i.Alumno.Genero == generoInt);
                    }
                }

                var inscripciones = await query.ToListAsync();

                // Obtener grupos del ciclo actual
                var grupos = await _context.Grupo
                    .Include(g => g.Carrera)
                    .Include(g => g.Docente)
                    .Include(g => g.MateriasGrupos)
                    .ThenInclude(mg => mg.MateriasInscritas)
                    .ThenInclude(mi => mi.Alumno)
                    .Where(g => g.CicloId == cicloactual.Id && g.Activo)
                    .ToListAsync();

                // Agrupar por carrera
                var carrerasConGrupos = grupos
                    .GroupBy(g => g.Carrera)
                    .OrderBy(g => g.Key?.NombreCarrera)
                    .ToList();

                using var memoryStream = new MemoryStream();
                using var writer = new PdfWriter(memoryStream);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                // === Numeración de página se agregará al final del documento ===

                // Configurar márgenes
                document.SetMargins(30, 30, 30, 30);

                // Agregar encabezado
                AgregarEncabezado(document, esFiltrado, carreraId, genero);

                // Agregar contenido del reporte
                await AgregarContenidoReporte(document, carrerasConGrupos, inscripciones);

                // Agregar pie de página
                AgregarPiePagina(document, inscripciones.Count);

                document.Close();

                // === Numeración de página simple ===
                // Como no podemos usar eventos de página, agregamos la numeración al final
                // Esto mostrará "Página 1" pero será más confiable
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar reporte: {ex.Message}", ex);
            }
        }

        private void AgregarEncabezado(Document document, bool esFiltrado, int? carreraId, string genero)
        {
            // Logo y título
            try
            {
                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                if (File.Exists(logoPath))
                {
                    var logo = new Image(ImageDataFactory.Create(logoPath));
                    logo.SetWidth(60);
                    logo.SetHeight(60);
                    document.Add(logo);
                }
            }
            catch
            {
                // Si no existe el logo, continuar sin él
            }

            // Título principal
            document.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                .SetFont(_fontTitle)
                .SetFontSize(16)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(5));

            document.Add(new Paragraph("REPORTE DE INSCRIPCIONES")
                .SetFont(_fontTitle)
                .SetFontSize(14)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(10));

            // Información de filtros si aplica
            if (esFiltrado)
            {
                var filtrosInfo = new Paragraph("Filtros aplicados:")
                    .SetFont(_fontBold)
                    .SetFontSize(10)
                    .SetMarginBottom(5);

                if (carreraId.HasValue && carreraId.Value > 0)
                {
                    var carrera = _context.Carreras.FirstOrDefault(c => c.CarreraId == carreraId.Value);
                    if (carrera != null)
                    {
                        filtrosInfo.Add(new Text($" Carrera: {carrera.NombreCarrera}"));
                    }
                }

                if (!string.IsNullOrEmpty(genero))
                {
                    var generoText = genero == "0" ? "Hombres" : "Mujeres";
                    filtrosInfo.Add(new Text($" Género: {generoText}"));
                }

                document.Add(filtrosInfo);
            }

            // Línea separadora
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(15));
        }

        private async Task AgregarContenidoReporte(Document document, List<IGrouping<Carrera, Grupo>> carrerasConGrupos, List<Inscripcion> inscripciones)
        {
            int totalHombres = 0;
            int totalMujeres = 0;

            foreach (var carreraGrupo in carrerasConGrupos)
            {
                var carrera = carreraGrupo.Key;
                var grupos = carreraGrupo.ToList();

                // Título de la carrera
                document.Add(new Paragraph($"{carrera?.NombreCarrera?.ToUpper()}")
                    .SetFont(_fontBold)
                    .SetFontSize(15)
                    .SetFontColor(new DeviceRgb(0, 74, 0))
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(18)
                    .SetMarginBottom(8)
                    .SetKeepWithNext(true));

                int hombresCarrera = 0;
                int mujeresCarrera = 0;

                foreach (var grupo in grupos.OrderBy(g => g.Nombre))
                {
                    // Obtener estudiantes inscritos en este grupo
                    var estudiantesGrupo = grupo.MateriasGrupos?
                        .SelectMany(mg => mg.MateriasInscritas ?? Enumerable.Empty<MateriasInscritas>())
                        .Select(mi => mi.Alumno)
                        .Distinct()
                        .ToList() ?? new List<Alumno>();

                    if (!estudiantesGrupo.Any()) continue;

                    // Título del grupo
                    var grupoParrafo = new Paragraph($"Grupo: {grupo.Nombre}")
                        .SetFont(_fontBold)
                        .SetFontSize(11)
                        .SetFontColor(new DeviceRgb(0, 102, 204))
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetMarginTop(6)
                        .SetMarginBottom(4)
                        .SetBackgroundColor(new DeviceRgb(230, 240, 255))
                        .SetBorder(new SolidBorder(new DeviceRgb(0, 102, 204), 0.5f))
                        .SetKeepWithNext(true);
                    document.Add(grupoParrafo);

                    // Tabla de estudiantes
                    var tablaEstudiantes = new Table(new float[] { 1, 3, 2, 1 })
                        .SetWidth(UnitValue.CreatePercentValue(100))
                        .SetFontSize(8);

                    // Encabezados
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("No.").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Nombre Completo").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Carnet").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Género").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));

                    int contador = 1;
                    int hombresGrupo = 0;
                    int mujeresGrupo = 0;

                    foreach (var estudiante in estudiantesGrupo.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres))
                    {
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(contador.ToString())).SetTextAlignment(TextAlignment.CENTER));
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph($"{estudiante.Apellidos}, {estudiante.Nombres}")));
                        string carnetOCorreo = estudiante.Carnet ?? estudiante.Email?.Split('@')[0] ?? "";
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(carnetOCorreo)).SetTextAlignment(TextAlignment.CENTER));

                        var generoText = estudiante.Genero == 0 ? "M" : estudiante.Genero == 1 ? "F" : "O";
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(generoText)).SetTextAlignment(TextAlignment.CENTER));

                        if (estudiante.Genero == 0) hombresGrupo++;
                        else if (estudiante.Genero == 1) mujeresGrupo++;

                        contador++;
                    }

                    document.Add(tablaEstudiantes);

                    // Resumen del grupo
                    document.Add(new Paragraph($"Resumen del grupo: {hombresGrupo} hombres, {mujeresGrupo} mujeres")
                        .SetFont(_fontBold)
                        .SetFontSize(8)
                        .SetMarginTop(5)
                        .SetMarginBottom(10)
                        .SetKeepTogether(true));

                    hombresCarrera += hombresGrupo;
                    mujeresCarrera += mujeresGrupo;
                }

                // Resumen de la carrera
                document.Add(new Paragraph($"RESUMEN CARRERA: {hombresCarrera} hombres, {mujeresCarrera} mujeres")
                    .SetFont(_fontBold)
                    .SetFontSize(10)
                    .SetFontColor(new DeviceRgb(0, 74, 0))
                    .SetMarginTop(10)
                    .SetMarginBottom(15)
                    .SetKeepTogether(true));

                totalHombres += hombresCarrera;
                totalMujeres += mujeresCarrera;
            }

            // Resumen general
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(10));

            document.Add(new Paragraph($"RESUMEN GENERAL: {totalHombres} hombres, {totalMujeres} mujeres")
                .SetFont(_fontBold)
                .SetFontSize(12)
                .SetFontColor(new DeviceRgb(0, 74, 0))
                .SetTextAlignment(TextAlignment.CENTER));
        }

        private void AgregarPiePagina(Document document, int totalRegistros)
        {
            // Línea separadora
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(5));

            document.Add(new Paragraph($"Reporte generado el: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER));

            document.Add(new Paragraph($"Total de estudiantes: {totalRegistros}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER));

            // Información del documento
            document.Add(new Paragraph($"Documento generado el {DateTime.Now:dd/MM/yyyy} a las {DateTime.Now:HH:mm:ss}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(5));
        }


    }
} 