using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Materias;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
namespace SRAUMOAR.Pages.portal.estudiante
{

    //Allow only Estudiantes rol
    [Authorize(Roles = "Estudiantes")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(SRAUMOAR.Modelos.Contexto context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }
        public Alumno Alumno { get; set; } = default!;
        public Ciclo Ciclo { get; set; } = default!;
        public bool EsBecado { get; set; } = false;
        public bool TieneArancelesRetrasados { get; set; } = false;
        public bool EstaEnGrupoEspecializacion { get; set; } = false;
        public int MateriasAprobadas { get; set; } = 0;
        public int TotalMaterias { get; set; } = 0;
        public decimal PromedioGlobal { get; set; } = 0;
        public decimal CUM { get; set; } = 0;
        public decimal PorcentajeAvance { get; set; } = 0;
        [BindProperty]
        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<Arancel> Arancel { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademica { get; set; } = default!;
        public IList<DetallesCobroArancel> DetallesCobroArancel { get; set; } = default!;
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue("UserId") ?? "0";
            if (!int.TryParse(userId, out int idusuario) || idusuario <= 0)
            {
                TempData["ErrorMessage"] = "No se pudo identificar el usuario autenticado.";
                return RedirectToPage("/Index");
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(x => x.IdUsuario == idusuario);
            if (usuario == null)
            {
                TempData["ErrorMessage"] = "El usuario autenticado ya no existe en el sistema.";
                return RedirectToPage("/Index");
            }

            Alumno = await _context.Alumno
                .Include(c => c.Carrera)
                .FirstOrDefaultAsync(c => c.UsuarioId == idusuario);
            if (Alumno == null)
            {
                TempData["ErrorMessage"] = "Tu usuario no está vinculado a un registro de alumno. Contacta al administrador.";
                return RedirectToPage("/Index");
            }

            Ciclo = await _context.Ciclos.FirstOrDefaultAsync(x => x.Activo == true);
            if (Ciclo == null)
            {
                TempData["ErrorMessage"] = "No hay un ciclo activo configurado.";
                return RedirectToPage("/Index");
            }

            // Verificar si el alumno es becado en el ciclo actual
            EsBecado = await _context.Becados
                .AnyAsync(b => b.AlumnoId == Alumno.AlumnoId && 
                              b.CicloId == Ciclo.Id && 
                              b.Estado == true);

            // Verificar si el alumno estÃ¡ inscrito en algÃºn grupo de especializaciÃ³n del ciclo actual
            EstaEnGrupoEspecializacion = await _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Grupo)
                .AnyAsync(mi => mi.AlumnoId == Alumno.AlumnoId &&
                                mi.MateriasGrupo.Grupo.CicloId == Ciclo.Id &&
                                mi.MateriasGrupo.Grupo.EsEspecializacion);

            //seleccionar todas las materias inscritas por el alumno
            MateriasInscritas = await _context.MateriasInscritas
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Materia)
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Docente)
     .Include(mi => mi.MateriasGrupo)
         .ThenInclude(mg => mg.Grupo)  // Agregamos esta lï¿½nea para incluir el Grupo
     .Include(mi => mi.Notas)
         .ThenInclude(n => n.ActividadAcademica)
     .Where(mi => mi.MateriasGrupo.Grupo.CicloId == Ciclo.Id &&
                  mi.Alumno.AlumnoId == Alumno.AlumnoId)
     .ToListAsync();

            // Obtener actividades acadÃ©micas del ciclo para calcular promedios
            ActividadAcademica = await _context.ActividadesAcademicas
                .Include(a => a.Arancel)
                .Include(a => a.Ciclo)
                .Where(c => c.CicloId == Ciclo.Id)
                .ToListAsync();

            // Calcular y actualizar promedios si estÃ¡n en 0.0 o desactualizados
            bool hayCambios = false;
            foreach (var materia in MateriasInscritas)
            {
                // Recalcular el promedio si hay actividades acadÃ©micas definidas
                if (ActividadAcademica != null && ActividadAcademica.Any())
                {
                    var promedioCalculado = CalcularPromedioMateria(materia.Notas, ActividadAcademica);
                    var notaFinal = CalcularNotaFinal(materia, promedioCalculado);
                    
                    // Actualizar si el promedio calculado es diferente al almacenado
                    if (materia.NotaPromedio != notaFinal)
                    {
                        materia.NotaPromedio = notaFinal;
                        _context.MateriasInscritas.Update(materia);
                        hayCambios = true;
                    }
                }
            }

            // Guardar cambios si hubo actualizaciones
            if (hayCambios)
            {
                await _context.SaveChangesAsync();
            }

            // Consulta modificada para manejar mï¿½ltiples pagos
            var todosLosAranceles = await _context.Aranceles.Where(x => x.Ciclo.Id == Ciclo.Id)
                 .Include(a => a.Ciclo).ToListAsync();

            DetallesCobroArancel = await _context.DetallesCobrosArancel
                .Include(x => x.CobroArancel)
                .Include(x => x.Arancel)
                .Where(x => x.CobroArancel.CicloId == Ciclo.Id && x.CobroArancel.AlumnoId == Alumno.AlumnoId).ToListAsync();

            // Filtrar aranceles segÃºn si el estudiante estÃ¡ en grupo de pre-especializaciÃ³n
            if (EstaEnGrupoEspecializacion)
            {
                // Si estÃ¡ en grupo de especializaciÃ³n:
                // - Mostrar aranceles obligatorios que sean de especializaciÃ³n
                // - Mostrar todos los aranceles no obligatorios (que no sean de especializaciÃ³n)
                Arancel = todosLosAranceles.Where(a => 
                    (a.Obligatorio && a.EsEspecializacion) || 
                    (!a.Obligatorio && !a.EsEspecializacion)
                ).ToList();
            }
            else
            {
                // Si NO estÃ¡ en grupo de especializaciÃ³n:
                // - Mostrar solo aranceles obligatorios normales (no de especializaciÃ³n)
                // - Mostrar todos los aranceles no obligatorios (que no sean de especializaciÃ³n)
                Arancel = todosLosAranceles.Where(a => 
                    (a.Obligatorio && !a.EsEspecializacion) || 
                    (!a.Obligatorio && !a.EsEspecializacion)
                ).ToList();
            }

            // Verificar si tiene aranceles retrasados (solo si no es becado)
            if (!EsBecado)
            {
                // Determinar quÃ© aranceles verificar segÃºn si estÃ¡ en grupo de especializaciÃ³n
                var arancelesAVerificar = Arancel.AsEnumerable();
                if (EstaEnGrupoEspecializacion)
                {
                    // Si estÃ¡ en grupo de especializaciÃ³n, solo verificar aranceles obligatorios de especializaciÃ³n
                    arancelesAVerificar = Arancel.Where(a => !a.Obligatorio || a.EsEspecializacion);
                }
                else
                {
                    // Si no estÃ¡ en grupo de especializaciÃ³n, verificar todos los aranceles obligatorios
                    arancelesAVerificar = Arancel;
                }

                TieneArancelesRetrasados = arancelesAVerificar.Any(a => !DetallesCobroArancel
                    .Any(dc => dc.ArancelId == a.ArancelId && dc.CobroArancel.AlumnoId == Alumno.AlumnoId)
                    && (a.FechaFin < DateTime.Now));
            }


            // Calcular estadÃ­sticas del historial acadÃ©mico
            var historialAcademico = await _context.HistorialAcademico
                .Include(h => h.CiclosHistorial)
                    .ThenInclude(hc => hc.MateriasHistorial)
                        .ThenInclude(hm => hm.Materia)
                .Where(h => h.AlumnoId == Alumno.AlumnoId)
                .ToListAsync();

            if (historialAcademico != null && historialAcademico.Any())
            {
                var todosLosCiclos = historialAcademico
                    .SelectMany(h => h.CiclosHistorial ?? new List<HistorialCiclo>())
                    .ToList();

                if (todosLosCiclos.Any())
                {
                    var todasLasMaterias = todosLosCiclos
                        .SelectMany(hc => hc.MateriasHistorial ?? new List<HistorialMateria>())
                        .ToList();

                    TotalMaterias = todasLasMaterias.Count;
                    MateriasAprobadas = todasLasMaterias.Count(m => m.Aprobada);
                    
                    if (TotalMaterias > 0)
                    {
                        PromedioGlobal = Math.Round(todasLasMaterias.Average(m => m.Promedio), 1);
                    }

                    // Calcular CUM: suma de (promedio * UV) / suma de UV
                    decimal totalUV = todasLasMaterias.Sum(hm => 
                        hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0));
                    
                    decimal sumaPromedioPorUV = todasLasMaterias.Sum(hm => 
                    {
                        decimal uv = hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0);
                        return hm.Promedio * uv;
                    });
                    
                    if (totalUV > 0)
                    {
                        CUM = Math.Round(sumaPromedioPorUV / totalUV, 1);
                    }
                }
            }

            // Calcular porcentaje de avance basado en el pensum activo de la carrera
            if (Alumno.CarreraId.HasValue)
            {
                // Obtener el pensum activo de la carrera del alumno
                var pensumActivo = await _context.Pensums
                    .Include(p => p.Materias)
                    .Where(p => p.CarreraId == Alumno.CarreraId.Value && p.Activo == true)
                    .FirstOrDefaultAsync();

                if (pensumActivo != null && pensumActivo.Materias != null)
                {
                    // Total de materias en el pensum
                    int totalMateriasPensum = pensumActivo.Materias.Count;

                    if (totalMateriasPensum > 0)
                    {
                        // Obtener las materias aprobadas del alumno que pertenecen a este pensum
                        var historialAcademicoPensum = await _context.HistorialAcademico
                            .Include(h => h.CiclosHistorial)
                                .ThenInclude(hc => hc.MateriasHistorial)
                                    .ThenInclude(hm => hm.Materia)
                            .Where(h => h.AlumnoId == Alumno.AlumnoId && h.CarreraId == Alumno.CarreraId.Value)
                            .FirstOrDefaultAsync();

                        int materiasAprobadasPensum = 0;
                        if (historialAcademicoPensum != null && historialAcademicoPensum.CiclosHistorial != null)
                        {
                            materiasAprobadasPensum = historialAcademicoPensum.CiclosHistorial
                                .SelectMany(hc => hc.MateriasHistorial ?? new List<HistorialMateria>())
                                .Where(hm => hm.Aprobada && 
                                            hm.Materia != null && 
                                            hm.Materia.PensumId == pensumActivo.PensumId)
                                .Select(hm => hm.MateriaId)
                                .Distinct()
                                .Count();
                        }

                        // Calcular porcentaje de avance
                        PorcentajeAvance = Math.Round((decimal)materiasAprobadasPensum / totalMateriasPensum * 100, 1);
                    }
                }
            }

            return Page();

        }

        public IActionResult OnGetGenerarPDFSinDatos()
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            document.Add(new Paragraph("Aviso")
                .SetFontSize(18)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Add(new Paragraph("No hay datos disponibles para generar el PDF.")
                .SetFontSize(12)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Close();
            Response.Headers["Content-Disposition"] = "inline; filename=SinDatos.pdf";
            return File(memoryStream.ToArray(), "application/pdf");

        }

        public async Task<IActionResult> OnGetGenerarPdfAsync(int id)
        {
            try
            {
                // Aquï¿½ puedes agregar tu lï¿½gica para obtener el JSON y sello
                // Por ahora uso valores de ejemplo
                CobroArancel cobroArancel = await _context.CobrosArancel
                    .Include(c => c.Alumno)
                    .Include(c => c.Ciclo)
                    .FirstOrDefaultAsync(c => c.CobroArancelId == id);

                if ((cobroArancel==null))
                {

                    return OnGetGenerarPDFSinDatos();
                }


                Factura factura = await _context.Facturas.FirstOrDefaultAsync(f => f.CodigoGeneracion == cobroArancel.CodigoGeneracion);
                if (string.IsNullOrWhiteSpace(factura?.JsonDte))
                {

                    return OnGetGenerarPDFSinDatos();
                }


                var dteJson = factura.JsonDte; // Reemplazar con tu lï¿½gica
                var selloRecibido = factura.SelloRecepcion; // Reemplazar con tu lï¿½gica
                var tipo = factura.TipoDTE.ToString().PadLeft(2, '0');

                // Datos que necesitas enviar
                var requestData = new
                {
                    dteJson = dteJson,
                    selloRecibido = selloRecibido,
                    tipoDte = tipo
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                //var response = await client.PostAsync("https://localhost:7122/api/generar-pdf", content);
                var response = await client.PostAsync("http://207.58.153.147:7122/api/generar-pdf", content);

                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();

                    //Console.WriteLine($"[DEBUG CLIENT] PDF recibido, tamaï¿½o: {pdfBytes.Length} bytes");

                    // Respuesta mï¿½s simple - deja que la API maneje los headers
                    return File(pdfBytes, "application/pdf");
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR CLIENT] Error de API: {errorMessage}");
                    TempData["Error"] = $"Error al generar PDF: {errorMessage}";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR CLIENT] Excepciï¿½n: {ex}");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostActualizarFotoAsync(IFormFile nuevaFoto)
        {
            try
            {
                if (nuevaFoto == null || nuevaFoto.Length == 0)
                {
                    return new JsonResult(new { success = false, message = "No se seleccionÃ³ ninguna imagen." });
                }

                // Validar tamaÃ±o (5MB mÃ¡ximo antes de optimizaciÃ³n)
                if (nuevaFoto.Length > 5 * 1024 * 1024)
                {
                    return new JsonResult(new { success = false, message = "La imagen es demasiado grande. El tamaÃ±o mÃ¡ximo es 5MB." });
                }

                // Validar tipo de archivo
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(nuevaFoto.ContentType.ToLower()))
                {
                    return new JsonResult(new { success = false, message = "Formato de imagen no vÃ¡lido. Solo se permiten JPG y PNG." });
                }

                // Obtener el alumno actual
                var userId = User.FindFirstValue("UserId") ?? "0";
                int idusuario = int.Parse(userId);
                var alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.UsuarioId == idusuario);

                if (alumno == null)
                {
                    return new JsonResult(new { success = false, message = "Alumno no encontrado." });
                }

                // Optimizar y comprimir la imagen
                var fotoBytes = await OptimizarImagenAsync(nuevaFoto);

                // Actualizar la foto en la base de datos
                alumno.Foto = fotoBytes;
                _context.Alumno.Update(alumno);
                await _context.SaveChangesAsync();

                return new JsonResult(new { 
                    success = true, 
                    message = $"Foto actualizada exitosamente. TamaÃ±o optimizado: {(fotoBytes.Length / 1024.0):F1} KB" 
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error al actualizar la foto: {ex.Message}" });
            }
        }

        // MÃ©todo para calcular el promedio de una materia usando todas las actividades acadÃ©micas
        private static decimal CalcularPromedioMateria(ICollection<Notas> notas, IList<ActividadAcademica> actividadesAcademicas)
        {
            if (actividadesAcademicas == null || !actividadesAcademicas.Any())
                return 0;

            decimal sumaPonderada = 0;
            decimal totalPorcentaje = 0;

            // Iterar sobre TODAS las actividades acadÃ©micas del ciclo
            foreach (var actividad in actividadesAcademicas)
            {
                if (actividad == null) continue;

                int porcentaje = actividad.Porcentaje;
                totalPorcentaje += porcentaje;

                // Buscar si existe una nota registrada para esta actividad
                var notaRegistrada = notas
                    ?.FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId);

                // Si existe nota registrada, usar su valor; si no, usar 0
                decimal valorNota = notaRegistrada?.Nota ?? 0;

                sumaPonderada += valorNota * porcentaje;
            }

            // Si no hay porcentajes, retornar 0
            if (totalPorcentaje <= 0) return 0;

            return Math.Round(sumaPonderada / totalPorcentaje, 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Calcula la nota final aplicando las reglas de reposiciÃ³n
        /// </summary>
        private static decimal CalcularNotaFinal(MateriasInscritas materiaInscrita, decimal promedioCalculado)
        {
            // Aplicar regla de reposiciÃ³n
            if (materiaInscrita.NotaRecuperacion.HasValue)
            {
                if (materiaInscrita.NotaRecuperacion.Value >= 7)
                {
                    // Si aprobÃ³ recuperaciÃ³n (>=7), la nota final es 7
                    return 7;
                }
                else
                {
                    // Si tiene nota de recuperaciÃ³n pero reprobÃ³ (<7), usar esa nota redondeada a 1 decimal
                    return Math.Round(materiaInscrita.NotaRecuperacion.Value, 1, MidpointRounding.AwayFromZero);
                }
            }

            // Si no tiene nota de recuperaciÃ³n, usar el promedio calculado (ya redondeado a 1 decimal)
            return promedioCalculado;
        }

        private async Task<byte[]> OptimizarImagenAsync(IFormFile imagen)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await imagen.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var image = await ImageSharpImage.LoadAsync(memoryStream);
                
                // Redimensionar si es muy grande (mÃ¡ximo 300x300 pÃ­xeles)
                var maxSize = 300;
                if (image.Width > maxSize || image.Height > maxSize)
                {
                    var ratio = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);
                    
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }

                // Convertir a JPEG con compresiÃ³n
                using var outputStream = new MemoryStream();
                await image.SaveAsync(outputStream, new JpegEncoder
                {
                    Quality = 85 // Calidad del 85% para buen balance entre tamaÃ±o y calidad
                });

                var optimizedBytes = outputStream.ToArray();
                
                // Log del tamaÃ±o optimizado
                var originalSize = memoryStream.Length;
                var optimizedSize = optimizedBytes.Length;
                var compressionRatio = (double)optimizedSize / originalSize * 100;
                
                Console.WriteLine($"Imagen optimizada: {originalSize / 1024.0:F1} KB -> {optimizedSize / 1024.0:F1} KB ({compressionRatio:F1}%)");
                
                return optimizedBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al optimizar imagen: {ex.Message}");
                // Si falla la optimizaciÃ³n, devolver la imagen original
                using var memoryStream = new MemoryStream();
                await imagen.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}

