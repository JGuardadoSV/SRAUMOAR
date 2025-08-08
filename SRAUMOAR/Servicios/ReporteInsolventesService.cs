using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Colecturia;
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

namespace SRAUMOAR.Servicios
{
    public class ReporteInsolventesService
    {
        private readonly Contexto _context;
        private readonly PdfFont _fontNormal;
        private readonly PdfFont _fontBold;
        private readonly PdfFont _fontTitle;

        public ReporteInsolventesService(Contexto context)
        {
            _context = context;
            _fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            _fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
            _fontTitle = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
        }

        public async Task<byte[]> GenerarReporteCompletoAsync(bool incluirAlumnosConBeca = false)
        {
            return await GenerarReporteAsync(null, null, false, incluirAlumnosConBeca);
        }

        public async Task<byte[]> GenerarReporteFiltradoAsync(int? carreraId, decimal? montoMinimo, bool incluirAlumnosConBeca = false)
        {
            return await GenerarReporteAsync(carreraId, montoMinimo, true, incluirAlumnosConBeca);
        }

        private async Task<byte[]> GenerarReporteAsync(int? carreraId, decimal? montoMinimo, bool esFiltrado, bool incluirAlumnosConBeca)
        {
            try
            {
                var cicloActual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
                if (cicloActual == null)
                {
                    throw new InvalidOperationException("No hay un ciclo activo");
                }

                // Obtener aranceles obligatorios del ciclo actual que YA VENCIERON
                var arancelesObligatorios = await _context.Aranceles
                    .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                    .ToListAsync();

                // Obtener aranceles vencidos para el resumen
                var arancelesVencidos = await _context.Aranceles
                    .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaFin.HasValue && a.FechaFin.Value.Date < DateTime.Now.Date)
                    .OrderBy(a => a.FechaFin)
                    .ToListAsync();

                Console.WriteLine($"\n=== RESUMEN DE ARANCELES VENCIDOS ===");
                Console.WriteLine($"Total aranceles vencidos: {arancelesVencidos.Count}");
                foreach (var arancel in arancelesVencidos)
                {
                    var diasVencido = (DateTime.Now.Date - arancel.FechaFin.Value.Date).Days;
                    Console.WriteLine($"- {arancel.Nombre}: Vencido el {arancel.FechaFin:dd/MM/yyyy} (hace {diasVencido} días) - Costo: ${arancel.Costo:F2} - Mora: ${arancel.ValorMora:F2}");
                }
                                 Console.WriteLine($"=== FIN RESUMEN ===\n");

                 // Guardar información de aranceles vencidos para el reporte
                 var arancelesVencidosResumen = arancelesVencidos.Select(a => new ArancelVencidoResumen
                 {
                     Nombre = a.Nombre,
                     FechaVencimiento = a.FechaFin.Value,
                     DiasVencido = (DateTime.Now.Date - a.FechaFin.Value.Date).Days,
                     Costo = a.Costo,
                     ValorMora = a.ValorMora
                 }).ToList();

                 if (!arancelesObligatorios.Any())
                {
                    throw new InvalidOperationException("No hay aranceles obligatorios vencidos para el ciclo actual");
                }

                // Obtener alumnos inscritos en el ciclo actual
                var query = _context.Inscripciones
                    .Include(i => i.Alumno)
                        .ThenInclude(a => a.Carrera)
                    .Include(i => i.Ciclo)
                    .Where(i => i.CicloId == cicloActual.Id && i.Activa);

                // Filtrar por alumnos con beca según la opción seleccionada
                if (!incluirAlumnosConBeca)
                {
                    // Excluir alumnos que tienen beca en el ciclo actual
                    var alumnosConBeca = await _context.Becados
                        .Where(b => b.CicloId == cicloActual.Id && b.Estado)
                        .Select(b => b.AlumnoId)
                        .ToListAsync();

                    query = query.Where(i => !alumnosConBeca.Contains(i.AlumnoId));
                }

                if (carreraId.HasValue && carreraId.Value > 0)
                {
                    query = query.Where(i => i.Alumno.CarreraId == carreraId.Value);
                }

                var inscripciones = await query.ToListAsync();

                // Obtener grupos del ciclo actual para agrupación
                var grupos = await _context.Grupo
                    .Include(g => g.Carrera)
                    .Include(g => g.MateriasGrupos)
                        .ThenInclude(mg => mg.MateriasInscritas)
                            .ThenInclude(mi => mi.Alumno)
                    .Where(g => g.CicloId == cicloActual.Id && g.Activo)
                    .ToListAsync();

                // Procesar alumnos insolventes
                var alumnosInsolventes = await ProcesarAlumnosInsolventesAsync(inscripciones, arancelesObligatorios, cicloActual.Id);



                // Agrupar por carrera y grupo
                var reporteAgrupado = AgruparPorCarreraYGrupo(alumnosInsolventes, grupos);

                using var memoryStream = new MemoryStream();
                using var writer = new PdfWriter(memoryStream);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                // Configurar márgenes
                document.SetMargins(30, 30, 30, 30);

                // Agregar encabezado
                AgregarEncabezado(document, esFiltrado, carreraId, montoMinimo);

                                 // Agregar contenido del reporte
                 await AgregarContenidoReporte(document, reporteAgrupado, alumnosInsolventes, arancelesVencidosResumen);

                // Agregar pie de página
                AgregarPiePagina(document, alumnosInsolventes.Count);

                document.Close();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar reporte de insolventes: {ex.Message}", ex);
            }
        }

        private async Task<List<AlumnoInsolvente>> ProcesarAlumnosInsolventesAsync(
            List<Inscripcion> inscripciones, 
            List<Arancel> arancelesObligatorios, 
            int cicloId)
        {
            var alumnosInsolventes = new List<AlumnoInsolvente>();
            var alumnosUnicos = new Dictionary<int, AlumnoInsolvente>();

            // Obtener todos los pagos de una vez para evitar consultas en bucle
            var pagosExistentes = await _context.CobrosArancel
                .Include(ca => ca.DetallesCobroArancel)
                .Where(ca => ca.CicloId == cicloId)
                .ToListAsync();

            // Crear un diccionario para acceso rápido
            var pagosPorAlumno = pagosExistentes
                .GroupBy(ca => ca.AlumnoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(ca => ca.DetallesCobroArancel)
                          .Select(dca => dca.ArancelId)
                          .ToHashSet()
                );

            foreach (var inscripcion in inscripciones)
            {
                var alumno = inscripcion.Alumno;
                
                // Excluir alumnos con excepciones
                if (alumno.PermiteInscripcionSinPago)
                    continue;

                // Verificar si ya procesamos este alumno
                if (alumnosUnicos.ContainsKey(alumno.AlumnoId))
                    continue;

                var arancelesPendientes = new List<ArancelPendiente>();

                // Obtener aranceles pagados por este alumno
                var arancelesPagados = pagosPorAlumno.GetValueOrDefault(alumno.AlumnoId, new HashSet<int>());

                foreach (var arancel in arancelesObligatorios)
                {
                    // Verificar si el alumno ya pagó este arancel
                    if (!arancelesPagados.Contains(arancel.ArancelId))
                    {
                        // Calcular mora solo si el alumno no está exento y el arancel ya venció
                        var mora = 0m;
                        if (!alumno.ExentoMora && arancel.FechaFin.HasValue && arancel.FechaFin.Value.Date < DateTime.Now.Date)
                        {
                            mora = arancel.ValorMora;
                        }

                        arancelesPendientes.Add(new ArancelPendiente
                        {
                            ArancelId = arancel.ArancelId,
                            NombreArancel = arancel.Nombre,
                            CostoOriginal = arancel.Costo,
                            Mora = mora,
                            TotalConMora = arancel.Costo + mora,
                            FechaVencimiento = arancel.FechaFin,
                            EstaVencido = arancel.EstaVencido
                        });
                    }
                }

                if (arancelesPendientes.Any())
                {
                    var alumnoInsolvente = new AlumnoInsolvente
                    {
                        AlumnoId = alumno.AlumnoId,
                        Nombres = alumno.Nombres,
                        Apellidos = alumno.Apellidos,
                        Carrera = alumno.Carrera?.NombreCarrera,
                        Carnet = alumno.Carnet,
                        Email = alumno.Email,
                        ArancelesPendientes = arancelesPendientes,
                        TotalPendiente = arancelesPendientes.Sum(ap => ap.CostoOriginal),
                        TotalMora = arancelesPendientes.Sum(ap => ap.Mora),
                        TotalGeneral = arancelesPendientes.Sum(ap => ap.TotalConMora)
                    };

                    alumnosUnicos[alumno.AlumnoId] = alumnoInsolvente;
                    alumnosInsolventes.Add(alumnoInsolvente);
                }
            }

            return alumnosInsolventes;
        }

        private List<CarreraInsolvente> AgruparPorCarreraYGrupo(List<AlumnoInsolvente> alumnosInsolventes, List<Grupo> grupos)
        {
            var reporteAgrupado = new List<CarreraInsolvente>();

            // Crear un diccionario para rastrear alumnos únicos y sus grupos
            var alumnosUnicos = new Dictionary<int, AlumnoInsolvente>();
            var gruposPorAlumno = new Dictionary<int, List<string>>();

            // Agrupar por carrera
            var carrerasConAlumnos = alumnosInsolventes
                .GroupBy(a => a.Carrera)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var carreraGrupo in carrerasConAlumnos)
            {
                var carrera = new CarreraInsolvente
                {
                    NombreCarrera = carreraGrupo.Key ?? "Sin Carrera",
                    Grupos = new List<GrupoInsolvente>()
                };

                // Obtener grupos de esta carrera
                var gruposCarrera = grupos
                    .Where(g => g.Carrera?.NombreCarrera == carreraGrupo.Key)
                    .ToList();

                foreach (var grupo in gruposCarrera.OrderBy(g => g.Nombre))
                {
                    // Obtener alumnos de este grupo
                    var alumnosGrupo = grupo.MateriasGrupos?
                        .SelectMany(mg => mg.MateriasInscritas ?? Enumerable.Empty<MateriasInscritas>())
                        .Select(mi => mi.Alumno)
                        .Distinct()
                        .ToList() ?? new List<Alumno>();

                    var alumnosInsolventesGrupo = alumnosInsolventes
                        .Where(a => alumnosGrupo.Any(ag => ag.AlumnoId == a.AlumnoId))
                        .ToList();

                    if (alumnosInsolventesGrupo.Any())
                    {
                        // Procesar alumnos únicos para este grupo
                        var alumnosUnicosGrupo = new List<AlumnoInsolvente>();
                        
                        foreach (var alumno in alumnosInsolventesGrupo)
                        {
                            if (!alumnosUnicos.ContainsKey(alumno.AlumnoId))
                            {
                                // Es la primera vez que vemos este alumno
                                alumnosUnicos[alumno.AlumnoId] = alumno;
                                gruposPorAlumno[alumno.AlumnoId] = new List<string> { grupo.Nombre };
                                alumnosUnicosGrupo.Add(alumno);
                            }
                            else
                            {
                                // El alumno ya existe, agregar este grupo a su lista
                                if (!gruposPorAlumno[alumno.AlumnoId].Contains(grupo.Nombre))
                                {
                                    gruposPorAlumno[alumno.AlumnoId].Add(grupo.Nombre);
                                }
                                
                                // Agregar una copia del alumno con información de repetición
                                var alumnoConRepeticion = new AlumnoInsolvente
                                {
                                    AlumnoId = alumno.AlumnoId,
                                    Nombres = alumno.Nombres,
                                    Apellidos = alumno.Apellidos,
                                    Carrera = alumno.Carrera,
                                    Carnet = alumno.Carnet,
                                    Email = alumno.Email,
                                    ArancelesPendientes = alumno.ArancelesPendientes,
                                    TotalPendiente = alumno.TotalPendiente,
                                    TotalMora = alumno.TotalMora,
                                    TotalGeneral = alumno.TotalGeneral,
                                    // Agregar información de repetición
                                    EsRepeticion = true,
                                    GruposAdicionales = gruposPorAlumno[alumno.AlumnoId].Count > 1 
                                        ? string.Join(", ", gruposPorAlumno[alumno.AlumnoId].Where(g => g != grupo.Nombre))
                                        : ""
                                };
                                
                                alumnosUnicosGrupo.Add(alumnoConRepeticion);
                            }
                        }

                        if (alumnosUnicosGrupo.Any())
                        {
                            var grupoInsolvente = new GrupoInsolvente
                            {
                                NombreGrupo = grupo.Nombre,
                                AlumnosInsolventes = alumnosUnicosGrupo,
                                TotalPendiente = alumnosUnicosGrupo.Sum(a => a.TotalPendiente),
                                TotalMora = alumnosUnicosGrupo.Sum(a => a.TotalMora),
                                TotalGeneral = alumnosUnicosGrupo.Sum(a => a.TotalGeneral)
                            };

                            carrera.Grupos.Add(grupoInsolvente);
                        }
                    }
                }

                if (carrera.Grupos.Any())
                {
                    carrera.TotalPendiente = carrera.Grupos.Sum(g => g.TotalPendiente);
                    carrera.TotalMora = carrera.Grupos.Sum(g => g.TotalMora);
                    carrera.TotalGeneral = carrera.Grupos.Sum(g => g.TotalGeneral);
                    reporteAgrupado.Add(carrera);
                }
            }

            return reporteAgrupado;
        }

        private void AgregarEncabezado(Document document, bool esFiltrado, int? carreraId, decimal? montoMinimo)
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

            document.Add(new Paragraph("REPORTE DE ALUMNOS INSOLVENTES")
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

                

                document.Add(filtrosInfo);
            }

            // Línea separadora
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(15));
        }

                 private async Task AgregarContenidoReporte(Document document, List<CarreraInsolvente> carrerasConGrupos, List<AlumnoInsolvente> alumnosInsolventes, List<ArancelVencidoResumen> arancelesVencidos)
        {
            decimal totalGeneralPendiente = 0;
            decimal totalGeneralMora = 0;
            decimal totalGeneralTotal = 0;

            foreach (var carrera in carrerasConGrupos)
            {
                // Título de la carrera
                document.Add(new Paragraph($"{carrera.NombreCarrera?.ToUpper()}")
                    .SetFont(_fontBold)
                    .SetFontSize(15)
                    .SetFontColor(new DeviceRgb(0, 74, 0))
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(18)
                    .SetMarginBottom(8)
                    .SetKeepWithNext(true));

                foreach (var grupo in carrera.Grupos)
                {
                    // Título del grupo
                    var grupoParrafo = new Paragraph($"Grupo: {grupo.NombreGrupo}")
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

                    // Tabla de alumnos insolventes
                    var tablaAlumnos = new Table(new float[] { 1, 3, 2, 2, 2, 2 })
                        .SetWidth(UnitValue.CreatePercentValue(100))
                        .SetFontSize(8);

                    // Encabezados
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("No.").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("Nombre Completo").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("Carnet").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("Pendiente").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("Mora").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                    tablaAlumnos.AddHeaderCell(new Cell().Add(new Paragraph("Total").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));

                    int contador = 1;
                    foreach (var alumno in grupo.AlumnosInsolventes.OrderBy(a => a.Apellidos).ThenBy(a => a.Nombres))
                    {
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph(contador.ToString())).SetTextAlignment(TextAlignment.CENTER));
                        
                        // Agregar información de repetición si aplica
                        string nombreCompleto = $"{alumno.Apellidos}, {alumno.Nombres}";
                        if (alumno.EsRepeticion && !string.IsNullOrEmpty(alumno.GruposAdicionales))
                        {
                            nombreCompleto += $" (Repetición - También en: {alumno.GruposAdicionales})";
                        }
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph(nombreCompleto)));
                        
                        string carnetOCorreo = alumno.Carnet ?? alumno.Email?.Split('@')[0] ?? "";
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph(carnetOCorreo)).SetTextAlignment(TextAlignment.CENTER));
                        
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph($"${alumno.TotalPendiente:F2}")).SetTextAlignment(TextAlignment.RIGHT));
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph($"${alumno.TotalMora:F2}")).SetTextAlignment(TextAlignment.RIGHT));
                        tablaAlumnos.AddCell(new Cell().Add(new Paragraph($"${alumno.TotalGeneral:F2}")).SetTextAlignment(TextAlignment.RIGHT));

                        contador++;
                    }

                    document.Add(tablaAlumnos);

                    // Resumen del grupo
                    document.Add(new Paragraph($"Resumen del grupo: {grupo.AlumnosInsolventes.Count} alumnos insolventes - Total: ${grupo.TotalGeneral:F2}")
                        .SetFont(_fontBold)
                        .SetFontSize(8)
                        .SetMarginTop(5)
                        .SetMarginBottom(10)
                        .SetKeepTogether(true));
                }

                // Resumen de la carrera
                document.Add(new Paragraph($"RESUMEN CARRERA: {carrera.Grupos.Sum(g => g.AlumnosInsolventes.Count)} alumnos insolventes - Total: ${carrera.TotalGeneral:F2}")
                    .SetFont(_fontBold)
                    .SetFontSize(10)
                    .SetFontColor(new DeviceRgb(0, 74, 0))
                    .SetMarginTop(10)
                    .SetMarginBottom(15)
                    .SetKeepTogether(true));

                totalGeneralPendiente += carrera.TotalPendiente;
                totalGeneralMora += carrera.TotalMora;
                totalGeneralTotal += carrera.TotalGeneral;
            }

            // Resumen general
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(10));

            document.Add(new Paragraph($"RESUMEN GENERAL: {alumnosInsolventes.Count} alumnos insolventes")
                .SetFont(_fontBold)
                .SetFontSize(12)
                .SetFontColor(new DeviceRgb(139, 0, 0))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(5));

                         document.Add(new Paragraph($"Total Pendiente: ${totalGeneralPendiente:F2} | Total Mora: ${totalGeneralMora:F2} | Total General: ${totalGeneralTotal:F2}")
                 .SetFont(_fontBold)
                 .SetFontSize(10)
                 .SetFontColor(new DeviceRgb(139, 0, 0))
                 .SetTextAlignment(TextAlignment.CENTER)
                 .SetMarginBottom(15));

             // Agregar resumen de aranceles vencidos si existen
             if (arancelesVencidos.Any())
             {
                 document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                     .SetFontSize(8)
                     .SetTextAlignment(TextAlignment.CENTER)
                     .SetMarginTop(15)
                     .SetMarginBottom(10));

                 document.Add(new Paragraph($"RESUMEN DE ARANCELES VENCIDOS ({arancelesVencidos.Count} aranceles)")
                     .SetFont(_fontBold)
                     .SetFontSize(12)
                     .SetFontColor(new DeviceRgb(255, 140, 0))
                     .SetTextAlignment(TextAlignment.CENTER)
                     .SetMarginBottom(10));

                 // Tabla de aranceles vencidos
                 var tablaArancelesVencidos = new Table(new float[] { 3, 2, 2, 2, 2 })
                     .SetWidth(UnitValue.CreatePercentValue(100))
                     .SetFontSize(8);

                 // Encabezados
                 tablaArancelesVencidos.AddHeaderCell(new Cell().Add(new Paragraph("Arancel").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                 tablaArancelesVencidos.AddHeaderCell(new Cell().Add(new Paragraph("Fecha Venc.").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                 tablaArancelesVencidos.AddHeaderCell(new Cell().Add(new Paragraph("Días Vencido").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                 tablaArancelesVencidos.AddHeaderCell(new Cell().Add(new Paragraph("Costo").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));
                 tablaArancelesVencidos.AddHeaderCell(new Cell().Add(new Paragraph("Total + Mora").SetFont(_fontBold)).SetTextAlignment(TextAlignment.CENTER));

                 decimal totalCostoVencido = 0;
                 decimal totalMoraVencido = 0;

                 foreach (var arancel in arancelesVencidos.OrderBy(a => a.FechaVencimiento))
                 {
                     tablaArancelesVencidos.AddCell(new Cell().Add(new Paragraph(arancel.Nombre)));
                     tablaArancelesVencidos.AddCell(new Cell().Add(new Paragraph(arancel.FechaVencimiento.ToString("dd/MM/yyyy"))).SetTextAlignment(TextAlignment.CENTER));
                     tablaArancelesVencidos.AddCell(new Cell().Add(new Paragraph($"{arancel.DiasVencido} días")).SetTextAlignment(TextAlignment.CENTER));
                     tablaArancelesVencidos.AddCell(new Cell().Add(new Paragraph($"${arancel.Costo:F2}")).SetTextAlignment(TextAlignment.RIGHT));
                     tablaArancelesVencidos.AddCell(new Cell().Add(new Paragraph($"${(arancel.Costo + arancel.ValorMora):F2}")).SetTextAlignment(TextAlignment.RIGHT));

                     totalCostoVencido += arancel.Costo;
                     totalMoraVencido += arancel.ValorMora;
                 }

                 document.Add(tablaArancelesVencidos);

                 // Totales de aranceles vencidos
                 document.Add(new Paragraph($"Total Costo Vencido: ${totalCostoVencido:F2} | Total Mora Vencido: ${totalMoraVencido:F2} | Total General: ${(totalCostoVencido + totalMoraVencido):F2}")
                     .SetFont(_fontBold)
                     .SetFontSize(10)
                     .SetFontColor(new DeviceRgb(255, 140, 0))
                     .SetTextAlignment(TextAlignment.CENTER)
                     .SetMarginTop(10)
                     .SetMarginBottom(15));
             }
        }

        private void AgregarPiePagina(Document document, int totalRegistros)
        {
            document.Add(new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(5));

            document.Add(new Paragraph($"Reporte generado el: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}")
                .SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(ColorConstants.GRAY));

            document.Add(new Paragraph($"Total de alumnos insolventes: {totalRegistros}")
                .SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(ColorConstants.GRAY));
        }
    }

    // Clases de modelo para el reporte
    public class AlumnoInsolvente
    {
        public int AlumnoId { get; set; }
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string? Carrera { get; set; }
        public string? Carnet { get; set; }
        public string? Email { get; set; }
        public List<ArancelPendiente> ArancelesPendientes { get; set; } = new();
        public decimal TotalPendiente { get; set; }
        public decimal TotalMora { get; set; }
        public decimal TotalGeneral { get; set; }
        public bool EsRepeticion { get; set; } = false;
        public string GruposAdicionales { get; set; } = "";
    }

    public class ArancelPendiente
    {
        public int ArancelId { get; set; }
        public string NombreArancel { get; set; } = "";
        public decimal CostoOriginal { get; set; }
        public decimal Mora { get; set; }
        public decimal TotalConMora { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public bool EstaVencido { get; set; }
    }

    public class CarreraInsolvente
    {
        public string NombreCarrera { get; set; } = "";
        public List<GrupoInsolvente> Grupos { get; set; } = new();
        public decimal TotalPendiente { get; set; }
        public decimal TotalMora { get; set; }
        public decimal TotalGeneral { get; set; }
    }

         public class GrupoInsolvente
     {
         public string NombreGrupo { get; set; } = "";
         public List<AlumnoInsolvente> AlumnosInsolventes { get; set; } = new();
         public decimal TotalPendiente { get; set; }
         public decimal TotalMora { get; set; }
         public decimal TotalGeneral { get; set; }
     }

     public class ArancelVencidoResumen
     {
         public string Nombre { get; set; } = "";
         public DateTime FechaVencimiento { get; set; }
         public int DiasVencido { get; set; }
         public decimal Costo { get; set; }
         public decimal ValorMora { get; set; }
     }
 }
