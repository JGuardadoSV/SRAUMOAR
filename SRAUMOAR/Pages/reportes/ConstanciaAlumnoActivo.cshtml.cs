using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SRAUMOAR.Modelos;
using System.Globalization;
using System.IO;

namespace SRAUMOAR.Pages.reportes
{
    public class ConstanciaAlumnoActivoModel : PageModel
    {
        private readonly Contexto _context;

        public ConstanciaAlumnoActivoModel(Contexto context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int? alumnoId, int? cicloId, DateTime? fechaExpedicion = null)
        {
            try
            {
                if (!alumnoId.HasValue || alumnoId.Value <= 0)
                {
                    return BadRequest("Debe especificar un alumno.");
                }

                if (!cicloId.HasValue || cicloId.Value <= 0)
                {
                    return BadRequest("Debe especificar un ciclo.");
                }

                var alumno = await _context.Alumno
                    .AsNoTracking()
                    .Include(a => a.Carrera)
                        .ThenInclude(c => c!.Facultad)
                    .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId.Value);

                if (alumno == null)
                {
                    return NotFound("Alumno no encontrado.");
                }

                var ciclo = await _context.Ciclos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == cicloId.Value);

                if (ciclo == null)
                {
                    return NotFound("El ciclo especificado no existe.");
                }

                int materiasInscritas = await _context.MateriasInscritas
                    .AsNoTracking()
                    .Include(mi => mi.MateriasGrupo)!.ThenInclude(mg => mg!.Grupo)
                    .CountAsync(mi => mi.AlumnoId == alumnoId.Value && mi.MateriasGrupo!.Grupo!.CicloId == cicloId.Value);

                if (materiasInscritas <= 0)
                {
                    return BadRequest("No se encontraron materias inscritas para este alumno en el ciclo seleccionado.");
                }

                var configs = await CargarConfiguracionesAsync();

                string dir1 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea1", ""));
                string dir2 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea2", ""));
                string dir3 = CleanHeaderString(GetConfig(configs, "CertificacionNotas", "DireccionLinea3", ""));

                string tituloReporte = GetConfig(configs, "ConstanciaAlumnoActivo", "TituloReporte", "ALUMNO ACTIVO");
                string emisorCargo = GetConfig(configs, "ConstanciaAlumnoActivo", "EmisorCargo", "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico");
                string lugarInstitucion = GetConfig(configs, "ConstanciaAlumnoActivo", "LugarInstitucion", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango");
                string destinatario = GetConfig(configs, "ConstanciaAlumnoActivo", "Destinatario", "AFP CRECER");
                string lugarExpedicion = GetConfig(configs, "ConstanciaAlumnoActivo", "LugarExpedicion", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango");
                string firmaNombre = GetConfig(configs, "ConstanciaAlumnoActivo", "FirmaNombre", "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ");
                string firmaCargoLinea1 = GetConfig(configs, "ConstanciaAlumnoActivo", "FirmaCargoLinea1", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y");
                string firmaCargoLinea2 = GetConfig(configs, "ConstanciaAlumnoActivo", "FirmaCargoLinea2", "ADMINISTRADOR EN FUNCIONES AD HONOREM");
                string firmaCargoLinea3 = GetConfig(configs, "ConstanciaAlumnoActivo", "FirmaCargoLinea3", "DE REGISTRO ACADÉMICO");
                string elaboradoPor = GetConfig(configs, "ConstanciaAlumnoActivo", "ElaboradoPor", "ELABORADO POR: MTRC");
                string leyendaValidez = GetConfig(configs, "ConstanciaAlumnoActivo", "LeyendaValidez", "ESTA CONSTANCIA NO ES VÁLIDA, SIN FIRMAS Y SELLOS DE ESTA UNIVERSIDAD");

                string cuerpo = GetConfig(configs, "ConstanciaAlumnoActivo", "Cuerpo",
                    "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N°. {carnet}, es {alumnoalumna} {activoactiva} DE {cicloRomano} CICLO, de la {facultad}, en la Carrera de {carrera}, del Ciclo Académico {cicloAcademico}, el cual dio inicio el día {fechaInicioCiclo} y finalizará el día {fechaFinCiclo}.");

                string cierre = GetConfig(configs, "ConstanciaAlumnoActivo", "Cierre",
                    "Y para ser presentada a la {destinatario}, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}.");

                var datos = CrearDatosPlantilla(alumno, ciclo, fechaExpedicion?.Date ?? DateTime.Now.Date, emisorCargo, lugarInstitucion, destinatario, lugarExpedicion);
                cuerpo = ReemplazarPlaceholders(cuerpo, datos);
                cierre = ReemplazarPlaceholders(cierre, datos);

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(38);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                        page.Content().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                                if (System.IO.File.Exists(logoPath))
                                {
                                    row.ConstantItem(76).Image(logoPath);
                                }
                                else
                                {
                                    row.ConstantItem(76).Text("");
                                }

                                row.RelativeItem().AlignCenter().Column(textCol =>
                                {
                                    textCol.Item().AlignCenter().Text("Universidad Monseñor Oscar Arnulfo Romero")
                                        .FontSize(15);

                                    if (!string.IsNullOrWhiteSpace(dir1))
                                        textCol.Item().AlignCenter().Text(dir1).FontSize(9.5f);
                                    if (!string.IsNullOrWhiteSpace(dir2))
                                        textCol.Item().AlignCenter().Text(dir2).FontSize(9.5f);
                                    if (!string.IsNullOrWhiteSpace(dir3))
                                        textCol.Item().AlignCenter().Text(dir3).FontSize(9.5f);
                                });
                            });

                            col.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(Colors.Black);

                            col.Item().PaddingTop(10).AlignCenter().Text(tituloReporte)
                                .FontSize(13);

                            col.Item().PaddingTop(18).Text(t =>
                            {
                                t.Justify();
                                AgregarTextoConNegritas(t, cuerpo, datos);
                            });

                            col.Item().PaddingTop(52).Text(t =>
                            {
                                t.Justify();
                                AgregarTextoConNegritas(t, cierre, datos);
                            });

                            col.Item().PaddingTop(88).ShowEntire().Column(firma =>
                            {
                                firma.Item().AlignCenter().Text(firmaNombre.ToUpper()).Bold().FontSize(10);
                                if (!string.IsNullOrWhiteSpace(firmaCargoLinea1))
                                {
                                    firma.Item().PaddingTop(8).AlignCenter().Text(firmaCargoLinea1.ToUpper()).Bold().FontSize(10);
                                }
                                if (!string.IsNullOrWhiteSpace(firmaCargoLinea2))
                                {
                                    firma.Item().AlignCenter().Text(firmaCargoLinea2.ToUpper()).Bold().FontSize(10);
                                }
                                if (!string.IsNullOrWhiteSpace(firmaCargoLinea3))
                                {
                                    firma.Item().AlignCenter().Text(firmaCargoLinea3.ToUpper()).Bold().FontSize(10);
                                }
                            });

                            col.Item().PaddingTop(24).PaddingLeft(18).Column(footer =>
                            {
                                footer.Item().Text(elaboradoPor).Bold().FontSize(6.5f);
                                footer.Item().Text(leyendaValidez).Bold().FontSize(6.5f);
                            });
                        });
                    });
                });

                return File(document.GeneratePdf(), "application/pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar PDF: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, Dictionary<string, string>>> CargarConfiguracionesAsync()
        {
            try
            {
                return await _context.ConfiguracionesReportes
                    .AsNoTracking()
                    .Where(c => c.Reporte == "CertificacionNotas" || c.Reporte == "ConstanciaAlumnoActivo")
                    .GroupBy(c => c.Reporte)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.ToDictionary(c => c.Clave, c => c.Valor ?? ""));
            }
            catch
            {
                return new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private static string GetConfig(Dictionary<string, Dictionary<string, string>> configs, string reporte, string clave, string defaultValue)
        {
            if (configs.TryGetValue(reporte, out var reporteConfigs) &&
                reporteConfigs.TryGetValue(clave, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return defaultValue;
        }

        private static Dictionary<string, string> CrearDatosPlantilla(
            Entidades.Alumnos.Alumno alumno,
            Entidades.Procesos.Ciclo ciclo,
            DateTime fechaExpedicion,
            string emisorCargo,
            string lugarInstitucion,
            string destinatario,
            string lugarExpedicion)
        {
            string nombreCompleto = $"{alumno.Nombres} {alumno.Apellidos}".ToUpper().Trim();
            string carnet = (alumno.Carnet ?? "").Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(carnet) && !string.IsNullOrWhiteSpace(alumno.Email))
            {
                int atIndex = alumno.Email.IndexOf('@');
                if (atIndex > 0)
                {
                    carnet = alumno.Email.Substring(0, atIndex).Trim().ToUpper();
                }
            }

            bool esFemenino = alumno.Genero == 1;
            string facultad = (alumno.Carrera?.Facultad?.NombreFacultad ?? "Facultad no asignada").Trim();
            string carrera = (alumno.Carrera?.NombreCarrera ?? "Carrera no asignada").Trim();

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["emisorCargo"] = emisorCargo,
                ["lugarInstitucion"] = lugarInstitucion,
                ["nombrealumno"] = nombreCompleto,
                ["carnet"] = carnet,
                ["alumnoalumna"] = esFemenino ? "alumna" : "alumno",
                ["activoactiva"] = esFemenino ? "ACTIVA" : "ACTIVO",
                ["cicloRomano"] = ConvertirARomano(ciclo.NCiclo),
                ["facultad"] = facultad,
                ["carrera"] = carrera,
                ["cicloAcademico"] = $"{ciclo.NCiclo:D2}-{ciclo.anio}",
                ["fechaInicioCiclo"] = FormatearFechaCurso(ciclo.FechaInicio),
                ["fechaFinCiclo"] = FormatearFechaCurso(ciclo.FechaFin),
                ["destinatario"] = destinatario,
                ["lugarExpedicion"] = lugarExpedicion,
                ["fechaExpedicion"] = ConvertirFechaALetras(fechaExpedicion)
            };
        }

        private static string ReemplazarPlaceholders(string plantilla, Dictionary<string, string> datos)
        {
            string resultado = CleanHeaderString(plantilla);
            foreach (var item in datos)
            {
                resultado = resultado.Replace("{" + item.Key + "}", item.Value, StringComparison.OrdinalIgnoreCase);
            }

            return resultado;
        }

        private static void AgregarTextoConNegritas(TextDescriptor descriptor, string texto, Dictionary<string, string> datos)
        {
            var segmentosNegrita = new[]
            {
                "HACE CONSTAR QUE:",
                datos["nombrealumno"],
                $"es {datos["alumnoalumna"]} {datos["activoactiva"]} DE {datos["cicloRomano"]} CICLO",
                datos["facultad"],
                datos["carrera"],
                $"del Ciclo Académico {datos["cicloAcademico"]}, el cual dio inicio el día {datos["fechaInicioCiclo"]} y finalizará el día {datos["fechaFinCiclo"]}",
                datos["destinatario"]
            }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(s => s.Length)
            .ToList();

            int index = 0;
            while (index < texto.Length)
            {
                string? match = segmentosNegrita.FirstOrDefault(s =>
                    index + s.Length <= texto.Length &&
                    string.Compare(texto, index, s, 0, s.Length, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase) == 0);

                if (!string.IsNullOrEmpty(match))
                {
                    descriptor.Span(texto.Substring(index, match.Length)).Bold().FontSize(11);
                    index += match.Length;
                }
                else
                {
                    descriptor.Span(texto[index].ToString()).FontSize(11);
                    index++;
                }
            }
        }

        private static string CleanHeaderString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            input = input.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            while (input.Contains("  "))
            {
                input = input.Replace("  ", " ");
            }

            return input.Trim();
        }

        private static string FormatearFechaCurso(DateTime fecha)
        {
            int dia = fecha.Day;
            string mes = fecha.ToString("MMMM", new CultureInfo("es-SV")).ToLower();
            return $"{ConvertirEnteroALetras(dia).ToLower()} de {mes}";
        }

        private static string ConvertirFechaALetras(DateTime fecha)
        {
            int dia = fecha.Day;
            string mes = fecha.ToString("MMMM", new CultureInfo("es-SV")).ToLower();
            int anio = fecha.Year;

            string diaLetras = dia == 1 ? "primer" : ConvertirEnteroALetras(dia).ToLower();
            string diaTexto = dia == 1 ? "al primer día" : $"a los {diaLetras} días";
            string anioLetras = ConvertirEnteroALetras(anio).ToLower();

            return $"{diaTexto} del mes de {mes} del año {anioLetras}";
        }

        private static string ConvertirARomano(int numero)
        {
            return numero switch
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
                _ => numero.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string ConvertirEnteroALetras(int numero)
        {
            if (numero == 0) return "CERO";
            if (numero < 0) return "MENOS " + ConvertirEnteroALetras(-numero);

            string resultado = "";

            if (numero >= 1000)
            {
                int miles = numero / 1000;
                resultado = ConvertirEnteroALetras(miles) + " MIL";
                numero %= 1000;
                if (numero > 0) resultado += " ";
            }

            if (numero >= 100)
            {
                int centenas = numero / 100;
                if (centenas == 1 && numero % 100 == 0)
                {
                    resultado += "CIEN";
                    return resultado;
                }

                resultado += centenas == 1
                    ? "CIENTO"
                    : new[] { "", "", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" }[centenas];

                numero %= 100;
                if (numero > 0) resultado += " ";
            }

            if (numero >= 1 && numero <= 15)
            {
                string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE" };
                resultado += unidades[numero];
            }
            else if (numero >= 16 && numero < 20)
            {
                resultado += "DIECI" + ConvertirUnidad(numero % 10);
            }
            else if (numero >= 20 && numero < 30)
            {
                resultado += numero == 20 ? "VEINTE" : "VEINTI" + ConvertirUnidad(numero % 10);
            }
            else if (numero >= 30)
            {
                int decenas = numero / 10;
                int unidades = numero % 10;
                string[] decenasTexto = { "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
                resultado += decenasTexto[decenas];
                if (unidades > 0)
                {
                    resultado += " Y " + ConvertirUnidad(unidades);
                }
            }

            return resultado.Trim();
        }

        private static string ConvertirUnidad(int numero)
        {
            if (numero == 0) return "";
            string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            return unidades[numero];
        }
    }
}
