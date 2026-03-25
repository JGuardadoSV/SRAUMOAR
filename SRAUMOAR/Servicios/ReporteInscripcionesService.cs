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

        public async Task<byte[]> GenerarReporteCompletoAsync(int? cicloId = null)
        {
            return await GenerarReporteAsync(cicloId, null, null, false);
        }

        public async Task<byte[]> GenerarReporteFiltradoAsync(int? cicloId, int? carreraId, string? genero)
        {
            return await GenerarReporteAsync(cicloId, carreraId, genero, true);
        }

        private async Task<byte[]> GenerarReporteAsync(int? cicloId, int? carreraId, string? genero, bool esFiltrado)
        {
            try
            {
                var cicloactual = await ObtenerCicloReporteAsync(cicloId);
                if (cicloactual == null)
                {
                    throw new InvalidOperationException("No hay un ciclo disponible para generar el reporte");
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
                        .ThenInclude(mg => mg.Materia) // incluir Materia para poder leer su Ciclo
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

                // Calcular alumnos únicos y totales globales de género a partir de las inscripciones
                var alumnoIdsPermitidos = inscripciones
                    .Where(i => i.Alumno != null)
                    .Select(i => i.AlumnoId)
                    .ToHashSet();

                var alumnosCanonicos = ResolverGruposCanonicosPorAlumno(grupos, alumnoIdsPermitidos);
                int totalHombresGlobal = alumnosCanonicos.Count(a => a.Alumno.Genero == 0);
                int totalMujeresGlobal = alumnosCanonicos.Count(a => a.Alumno.Genero == 1);

                using var memoryStream = new MemoryStream();
                using var writer = new PdfWriter(memoryStream);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                // === Numeración de página se agregará al final del documento ===

                // Configurar márgenes
                document.SetMargins(30, 30, 30, 30);

                // Agregar encabezado
                AgregarEncabezado(document, cicloactual, esFiltrado, carreraId, genero);

                // Agregar contenido del reporte
                await AgregarContenidoReporte(document, carrerasConGrupos, inscripciones, totalHombresGlobal, totalMujeresGlobal);

                // Agregar pie de página (usando total de alumnos únicos)
                AgregarPiePagina(document, alumnosCanonicos.Count);

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

        private async Task<Ciclo?> ObtenerCicloReporteAsync(int? cicloId)
        {
            if (cicloId.HasValue && cicloId.Value > 0)
            {
                return await _context.Ciclos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == cicloId.Value);
            }

            return await _context.Ciclos
                .AsNoTracking()
                .Where(c => c.Activo)
                .FirstOrDefaultAsync();
        }

        private void AgregarEncabezado(Document document, Ciclo cicloActual, bool esFiltrado, int? carreraId, string? genero)
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

            document.Add(new Paragraph($"Ciclo consultado: {cicloActual.NCiclo} - {cicloActual.anio}")
                .SetFont(_fontBold)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(5));

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

        private string GetCarnetFromAlumno(Alumno estudiante)
        {
            if (!string.IsNullOrWhiteSpace(estudiante.Carnet))
            {
                return estudiante.Carnet;
            }

            if (!string.IsNullOrWhiteSpace(estudiante.Email) && estudiante.Email.EndsWith("@umoar.edu.sv", StringComparison.OrdinalIgnoreCase))
            {
                var partes = estudiante.Email.Split('@');
                if (partes.Length > 1)
                {
                    return partes[0];
                }
            }

            return string.Empty;
        }

        private string GetRomanFromCiclo(int? ciclo)
        {
            if (!ciclo.HasValue || ciclo.Value <= 0) return string.Empty;

            return ciclo.Value switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                6 => "VI",
                7 => "VII",
                8 => "VIII",
                9 => "IX",
                10 => "X",
                _ => ciclo.Value.ToString()
            };
        }

        private List<AlumnoGrupoCanonico> ResolverGruposCanonicosPorAlumno(List<Grupo> grupos, HashSet<int> alumnoIdsPermitidos)
        {
            var asignaciones = grupos
                .SelectMany(g => (g.MateriasGrupos ?? Enumerable.Empty<MateriasGrupo>())
                    .SelectMany(mg => (mg.MateriasInscritas ?? Enumerable.Empty<MateriasInscritas>())
                        .Where(mi => mi.Alumno != null && alumnoIdsPermitidos.Contains(mi.AlumnoId))
                        .Select(mi => new
                        {
                            Grupo = g,
                            Alumno = mi.Alumno!,
                            CicloMateria = g.EsEspecializacion ? (int?)null : mg.Materia?.Ciclo,
                            mg.MateriaId
                        })))
                .ToList();

            var totalMateriasPorAlumno = asignaciones
                .GroupBy(x => x.Alumno.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.MateriaId)
                          .Distinct()
                          .Count());

            var gruposCanonicos = asignaciones
                .GroupBy(x => new { x.Alumno.AlumnoId, x.Grupo.GrupoId })
                .Select(g => new
                {
                    Alumno = g.First().Alumno,
                    Grupo = g.First().Grupo,
                    CicloMayorGrupo = g.Max(x => x.CicloMateria)
                })
                .GroupBy(x => x.Alumno.AlumnoId)
                .Select(g => g
                    .OrderByDescending(x => x.CicloMayorGrupo ?? int.MinValue)
                    .ThenBy(x => x.Grupo.GrupoId)
                    .Select(x => new AlumnoGrupoCanonico
                    {
                        Alumno = x.Alumno,
                        GrupoId = x.Grupo.GrupoId,
                        Ciclo = x.CicloMayorGrupo,
                        TotalMaterias = totalMateriasPorAlumno.TryGetValue(x.Alumno.AlumnoId, out var totalMaterias)
                            ? totalMaterias
                            : 0
                    })
                    .First())
                .ToList();

            return gruposCanonicos;
        }

        private async Task AgregarContenidoReporte(Document document, List<IGrouping<Carrera, Grupo>> carrerasConGrupos, List<Inscripcion> inscripciones, int totalHombresGlobal, int totalMujeresGlobal)
        {
            var alumnoIdsPermitidos = inscripciones
                .Where(i => i.Alumno != null)
                .Select(i => i.AlumnoId)
                .ToHashSet();

            var gruposReporte = carrerasConGrupos
                .SelectMany(cg => cg)
                .ToList();

            var alumnosCanonicos = ResolverGruposCanonicosPorAlumno(gruposReporte, alumnoIdsPermitidos);
            var alumnosCanonicosPorGrupo = alumnosCanonicos
                .GroupBy(x => x.GrupoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.Ciclo ?? int.MaxValue)
                          .ThenBy(x => x.Alumno.Apellidos)
                          .ThenBy(x => x.Alumno.Nombres)
                          .ToList());

            foreach (var carreraGrupo in carrerasConGrupos)
            {
                var carrera = carreraGrupo.Key;
                var grupos = carreraGrupo
                    .OrderBy(g => g.EsEspecializacion) // primero grupos normales (false), luego especialización (true)
                    .ThenBy(g => g.Nombre)
                    .ToList();

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

                // Recorrer todos los grupos (incluyendo especialización) para mostrarlos en el reporte
                foreach (var grupo in grupos)
                {
                    // Obtener estudiantes asignados canonicamente a este grupo
                    var estudiantesGrupo = alumnosCanonicosPorGrupo.TryGetValue(grupo.GrupoId, out var alumnosGrupo)
                        ? alumnosGrupo
                        : new List<AlumnoGrupoCanonico>();

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

                    // Tabla de estudiantes (No., Nombre, Carnet, Género, Ciclo, Total Materias)
                    var tablaEstudiantes = new Table(new float[] { 1, 3, 2, 1, 1, 1.3f })
                        .SetWidth(UnitValue.CreatePercentValue(100))
                        .SetFontSize(8);

                    // Encabezados
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("No.").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Nombre Completo").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Carnet").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Género").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Ciclo").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaEstudiantes.AddHeaderCell(new Cell().Add(new Paragraph("Total Materias").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));

                    int contador = 1;
                    int hombresGrupo = 0;
                    int mujeresGrupo = 0;

                    foreach (var ac in estudiantesGrupo)
                    {
                        var estudiante = ac.Alumno;
                        var cicloTexto = GetRomanFromCiclo(ac.Ciclo);

                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(contador.ToString())).SetTextAlignment(TextAlignment.CENTER));
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph($"{estudiante.Apellidos}, {estudiante.Nombres}")));
                        string carnet = GetCarnetFromAlumno(estudiante);
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(carnet)).SetTextAlignment(TextAlignment.CENTER));

                        var generoText = estudiante.Genero == 0 ? "M" : estudiante.Genero == 1 ? "F" : "O";
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(generoText)).SetTextAlignment(TextAlignment.CENTER));

                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(cicloTexto)).SetTextAlignment(TextAlignment.CENTER));
                        tablaEstudiantes.AddCell(new Cell().Add(new Paragraph(ac.TotalMaterias.ToString())).SetTextAlignment(TextAlignment.CENTER));

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
                document.Add(new Paragraph($"RESUMEN CARRERA (por grupos): {hombresCarrera} hombres, {mujeresCarrera} mujeres")
                    .SetFont(_fontBold)
                    .SetFontSize(10)
                    .SetFontColor(new DeviceRgb(0, 74, 0))
                    .SetMarginTop(10)
                    .SetMarginBottom(15)
                    .SetKeepTogether(true));
            }

            // Separador antes de los resúmenes globales
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(10));

            // Resumen general basado en alumnos únicos de inscripciones
            document.Add(new Paragraph($"RESUMEN GENERAL: {totalHombresGlobal} hombres, {totalMujeresGlobal} mujeres")
                .SetFont(_fontBold)
                .SetFontSize(12)
                .SetFontColor(new DeviceRgb(0, 74, 0))
                .SetTextAlignment(TextAlignment.CENTER));
        }

        private void AgregarPiePagina(Document document, int totalEstudiantes)
        {
            // Agrupar todo el pie en un solo bloque para evitar que se corte entre páginas
            var pie = new Div()
                .SetKeepTogether(true);

            pie.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(5));

            pie.Add(new Paragraph($"Reporte generado el: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER));

            pie.Add(new Paragraph($"Total de estudiantes: {totalEstudiantes}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER));

            pie.Add(new Paragraph($"Documento generado el {DateTime.Now:dd/MM/yyyy} a las {DateTime.Now:HH:mm:ss}")
                .SetFont(_fontNormal)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(5));

            document.Add(pie);
        }

        private class AlumnoGrupoCanonico
        {
            public required Alumno Alumno { get; set; }
            public int GrupoId { get; set; }
            public int? Ciclo { get; set; }
            public int TotalMaterias { get; set; }
        }

    }
} 
