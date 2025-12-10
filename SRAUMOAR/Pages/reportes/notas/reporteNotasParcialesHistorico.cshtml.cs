using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Layout.Borders;
using iText.Kernel.Colors;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using System.Text.RegularExpressions;
using System.Web;

namespace SRAUMOAR.Pages.reportes.notas
{
    public class reporteNotasParcialesHistoricoModel : PageModel
    {
        private readonly Contexto _context;

        private PdfFont _fontNormal;
        private PdfFont _fontBold;

        public reporteNotasParcialesHistoricoModel(Contexto context)
        {
            _context = context;
        }

        /// <summary>
        /// Calcula el promedio ponderado de notas usando los porcentajes configurados en las actividades académicas
        /// </summary>
        private static decimal CalcularPromedioMateriaComun(ICollection<Notas> notas, IList<ActividadAcademica> actividadesAcademicas)
        {
            if (actividadesAcademicas == null || !actividadesAcademicas.Any())
                return 0;

            decimal sumaPonderada = 0;
            decimal totalPorcentaje = 0;

            foreach (var actividad in actividadesAcademicas)
            {
                if (actividad == null) continue;

                int porcentaje = actividad.Porcentaje;
                totalPorcentaje += porcentaje;

                var notaRegistrada = notas?.FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId);
                decimal valorNota = notaRegistrada?.Nota ?? 0;
                sumaPonderada += valorNota * porcentaje;
            }

            if (totalPorcentaje <= 0) return 0;

            return Math.Round(sumaPonderada / totalPorcentaje, 2);
        }

        /// <summary>
        /// Calcula la nota final aplicando las reglas de reposición
        /// </summary>
        private static decimal CalcularNotaFinal(MateriasInscritas materiaInscrita, ICollection<Notas> notasMateria, IList<ActividadAcademica> actividadesAcademicas)
        {
            // Calcular promedio base
            decimal promedio = CalcularPromedioMateriaComun(notasMateria, actividadesAcademicas);

            // Aplicar regla de reposición
            if (materiaInscrita.NotaRecuperacion.HasValue)
            {
                if (materiaInscrita.NotaRecuperacion.Value >= 7)
                {
                    // Si aprobó recuperación (>=7), la nota final es 7
                    return 7;
                }
                else
                {
                    // Si tiene nota de recuperación pero reprobó (<7), usar esa nota
                    return materiaInscrita.NotaRecuperacion.Value;
                }
            }

            // Si no tiene nota de recuperación, usar el promedio calculado
            return promedio;
        }

        private void InitializeFonts()
        {
            _fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            _fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
        }

        public IActionResult OnGet(int alumnoId, int cicloId)
        {
            InitializeFonts();

            var ciclo = _context.Ciclos.FirstOrDefault(c => c.Id == cicloId);
            if (ciclo == null)
            {
                return NotFound("El ciclo especificado no existe");
            }

            var alumno = _context.Alumno
                .Include(a => a.Carrera)
                    .ThenInclude(c => c!.Facultad)
                .FirstOrDefault(a => a.AlumnoId == alumnoId);

            if (alumno == null)
            {
                return NotFound();
            }

            var materiasInscritas = _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)!.ThenInclude(mg => mg!.Materia)
                .Include(mi => mi.Notas)!.ThenInclude(n => n!.ActividadAcademica)
                .Where(mi => mi.AlumnoId == alumnoId && mi.MateriasGrupo!.Grupo!.CicloId == cicloId)
                .ToList();

            // Obtener actividades académicas del ciclo histórico para calcular promedios correctamente
            var actividadesAcademicas = _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloId)
                .OrderBy(a => a.FechaInicio)
                .ToList();

            // Título del reporte histórico
            string tituloReporte = $"Informe de notas finales ciclo {ciclo.NCiclo} - {ciclo.anio}";

            // Determinar estado de solvencia del ciclo histórico
            var hoy = DateTime.Now.Date;
            var esBecado = _context.Becados.Any(b => b.AlumnoId == alumnoId && b.CicloId == cicloId && b.Estado);
            var arancelesObligatoriosVigentes = _context.Aranceles
                .Where(a => a.CicloId == cicloId && a.Obligatorio && a.Activo && a.FechaInicio <= hoy)
                .ToList();
            var arancelesPagadosIds = _context.DetallesCobrosArancel
                .Where(d => d.CobroArancel.AlumnoId == alumnoId && d.CobroArancel.CicloId == cicloId)
                .Select(d => d.ArancelId)
                .ToHashSet();
            bool tienePendientes = arancelesObligatoriosVigentes.Any(a => !arancelesPagadosIds.Contains(a.ArancelId));
            string estadoSolvencia = esBecado ? "Solvente" : (tienePendientes ? "No solvente" : "Solvente");

            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            {
                using var pdf = new PdfDocument(writer);
                var doc = new Document(pdf, iText.Kernel.Geom.PageSize.LETTER);
                doc.SetMargins(36, 36, 36, 36);

                // Encabezado con logo + textos centrados
                var headerTable = new Table(2).UseAllAvailableWidth();
                try
                {
                    var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                    var logo = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(logoPath)).SetWidth(60);
                    headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE));
                }
                catch
                {
                    headerTable.AddCell(new Cell().Add(new Paragraph(" ")).SetBorder(Border.NO_BORDER));
                }
                var textCell = new Cell().SetBorder(Border.NO_BORDER);
                textCell.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO").SetFont(_fontBold).SetFontSize(14).SetTextAlignment(TextAlignment.CENTER));
                textCell.Add(new Paragraph(tituloReporte.ToUpper()).SetFont(_fontBold).SetFontSize(12).SetTextAlignment(TextAlignment.CENTER));
                headerTable.AddCell(textCell);
                doc.Add(headerTable);
                
                // Línea separadora con estado a la derecha
                var separatorTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                var separatorCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(0);
                separatorCell.Add(new Paragraph(" ")
                    .SetBorderBottom(new SolidBorder(ColorConstants.LIGHT_GRAY, 1))
                    .SetMarginTop(4)
                    .SetMarginBottom(12));
                separatorTable.AddCell(separatorCell);
                
                // Estado a la derecha sobre la línea
                var estadoCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(0)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetVerticalAlignment(VerticalAlignment.BOTTOM);
                string estadoTexto = estadoSolvencia.ToUpper();
                estadoCell.Add(new Paragraph(estadoTexto)
                    .SetFont(_fontBold)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(0)
                    .SetMarginBottom(2));
                separatorTable.AddCell(estadoCell);
                doc.Add(separatorTable);

                // Datos del alumno
                string carnet = !string.IsNullOrWhiteSpace(alumno.Carnet) ? alumno.Carnet : ((alumno.Email ?? string.Empty).Split('@').FirstOrDefault() ?? string.Empty);
                var datos = new Table(new float[] { 70, 460 }).UseAllAvailableWidth();
                datos.AddCell(CeldaLabel("Nombre:"));
                datos.AddCell(CeldaValor($"{alumno.Apellidos} {alumno.Nombres}"));
                datos.AddCell(CeldaLabel("Carnet:"));
                datos.AddCell(CeldaValor(carnet));
                datos.AddCell(CeldaLabel("Facultad:"));
                datos.AddCell(CeldaValor(alumno.Carrera?.Facultad?.NombreFacultad ?? ""));
                datos.AddCell(CeldaLabel("Carrera:"));
                datos.AddCell(CeldaValor(alumno.Carrera?.NombreCarrera ?? ""));
                doc.Add(datos);

                doc.Add(new Paragraph(" "));

                // Tabla de notas histórico: COD, Materia, Nota Final, Nota Recuperación, Estado, Observación
                var tabla = new Table(new float[] { 50, 290, 70, 80, 80, 70 }).UseAllAvailableWidth();
                tabla.SetBorder(Border.NO_BORDER); // Tabla sin bordes generales
                
                EstiloEncabezado(tabla, "Código");
                EstiloEncabezado(tabla, "Nombre de la asignatura");
                EstiloEncabezado(tabla, "Nota Final");
                EstiloEncabezado(tabla, "Nota Rep.");
                EstiloEncabezado(tabla, "Resultado");
                EstiloEncabezado(tabla, "Observación");

                foreach (var mi in materiasInscritas.OrderBy(x => x.MateriasGrupo!.Materia!.CodigoMateria))
                {
                    var materia = mi.MateriasGrupo!.Materia!;
                    var notas = (mi.Notas ?? new List<Notas>())
                        .Where(n => n.ActividadAcademica != null)
                        .ToList();

                    // Calcular nota final usando porcentajes configurados y regla de reposición
                    decimal notaFinal = CalcularNotaFinal(mi, notas, actividadesAcademicas);
                    
                    // Calcular nota de recuperación según reglas
                    decimal? notaRecuperacion = null;
                    if (mi.NotaRecuperacion.HasValue)
                    {
                        if (mi.NotaRecuperacion.Value >= 7)
                        {
                            // Si aprobó recuperación (>=7), la nota final es 7
                            notaRecuperacion = 7;
                        }
                        else
                        {
                            // Si tiene nota de recuperación pero reprobó (<7), usar esa nota
                            notaRecuperacion = mi.NotaRecuperacion.Value;
                        }
                    }

                    // Determinar estado: APROBADO o REPROBADO
                    string estado = notaFinal >= 7 ? "APROBADO" : "REPROBADO";

                    // Observación (puede estar vacía o tener algún valor)
                    string observacion = ""; // Por ahora vacía, se puede agregar lógica adicional si es necesario

                    // Celdas de datos sin bordes ni fondo
                    tabla.AddCell(CeldaTextoSinBorde(materia.CodigoMateria ?? ""));
                    tabla.AddCell(CeldaTextoSinBorde(materia.NombreMateria ?? ""));
                    tabla.AddCell(CeldaNotaSinBorde(notaFinal));
                    tabla.AddCell(CeldaNotaSinBorde(notaRecuperacion));
                    tabla.AddCell(CeldaTextoSinBorde(estado));
                    tabla.AddCell(CeldaTextoSinBorde(observacion));
                }

                doc.Add(tabla);

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(" "));

                // Firma y Sello en la misma línea, cada una con su línea arriba, alineadas a la derecha
                var firmaTabla = new Table(new float[] { 120, 120 }).SetWidth(240).SetHorizontalAlignment(HorizontalAlignment.RIGHT);
                
                // Columna FIRMA: línea arriba y texto abajo
                var firmaColumna = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(0)
                    .SetTextAlignment(TextAlignment.CENTER);
                firmaColumna.Add(new Paragraph(" ").SetBorderBottom(new SolidBorder(1)).SetMargin(0).SetMarginBottom(3));
                firmaColumna.Add(new Paragraph("FIRMA").SetFont(_fontBold).SetMargin(0).SetMarginTop(0));
                
                // Columna SELLO: línea arriba y texto abajo
                var selloColumna = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(0)
                    .SetTextAlignment(TextAlignment.CENTER);
                selloColumna.Add(new Paragraph(" ").SetBorderBottom(new SolidBorder(1)).SetMargin(0).SetMarginBottom(3));
                selloColumna.Add(new Paragraph("SELLO").SetFont(_fontBold).SetMargin(0).SetMarginTop(0));
                
                // Agregar celdas: FIRMA y SELLO en la misma fila
                firmaTabla.AddCell(firmaColumna);
                firmaTabla.AddCell(selloColumna);
                
                doc.Add(firmaTabla);

                doc.Close();
            }

            // Crear nombre de archivo sin caracteres especiales para evitar errores en headers HTTP
            var apellidosLimpios = Regex.Replace(alumno.Apellidos ?? "", @"[^\w\s-]", "").Replace(" ", "_");
            var nombresLimpios = Regex.Replace(alumno.Nombres ?? "", @"[^\w\s-]", "").Replace(" ", "_");
            var fileName = $"NotasParciales_Historico_{ciclo.NCiclo}_{ciclo.anio}_{apellidosLimpios}_{nombresLimpios}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            
            // Codificar el nombre del archivo para headers HTTP usando RFC 5987
            var encodedFileName = System.Web.HttpUtility.UrlEncode(fileName);
            Response.Headers["Content-Disposition"] = $"inline; filename*=UTF-8''{encodedFileName}";
            return File(ms.ToArray(), "application/pdf");
        }

        private Cell CeldaLabel(string text)
        {
            return new Cell().Add(new Paragraph(text).SetFont(_fontBold)).SetBorder(Border.NO_BORDER);
        }
        private Cell CeldaSeparador()
        {
            return new Cell().Add(new Paragraph(":")).SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        }
        private Cell CeldaValor(string text)
        {
            return new Cell().Add(new Paragraph(text).SetFont(_fontNormal)).SetBorder(Border.NO_BORDER);
        }
        private void EstiloEncabezado(Table tabla, string texto)
        {
            tabla.AddCell(new Cell()
                .Add(new Paragraph(texto).SetFont(_fontBold).SetFontSize(10))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(new SolidBorder(1))
                .SetPadding(8)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE));
        }
        private Cell CeldaTexto(string text, bool alt = false)
        {
            var cell = new Cell().Add(new Paragraph(text).SetFont(_fontNormal)).SetBorder(new SolidBorder(1)).SetPadding(5);
            if (alt) cell.SetBackgroundColor(new DeviceRgb(248, 248, 248));
            return cell;
        }
        private Cell CeldaNota(decimal? valor, bool alt = false)
        {
            var texto = valor.HasValue ? valor.Value.ToString("0.00") : "0.00";
            var cell = new Cell().Add(new Paragraph(texto).SetTextAlignment(TextAlignment.CENTER).SetFont(_fontNormal)).SetBorder(new SolidBorder(1)).SetPadding(5);
            if (alt) cell.SetBackgroundColor(new DeviceRgb(248, 248, 248));
            return cell;
        }
        private Cell CeldaTextoSinBorde(string text)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(_fontNormal).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPadding(8)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE);
        }
        private Cell CeldaNotaSinBorde(decimal? valor)
        {
            var texto = valor.HasValue ? valor.Value.ToString("0.00") : "-";
            return new Cell()
                .Add(new Paragraph(texto).SetTextAlignment(TextAlignment.CENTER).SetFont(_fontNormal).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPadding(8)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE);
        }
    }
}

