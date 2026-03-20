using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios
{
    public class ReporteCuadroEstadisticoService
    {
        private const int FirstHeaderRow = 12;
        private const int DetailRowsPerPage = 26;
        private const int TableColumnCount = 21;
        private static readonly XLColor MatriculaHeaderFill = XLColor.FromHtml("#D9E2F3");
        private static readonly XLColor RetiroHeaderFill = XLColor.FromHtml("#FCE4D6");
        private static readonly XLColor TotalFill = XLColor.FromHtml("#E2F0D9");
        private static readonly XLColor SummaryFill = XLColor.FromHtml("#F4B183");
        private static readonly XLBorderStyleValues MediumBorder = XLBorderStyleValues.Medium;
        private static readonly XLBorderStyleValues ThinBorder = XLBorderStyleValues.Thin;

        private readonly Contexto _context;
        private readonly ILogger<ReporteCuadroEstadisticoService> _logger;

        public ReporteCuadroEstadisticoService(Contexto context, ILogger<ReporteCuadroEstadisticoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<byte[]> GenerarExcelAsync(int cicloId, int? carreraId)
        {
            var ciclo = await _context.Ciclos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cicloId);
            if (ciclo == null) throw new InvalidOperationException("No se encontro el ciclo seleccionado.");

            var reporte = await ConstruirReporteAsync(ciclo, NormalizarCarreraId(carreraId));
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Hoja1");
            ConfigurarHoja(worksheet);
            DibujarReporte(worksheet, reporte, ciclo);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task<ReporteData> ConstruirReporteAsync(Ciclo cicloActual, int? carreraId)
        {
            var inscripciones = await _context.Inscripciones
                .AsNoTracking()
                .Include(i => i.Alumno).ThenInclude(a => a!.Carrera)
                .Where(i => i.CicloId == cicloActual.Id && i.Activa)
                .Where(i => !carreraId.HasValue || i.Alumno!.CarreraId == carreraId.Value)
                .ToListAsync();

            var inscripcionesUnicas = inscripciones.Where(i => i.Alumno != null).GroupBy(i => i.AlumnoId).Select(g => g.First()).ToList();

            var deserciones = await _context.DesercionesAlumno
                .AsNoTracking()
                .Include(d => d.Alumno).ThenInclude(a => a!.Carrera)
                .Where(d => d.CicloId == cicloActual.Id)
                .Where(d => !carreraId.HasValue || d.Alumno!.CarreraId == carreraId.Value)
                .ToListAsync();

            var alumnoIds = inscripcionesUnicas.Select(i => i.AlumnoId).Concat(deserciones.Select(d => d.AlumnoId)).Distinct().ToList();
            var alumnosConInscripcionActual = inscripcionesUnicas.Select(i => i.AlumnoId).ToHashSet();
            var alumnosConInscripcionAnterior = await ObtenerAlumnoIdsConInscripcionAnteriorAsync(alumnoIds, cicloActual);
            var alumnosConGruposActuales = await ObtenerAlumnoIdsConGruposActualesAsync(cicloActual.Id, alumnoIds);
            var ubicaciones = await ObtenerUbicacionesFilaAsync(cicloActual, alumnoIds);

            var carreras = new Dictionary<int, CarreraData>();
            foreach (var inscripcion in inscripcionesUnicas)
            {
                var alumno = inscripcion.Alumno;
                if (alumno?.CarreraId is null || alumno.Carrera == null) continue;
                var categoria = ResolverCategoria(alumno, alumnosConInscripcionActual.Contains(alumno.AlumnoId) || alumnosConGruposActuales.Contains(alumno.AlumnoId), alumnosConInscripcionAnterior.Contains(alumno.AlumnoId));
                var ubicacion = ObtenerUbicacionOAsignarTemporal(ubicaciones, alumno.AlumnoId, cicloActual.Id, false);
                var carrera = ObtenerOCrearCarrera(carreras, alumno.CarreraId.Value, alumno.Carrera.NombreCarrera ?? "SIN CARRERA");
                var fila = ObtenerOCrearFila(carrera, ubicacion);
                IncrementarConteo(fila.Matricula, categoria, alumno.Genero);
            }

            foreach (var desercion in deserciones)
            {
                var alumno = desercion.Alumno;
                if (alumno?.CarreraId is null || alumno.Carrera == null) continue;
                var categoria = ResolverCategoria(alumno, alumnosConInscripcionActual.Contains(alumno.AlumnoId) || alumnosConGruposActuales.Contains(alumno.AlumnoId), alumnosConInscripcionAnterior.Contains(alumno.AlumnoId));
                var ubicacion = ObtenerUbicacionOAsignarTemporal(ubicaciones, alumno.AlumnoId, cicloActual.Id, true);
                var carrera = ObtenerOCrearCarrera(carreras, alumno.CarreraId.Value, alumno.Carrera.NombreCarrera ?? "SIN CARRERA");
                var fila = ObtenerOCrearFila(carrera, ubicacion);
                IncrementarConteo(fila.Retirados, categoria, alumno.Genero);
            }

            return new ReporteData
            {
                Carreras = carreras.Values.Where(c => c.Filas.Count > 0).OrderBy(c => c.CarreraId).Select((c, index) => ConvertirCarrera(c, index + 1)).ToList()
            };
        }

        private async Task<HashSet<int>> ObtenerAlumnoIdsConInscripcionAnteriorAsync(IReadOnlyCollection<int> alumnoIds, Ciclo cicloActual)
        {
            if (alumnoIds.Count == 0) return new HashSet<int>();
            var ids = await _context.Inscripciones.AsNoTracking()
                .Where(i => alumnoIds.Contains(i.AlumnoId))
                .Where(i => i.Ciclo != null && (i.Ciclo.anio < cicloActual.anio || (i.Ciclo.anio == cicloActual.anio && i.Ciclo.NCiclo < cicloActual.NCiclo)))
                .Select(i => i.AlumnoId).Distinct().ToListAsync();
            return ids.ToHashSet();
        }

        private async Task<HashSet<int>> ObtenerAlumnoIdsConGruposActualesAsync(int cicloId, IReadOnlyCollection<int> alumnoIds)
        {
            if (alumnoIds.Count == 0) return new HashSet<int>();
            var ids = await _context.MateriasInscritas.AsNoTracking()
                .Where(mi => alumnoIds.Contains(mi.AlumnoId) && mi.MateriasGrupo!.Grupo!.CicloId == cicloId)
                .Select(mi => mi.AlumnoId).Distinct().ToListAsync();
            return ids.ToHashSet();
        }

        private async Task<Dictionary<int, UbicacionFila>> ObtenerUbicacionesFilaAsync(Ciclo cicloActual, IReadOnlyCollection<int> alumnoIds)
        {
            var ubicaciones = new Dictionary<int, UbicacionFila>();
            if (alumnoIds.Count == 0) return ubicaciones;

            var materias = await _context.MateriasInscritas.AsNoTracking()
                .Where(mi => alumnoIds.Contains(mi.AlumnoId))
                .Select(mi => new UbicacionMateriaData
                {
                    AlumnoId = mi.AlumnoId,
                    GrupoCicloId = mi.MateriasGrupo!.Grupo!.CicloId,
                    EsEspecializacion = mi.MateriasGrupo!.Grupo!.EsEspecializacion,
                    CicloMateria = mi.MateriasGrupo!.Materia!.Ciclo,
                    Anio = mi.MateriasGrupo!.Grupo!.Ciclo!.anio,
                    NumeroCiclo = mi.MateriasGrupo!.Grupo!.Ciclo!.NCiclo
                }).ToListAsync();

            foreach (var grupoAlumno in materias.GroupBy(m => m.AlumnoId))
            {
                var actuales = grupoAlumno.Where(m => m.GrupoCicloId == cicloActual.Id).ToList();
                var ubicacionActual = ResolverUbicacionDesdeMaterias(actuales);
                if (ubicacionActual != null)
                {
                    ubicaciones[grupoAlumno.Key] = ubicacionActual;
                    continue;
                }

                var periodoHistorico = grupoAlumno.Select(m => new { m.Anio, m.NumeroCiclo }).Distinct().OrderByDescending(m => m.Anio).ThenByDescending(m => m.NumeroCiclo).FirstOrDefault();
                if (periodoHistorico == null) continue;

                var historicas = grupoAlumno.Where(m => m.Anio == periodoHistorico.Anio && m.NumeroCiclo == periodoHistorico.NumeroCiclo).ToList();
                var ubicacionHistorica = ResolverUbicacionDesdeMaterias(historicas);
                if (ubicacionHistorica == null) continue;

                _logger.LogWarning("Alumno {AlumnoId} sin grupos del ciclo {CicloId}; se uso la ultima ubicacion historica disponible para el cuadro estadistico.", grupoAlumno.Key, cicloActual.Id);
                ubicaciones[grupoAlumno.Key] = ubicacionHistorica;
            }

            return ubicaciones;
        }

        private static int? NormalizarCarreraId(int? carreraId) => carreraId.HasValue && carreraId.Value > 0 ? carreraId.Value : null;

        private UbicacionFila ObtenerUbicacionOAsignarTemporal(IReadOnlyDictionary<int, UbicacionFila> ubicaciones, int alumnoId, int cicloId, bool esDesercion)
        {
            if (ubicaciones.TryGetValue(alumnoId, out var ubicacion)) return ubicacion;

            _logger.LogWarning("{TipoRegistro} del alumno {AlumnoId} sin grupos asociados en el ciclo {CicloId}; se asigna temporalmente al ciclo I para no excluirlo del cuadro. Sustituir esta regla cuando exista una fuente confiable para deducir el ciclo academico real.", esDesercion ? "Desercion" : "Inscripcion", alumnoId, cicloId);

            // Regla temporal: si el alumno aparece en el reporte de inscripciones del ciclo actual,
            // pero aun no tiene MateriasInscritas que permitan ubicarlo con precision, lo dejamos
            // visible en el cuadro asignandolo al ciclo I. Esta regla debe reemplazarse cuando
            // exista una fuente academica mas confiable para deducir el ciclo real.
            return new UbicacionFila { EsPreespecializacion = false, CicloNumero = 1 };
        }

        private static UbicacionFila? ResolverUbicacionDesdeMaterias(IReadOnlyCollection<UbicacionMateriaData> materias)
        {
            if (materias.Count == 0) return null;
            if (materias.Any(m => m.EsEspecializacion)) return new UbicacionFila { EsPreespecializacion = true, CicloNumero = null };
            var cicloMateria = materias.Where(m => !m.EsEspecializacion).Select(m => (int?)m.CicloMateria).Max();
            return cicloMateria.HasValue ? new UbicacionFila { EsPreespecializacion = false, CicloNumero = cicloMateria.Value } : null;
        }

        private static CategoriaIngreso ResolverCategoria(Entidades.Alumnos.Alumno alumno, bool tieneInscripcionActual, bool tieneInscripcionAnterior)
        {
            if (alumno.IngresoPorEquivalencias) return CategoriaIngreso.Equivalencias;
            if (tieneInscripcionAnterior) return CategoriaIngreso.AntiguoIngreso;
            if (tieneInscripcionActual) return CategoriaIngreso.NuevoIngreso;

            // Regla temporal: si por inconsistencia de datos no logramos cotejar el historial del
            // alumno, lo dejamos como antiguo para no excluirlo del cuadro estadistico. Sustituir
            // esta regla cuando exista una fuente unica y confiable para clasificar antiguedad.
            return CategoriaIngreso.AntiguoIngreso;
        }

        private static CarreraData ObtenerOCrearCarrera(IDictionary<int, CarreraData> carreras, int carreraId, string nombreCarrera)
        {
            if (!carreras.TryGetValue(carreraId, out var carrera))
            {
                carrera = new CarreraData { CarreraId = carreraId, NombreCarrera = nombreCarrera, Filas = new SortedDictionary<RowKey, FilaData>(RowKeyComparer.Instance) };
                carreras[carreraId] = carrera;
            }
            return carrera;
        }

        private static FilaData ObtenerOCrearFila(CarreraData carrera, UbicacionFila ubicacion)
        {
            var rowKey = new RowKey(ubicacion.EsPreespecializacion, ubicacion.CicloNumero ?? 0);
            if (!carrera.Filas.TryGetValue(rowKey, out var fila))
            {
                fila = new FilaData { EsPreespecializacion = ubicacion.EsPreespecializacion, CicloNumero = ubicacion.CicloNumero, Matricula = new ConteosSeccion(), Retirados = new ConteosSeccion() };
                carrera.Filas[rowKey] = fila;
            }
            return fila;
        }

        private static void IncrementarConteo(ConteosSeccion conteos, CategoriaIngreso categoria, int genero)
        {
            var esHombre = genero == 0;
            var esMujer = genero == 1;
            if (!esHombre && !esMujer) return;
            switch (categoria)
            {
                case CategoriaIngreso.NuevoIngreso: if (esHombre) conteos.NuevoIngresoH++; else conteos.NuevoIngresoM++; break;
                case CategoriaIngreso.AntiguoIngreso: if (esHombre) conteos.AntiguoIngresoH++; else conteos.AntiguoIngresoM++; break;
                case CategoriaIngreso.Equivalencias: if (esHombre) conteos.EquivalenciasH++; else conteos.EquivalenciasM++; break;
            }
        }

        private static CarreraData ConvertirCarrera(CarreraData carrera, int orden)
        {
            carrera.Orden = orden;
            carrera.Filas = new SortedDictionary<RowKey, FilaData>(carrera.Filas, RowKeyComparer.Instance);
            return carrera;
        }

        private void DibujarReporte(IXLWorksheet worksheet, ReporteData reporte, Ciclo ciclo)
        {
            var paginas = PaginarCarreras(reporte.Carreras);
            var totalPaginas = paginas.Count == 0 ? 1 : paginas.Count;
            var currentHeaderRow = FirstHeaderRow;
            var filasTotalesCarrera = new List<int>();
            if (paginas.Count == 0) paginas.Add(new List<CarreraData>());

            for (var pageIndex = 0; pageIndex < paginas.Count; pageIndex++)
            {
                var esUltimaPagina = pageIndex == paginas.Count - 1;
                DibujarEncabezadoDocumento(worksheet, currentHeaderRow, ciclo);
                DibujarEncabezadoPagina(worksheet, currentHeaderRow);
                var currentRow = currentHeaderRow + 3;

                foreach (var carrera in paginas[pageIndex])
                {
                    var filaInicioCarrera = currentRow;
                    var primeraFila = true;
                    foreach (var fila in carrera.Filas.Values)
                    {
                        DibujarFilaDetalle(worksheet, currentRow, carrera, fila, primeraFila);
                        primeraFila = false;
                        currentRow++;
                    }
                    DibujarFilaTotales(worksheet, currentRow, filaInicioCarrera, currentRow - 1);
                    filasTotalesCarrera.Add(currentRow);
                    currentRow++;
                }

                if (!esUltimaPagina)
                {
                    var footerRow = currentHeaderRow + 29;
                    DibujarPiePagina(worksheet, footerRow, pageIndex + 1, totalPaginas);
                    worksheet.PageSetup.AddHorizontalPageBreak(footerRow);
                    currentHeaderRow = footerRow + 2;
                    continue;
                }

                var summaryStartRow = currentRow + 4;
                DibujarResumen(worksheet, summaryStartRow, filasTotalesCarrera);
                var finalFooterRow = summaryStartRow + 24;
                DibujarPiePagina(worksheet, finalFooterRow, pageIndex + 1, totalPaginas);
                ConfigurarImpresion(worksheet, finalFooterRow);
            }
        }

        private static void DibujarEncabezadoDocumento(IXLWorksheet worksheet, int headerRow, Ciclo ciclo)
        {
            var tituloInicio = headerRow - 10;
            var cicloTexto = $"{ciclo.NCiclo:00}-{ciclo.anio}";
            var mesTexto = ObtenerMesEncabezado(ciclo);

            worksheet.Row(tituloInicio).Height = 25;
            worksheet.Row(tituloInicio + 2).Height = 24;
            worksheet.Row(tituloInicio + 4).Height = 24;
            worksheet.Row(tituloInicio + 6).Height = 26;

            var lineas = new[]
            {
                "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO",
                "REGISTRO ACADEMICO",
                $"CICLO ACADEMICO {cicloTexto}",
                $"ESTADISTICA MENSUAL CORRESPONDIENTE AL MES DE {mesTexto} DE {ciclo.anio}"
            };

            var filas = new[] { tituloInicio, tituloInicio + 2, tituloInicio + 4, tituloInicio + 6 };
            var tamanos = new[] { 18d, 17d, 16d, 16d };

            for (var i = 0; i < lineas.Length; i++)
            {
                var range = worksheet.Range(filas[i], 1, filas[i], TableColumnCount);
                range.Merge();
                range.Value = lineas[i];
                range.Style.Font.Bold = true;
                range.Style.Font.FontSize = tamanos[i];
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
        }

        private static List<List<CarreraData>> PaginarCarreras(IReadOnlyList<CarreraData> carreras)
        {
            var paginas = new List<List<CarreraData>>();
            var paginaActual = new List<CarreraData>();
            var filasActuales = 0;
            foreach (var carrera in carreras)
            {
                var filasCarrera = carrera.Filas.Count + 1;
                if (paginaActual.Count > 0 && filasActuales + filasCarrera > DetailRowsPerPage)
                {
                    paginas.Add(paginaActual);
                    paginaActual = new List<CarreraData>();
                    filasActuales = 0;
                }
                paginaActual.Add(carrera);
                filasActuales += filasCarrera;
            }
            if (paginaActual.Count > 0) paginas.Add(paginaActual);
            return paginas;
        }

        private static void ConfigurarHoja(IXLWorksheet worksheet)
        {
            worksheet.Style.Font.FontName = "Calibri";
            worksheet.Style.Font.FontSize = 11;
            var widths = new Dictionary<string, double> { ["A"] = 6.86, ["B"] = 41.57, ["C"] = 7.14, ["D"] = 7.86, ["E"] = 13.00, ["F"] = 13.00, ["G"] = 8.14, ["H"] = 7.43, ["I"] = 7.86, ["J"] = 7.00, ["K"] = 7.71, ["L"] = 1.71, ["M"] = 7.43, ["N"] = 7.86, ["O"] = 7.57, ["P"] = 8.71, ["Q"] = 7.00, ["R"] = 7.43, ["S"] = 7.57, ["T"] = 7.71, ["U"] = 15.00 };
            foreach (var width in widths) worksheet.Column(width.Key).Width = width.Value;
            worksheet.SheetView.ZoomScale = 106;
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            worksheet.PageSetup.Scale = 64;
            worksheet.PageSetup.Margins.Left = 0.71;
            worksheet.PageSetup.Margins.Right = 0.2362204724;
            worksheet.PageSetup.Margins.Top = 0.32;
            worksheet.PageSetup.Margins.Bottom = 0.33;
        }

        private static void ConfigurarImpresion(IXLWorksheet worksheet, int lastRow)
        {
            worksheet.PageSetup.PrintAreas.Clear();
            worksheet.PageSetup.PrintAreas.Add(1, 1, Math.Max(lastRow, 1), TableColumnCount);
        }

        private static void DibujarEncabezadoPagina(IXLWorksheet worksheet, int headerRow)
        {
            worksheet.Row(headerRow - 1).Height = 15.75;
            worksheet.Row(headerRow).Height = 23.25;
            worksheet.Row(headerRow + 1).Height = 33.75;
            worksheet.Row(headerRow + 2).Height = 15.75;
            worksheet.Cell(headerRow, 4).Value = "                                                MATRICULA";
            worksheet.Cell(headerRow, 13).Value = "                                                MATRICULA";
            worksheet.Cell(headerRow, 14).Value = "                                   RETIRADOS";
            worksheet.Range(headerRow, 4, headerRow, 20).Style.Font.Bold = true;
            worksheet.Cell(headerRow + 1, 1).Value = "NO. DE";
            worksheet.Cell(headerRow + 1, 2).Value = "CARRERAS";
            worksheet.Cell(headerRow + 1, 3).Value = "CICLO";
            worksheet.Cell(headerRow + 1, 21).Value = "TOTAL INSCRITOS";
            worksheet.Range(headerRow + 1, 4, headerRow + 1, 5).Merge().Value = "NUEVO INGRESO";
            worksheet.Range(headerRow + 1, 6, headerRow + 1, 7).Merge().Value = "ANTIGUO INGRESO";
            worksheet.Range(headerRow + 1, 8, headerRow + 1, 9).Merge().Value = "EQUIVALENCIAS";
            worksheet.Range(headerRow + 1, 10, headerRow + 1, 11).Merge().Value = "TOTAL ";
            worksheet.Range(headerRow + 1, 13, headerRow + 1, 14).Merge().Value = "NUEVO INGRESO";
            worksheet.Range(headerRow + 1, 15, headerRow + 1, 16).Merge().Value = "ANTIGUO INGRESO";
            worksheet.Range(headerRow + 1, 17, headerRow + 1, 18).Merge().Value = "EQUIVALENCIAS";
            worksheet.Range(headerRow + 1, 19, headerRow + 1, 20).Merge().Value = "TOTAL ";
            worksheet.Cell(headerRow + 2, 1).Value = "ORDEN";
            foreach (var col in new[] { 4, 6, 8, 10, 13, 15, 17, 19 }) worksheet.Cell(headerRow + 2, col).Value = "H";
            foreach (var col in new[] { 5, 7, 9, 11, 14, 16, 18, 20 }) worksheet.Cell(headerRow + 2, col).Value = "M";
            worksheet.Cell(headerRow + 2, 21).Value = "POR CARRERA";
            var titleRange = worksheet.Range(headerRow + 1, 1, headerRow + 2, 21);
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Range(headerRow + 1, 4, headerRow + 1, 11).Style.Fill.BackgroundColor = MatriculaHeaderFill;
            worksheet.Range(headerRow + 1, 13, headerRow + 1, 20).Style.Fill.BackgroundColor = RetiroHeaderFill;
            AplicarBordesEncabezado(worksheet, headerRow + 1, headerRow + 2);
        }

        private static void AplicarBordesEncabezado(IXLWorksheet worksheet, int topRow, int bottomRow)
        {
            var leftHeader = worksheet.Range(topRow, 1, bottomRow, 3);
            leftHeader.Style.Border.LeftBorder = MediumBorder;
            leftHeader.Style.Border.RightBorder = MediumBorder;
            leftHeader.Style.Border.BottomBorder = MediumBorder;
            var matriculaHeader = worksheet.Range(topRow, 4, bottomRow, 11);
            matriculaHeader.Style.Border.TopBorder = MediumBorder;
            matriculaHeader.Style.Border.BottomBorder = MediumBorder;
            matriculaHeader.Style.Border.LeftBorder = MediumBorder;
            matriculaHeader.Style.Border.RightBorder = MediumBorder;
            var retirosHeader = worksheet.Range(topRow, 13, bottomRow, 20);
            retirosHeader.Style.Border.TopBorder = MediumBorder;
            retirosHeader.Style.Border.BottomBorder = MediumBorder;
            retirosHeader.Style.Border.LeftBorder = MediumBorder;
            retirosHeader.Style.Border.RightBorder = MediumBorder;
            var totalHeader = worksheet.Range(topRow, 21, bottomRow, 21);
            totalHeader.Style.Border.LeftBorder = MediumBorder;
            totalHeader.Style.Border.RightBorder = MediumBorder;
            totalHeader.Style.Border.BottomBorder = MediumBorder;
        }

        private static void DibujarFilaDetalle(IXLWorksheet worksheet, int rowNumber, CarreraData carrera, FilaData fila, bool esPrimeraFilaCarrera)
        {
            worksheet.Row(rowNumber).Height = 24.95;
            if (esPrimeraFilaCarrera) worksheet.Cell(rowNumber, 1).Value = carrera.Orden;

            if (fila.EsPreespecializacion)
            {
                if (esPrimeraFilaCarrera)
                {
                    worksheet.Cell(rowNumber, 2).Value = carrera.NombreCarrera.ToUpperInvariant();
                    worksheet.Cell(rowNumber, 3).Value = "PREESPECIALIZACION";
                    worksheet.Cell(rowNumber, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    worksheet.Cell(rowNumber, 2).Value = "PREESPECIALIZACION";
                    worksheet.Cell(rowNumber, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }
            else
            {
                if (esPrimeraFilaCarrera) worksheet.Cell(rowNumber, 2).Value = carrera.NombreCarrera.ToUpperInvariant();
                worksheet.Cell(rowNumber, 3).Value = ConvertirARomano(fila.CicloNumero);
            }

            worksheet.Cell(rowNumber, 4).Value = fila.Matricula.NuevoIngresoH;
            worksheet.Cell(rowNumber, 5).Value = fila.Matricula.NuevoIngresoM;
            worksheet.Cell(rowNumber, 6).Value = fila.Matricula.AntiguoIngresoH;
            worksheet.Cell(rowNumber, 7).Value = fila.Matricula.AntiguoIngresoM;
            worksheet.Cell(rowNumber, 8).Value = fila.Matricula.EquivalenciasH;
            worksheet.Cell(rowNumber, 9).Value = fila.Matricula.EquivalenciasM;
            worksheet.Cell(rowNumber, 10).FormulaA1 = $"=D{rowNumber}+F{rowNumber}+H{rowNumber}";
            worksheet.Cell(rowNumber, 11).FormulaA1 = $"=E{rowNumber}+G{rowNumber}+I{rowNumber}";
            worksheet.Cell(rowNumber, 13).Value = fila.Retirados.NuevoIngresoH;
            worksheet.Cell(rowNumber, 14).Value = fila.Retirados.NuevoIngresoM;
            worksheet.Cell(rowNumber, 15).Value = fila.Retirados.AntiguoIngresoH;
            worksheet.Cell(rowNumber, 16).Value = fila.Retirados.AntiguoIngresoM;
            worksheet.Cell(rowNumber, 17).Value = fila.Retirados.EquivalenciasH;
            worksheet.Cell(rowNumber, 18).Value = fila.Retirados.EquivalenciasM;
            worksheet.Cell(rowNumber, 19).FormulaA1 = $"=M{rowNumber}+O{rowNumber}+Q{rowNumber}";
            worksheet.Cell(rowNumber, 20).FormulaA1 = $"=N{rowNumber}+P{rowNumber}+R{rowNumber}";

            var carreraCell = worksheet.Cell(rowNumber, 2);
            carreraCell.Style.Font.Bold = esPrimeraFilaCarrera;
            carreraCell.Style.Font.FontSize = 14;
            carreraCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            carreraCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            carreraCell.Style.Alignment.WrapText = true;
            carreraCell.Style.Alignment.ShrinkToFit = true;
            worksheet.Cell(rowNumber, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(rowNumber, 3).Style.Font.FontSize = 14;
            var dataRange = worksheet.Range(rowNumber, 4, rowNumber, 20);
            dataRange.Style.Font.FontSize = 14;
            dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            AplicarBordesFila(worksheet, rowNumber);
        }

        private static void DibujarFilaTotales(IXLWorksheet worksheet, int totalRow, int detailStartRow, int detailEndRow)
        {
            worksheet.Row(totalRow).Height = 24.95;
            worksheet.Cell(totalRow, 2).Value = "TOTALES";
            worksheet.Cell(totalRow, 2).Style.Font.Bold = true;
            worksheet.Cell(totalRow, 2).Style.Font.FontSize = 14;
            worksheet.Cell(totalRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            foreach (var col in new[] { "D", "E", "F", "G", "H", "I", "J", "K", "M", "N", "O", "P", "Q", "R", "S", "T" })
            {
                worksheet.Cell($"{col}{totalRow}").FormulaA1 = FormulaSumatoria(col, detailStartRow, detailEndRow);
            }
            worksheet.Cell(totalRow, 21).FormulaA1 = $"=J{totalRow}+K{totalRow}";
            var totalRange = worksheet.Range(totalRow, 2, totalRow, 21);
            totalRange.Style.Fill.BackgroundColor = TotalFill;
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Font.FontSize = 14;
            totalRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Range(totalRow, 4, totalRow, 21).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            AplicarBordesFila(worksheet, totalRow);
            worksheet.Cell(totalRow, 21).Style.Border.RightBorder = MediumBorder;
        }

        private static void DibujarResumen(IXLWorksheet worksheet, int summaryStartRow, IReadOnlyCollection<int> filasTotalesCarrera)
        {
            worksheet.Row(summaryStartRow).Height = 19.50;
            worksheet.Row(summaryStartRow + 1).Height = 31.50;
            worksheet.Row(summaryStartRow + 2).Height = 26.25;
            worksheet.Row(summaryStartRow + 3).Height = 31.50;
            worksheet.Cell(summaryStartRow, 2).Value = "RESUMEN GENERAL";
            worksheet.Cell(summaryStartRow, 2).Style.Font.Bold = true;
            worksheet.Cell(summaryStartRow, 2).Style.Font.FontSize = 12;
            worksheet.Cell(summaryStartRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(summaryStartRow + 1, 2).Value = "TOTAL ALUMNOS INSCRITOS =";
            worksheet.Cell(summaryStartRow + 2, 2).Value = "TOTAL HOMBRES =";
            worksheet.Cell(summaryStartRow + 3, 2).Value = "TOTAL MUJERES=";

            var formulaTotales = filasTotalesCarrera.Count == 0 ? "0" : string.Join("+", filasTotalesCarrera.Select(f => $"U{f}"));
            var formulaHombres = filasTotalesCarrera.Count == 0 ? "0" : string.Join("+", filasTotalesCarrera.Select(f => $"J{f}"));
            var formulaMujeres = filasTotalesCarrera.Count == 0 ? "0" : string.Join("+", filasTotalesCarrera.Select(f => $"K{f}"));
            worksheet.Cell(summaryStartRow + 1, 3).FormulaA1 = $"={formulaTotales}";
            worksheet.Cell(summaryStartRow + 2, 3).FormulaA1 = $"={formulaHombres}";
            worksheet.Cell(summaryStartRow + 3, 3).FormulaA1 = $"={formulaMujeres}";

            var resumenLabelRange = worksheet.Range(summaryStartRow + 1, 2, summaryStartRow + 3, 2);
            resumenLabelRange.Style.Fill.BackgroundColor = SummaryFill;
            resumenLabelRange.Style.Font.Bold = true;
            resumenLabelRange.Style.Font.FontSize = 12;
            resumenLabelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            resumenLabelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            var resumenValueRange = worksheet.Range(summaryStartRow + 1, 3, summaryStartRow + 3, 3);
            resumenValueRange.Style.Fill.BackgroundColor = SummaryFill;
            resumenValueRange.Style.Font.Bold = true;
            resumenValueRange.Style.Font.FontSize = 12;
            resumenValueRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            resumenValueRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Range(summaryStartRow + 1, 2, summaryStartRow + 3, 3).Style.Border.OutsideBorder = MediumBorder;
            worksheet.Range(summaryStartRow + 1, 2, summaryStartRow + 3, 3).Style.Border.InsideBorder = MediumBorder;
            worksheet.Cell(summaryStartRow + 1, 8).Value = "             LIC. JOSE AUGUSTO HERNANDEZ GONZALEZ";
            worksheet.Cell(summaryStartRow + 2, 8).Value = "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y";
            worksheet.Cell(summaryStartRow + 3, 8).Value = "ADMINISTRADOR EN FUNCIONES DE REGISTRO ACADEMICO AD HONOREM";
            worksheet.Cell(summaryStartRow + 1, 8).Style.Border.TopBorder = ThinBorder;
            worksheet.Range(summaryStartRow + 1, 8, summaryStartRow + 3, 21).Style.Font.Bold = true;
            worksheet.Range(summaryStartRow + 1, 8, summaryStartRow + 3, 21).Style.Font.FontSize = 12;
        }

        private static void DibujarPiePagina(IXLWorksheet worksheet, int footerRow, int paginaActual, int totalPaginas)
        {
            worksheet.Cell(footerRow, 8).Value = $"Pagina {paginaActual}/{totalPaginas}";
            worksheet.Cell(footerRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(footerRow, 8).Style.Font.FontSize = 11;
        }

        private static void AplicarBordesFila(IXLWorksheet worksheet, int rowNumber)
        {
            var leftRange = worksheet.Range(rowNumber, 1, rowNumber, 3);
            leftRange.Style.Border.LeftBorder = MediumBorder;
            leftRange.Style.Border.BottomBorder = ThinBorder;
            var matriculaRange = worksheet.Range(rowNumber, 4, rowNumber, 11);
            matriculaRange.Style.Border.LeftBorder = MediumBorder;
            matriculaRange.Style.Border.RightBorder = MediumBorder;
            matriculaRange.Style.Border.TopBorder = MediumBorder;
            matriculaRange.Style.Border.BottomBorder = ThinBorder;
            var retiroRange = worksheet.Range(rowNumber, 13, rowNumber, 20);
            retiroRange.Style.Border.LeftBorder = MediumBorder;
            retiroRange.Style.Border.RightBorder = MediumBorder;
            retiroRange.Style.Border.TopBorder = MediumBorder;
            retiroRange.Style.Border.BottomBorder = ThinBorder;
            var totalRange = worksheet.Range(rowNumber, 21, rowNumber, 21);
            totalRange.Style.Border.LeftBorder = MediumBorder;
            totalRange.Style.Border.RightBorder = MediumBorder;
            totalRange.Style.Border.BottomBorder = ThinBorder;
            totalRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            totalRange.Style.Font.FontSize = 14;
            totalRange.Style.Font.Bold = true;
        }

        private static string FormulaSumatoria(string column, int startRow, int endRow) => $"=SUM({column}{startRow}:{column}{endRow})";

        private static string ObtenerMesEncabezado(Ciclo ciclo) => ciclo.NCiclo switch
        {
            1 => "ENERO",
            2 => "JULIO",
            _ => "ENERO"
        };

        private static string ConvertirARomano(int? cicloNumero) => cicloNumero switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
            6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X",
            _ => string.Empty
        };

        private enum CategoriaIngreso { NuevoIngreso, AntiguoIngreso, Equivalencias }

        private readonly record struct RowKey(bool EsPreespecializacion, int CicloNumero);

        private sealed class RowKeyComparer : IComparer<RowKey>
        {
            public static readonly RowKeyComparer Instance = new();
            public int Compare(RowKey x, RowKey y)
            {
                if (x.EsPreespecializacion != y.EsPreespecializacion) return x.EsPreespecializacion ? 1 : -1;
                return x.CicloNumero.CompareTo(y.CicloNumero);
            }
        }

        private sealed class ReporteData { public List<CarreraData> Carreras { get; set; } = new(); }

        private sealed class CarreraData
        {
            public int CarreraId { get; set; }
            public int Orden { get; set; }
            public string NombreCarrera { get; set; } = string.Empty;
            public SortedDictionary<RowKey, FilaData> Filas { get; set; } = new(RowKeyComparer.Instance);
        }

        private sealed class FilaData
        {
            public bool EsPreespecializacion { get; set; }
            public int? CicloNumero { get; set; }
            public ConteosSeccion Matricula { get; set; } = new();
            public ConteosSeccion Retirados { get; set; } = new();
        }

        private sealed class ConteosSeccion
        {
            public int NuevoIngresoH { get; set; }
            public int NuevoIngresoM { get; set; }
            public int AntiguoIngresoH { get; set; }
            public int AntiguoIngresoM { get; set; }
            public int EquivalenciasH { get; set; }
            public int EquivalenciasM { get; set; }
        }

        private sealed class UbicacionFila
        {
            public bool EsPreespecializacion { get; set; }
            public int? CicloNumero { get; set; }
        }

        private sealed class UbicacionMateriaData
        {
            public int AlumnoId { get; set; }
            public int GrupoCicloId { get; set; }
            public bool EsEspecializacion { get; set; }
            public int CicloMateria { get; set; }
            public int Anio { get; set; }
            public int NumeroCiclo { get; set; }
        }
    }
}
