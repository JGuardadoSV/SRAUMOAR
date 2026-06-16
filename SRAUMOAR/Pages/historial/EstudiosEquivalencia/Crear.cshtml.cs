using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Pages.historial.EstudiosEquivalencia
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CrearModel : PageModel
    {
        private readonly Contexto _context;

        public CrearModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public int AlumnoId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "La universidad de origen es requerida")]
        [Display(Name = "Universidad de Origen")]
        public string UniversidadOrigen { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "La carrera de origen es requerida")]
        [Display(Name = "Carrera de Origen")]
        public string CarreraOrigen { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "La fecha del estudio es requerida")]
        [Display(Name = "Fecha de Evaluación")]
        public DateTime FechaEstudio { get; set; } = DateTime.Now;

        [BindProperty]
        public List<DetalleEquivalenciaViewModel> Detalles { get; set; } = new();

        public List<Materia> MateriasDisponibles { get; set; } = new();
        public string NombreAlumno { get; set; } = string.Empty;
        public string DetallesJson { get; set; } = "[]";

        [BindProperty]
        public string TipoEquivalencia { get; set; } = "Externa";

        public List<Carrera> CarrerasDisponiblesUmoar { get; set; } = new();

        public class DetalleEquivalenciaViewModel
        {
            [Required(ErrorMessage = "Debe seleccionar una materia de destino")]
            public int MateriaDestinoId { get; set; }

            [Required]
            [Range(7.0, 10.0, ErrorMessage = "La nota homologada en UMOAR debe ser aprobada (entre 7.0 y 10.0)")]
            public decimal NotaEquivalencia { get; set; }

            public List<MateriaOrigenViewModel> MateriasOrigen { get; set; } = new();
        }

        public class MateriaOrigenViewModel
        {
            [Required(ErrorMessage = "El código de materia origen es requerido")]
            public string MateriaOrigenCodigo { get; set; } = string.Empty;

            [Required(ErrorMessage = "El nombre de materia origen es requerido")]
            public string MateriaOrigenNombre { get; set; } = string.Empty;

            [Required]
            [Range(0, 10, ErrorMessage = "La nota origen debe estar entre 0 y 10")]
            public decimal NotaOrigen { get; set; }

            [Required]
            [Range(1, 20, ErrorMessage = "Las UV deben ser mayor a 0")]
            public int uv { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? alumnoId)
        {
            if (alumnoId.HasValue)
            {
                var alumno = await _context.Alumno.FindAsync(alumnoId.Value);
                if (alumno != null)
                {
                    AlumnoId = alumno.AlumnoId;
                    NombreAlumno = $"{alumno.Apellidos}, {alumno.Nombres}";
                }
            }

            await CargarMaterias();
            await PrepararDetallesJson();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (TipoEquivalencia == "Interna")
            {
                UniversidadOrigen = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
                ModelState.Remove(nameof(UniversidadOrigen));
            }

            if (AlumnoId == 0)
            {
                ModelState.AddModelError(nameof(AlumnoId), "Debe seleccionar un alumno.");
            }

            if (Detalles == null || !Detalles.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos una materia homologada.");
            }

            if (!ModelState.IsValid)
            {
                await CargarMaterias();
                await PrepararDetallesJson();
                if (AlumnoId != 0)
                {
                    var alumno = await _context.Alumno.FindAsync(AlumnoId);
                    if (alumno != null)
                    {
                        NombreAlumno = $"{alumno.Apellidos}, {alumno.Nombres}";
                    }
                }
                return Page();
            }

            try
            {
                var estudio = new EstudioEquivalencia
                {
                    AlumnoId = AlumnoId,
                    UniversidadOrigen = UniversidadOrigen,
                    CarreraOrigen = CarreraOrigen,
                    FechaEstudio = FechaEstudio,
                    Estado = "Borrador", // Se crea en estado Borrador inicialmente
                    FechaAprobacion = null,
                    EsInterno = (TipoEquivalencia == "Interna")
                };

                _context.EstudiosEquivalencia.Add(estudio);
                await _context.SaveChangesAsync(); // Generar ID de cabecera

                foreach (var det in Detalles)
                {
                    var detalle = new DetalleEquivalencia
                    {
                        EstudioEquivalenciaId = estudio.EstudioEquivalenciaId,
                        MateriaDestinoId = det.MateriaDestinoId,
                        NotaEquivalencia = det.NotaEquivalencia,
                        MateriasOrigen = det.MateriasOrigen.Select(mo => new DetalleEquivalenciaOrigen
                        {
                            MateriaOrigenCodigo = mo.MateriaOrigenCodigo,
                            MateriaOrigenNombre = mo.MateriaOrigenNombre,
                            NotaOrigen = mo.NotaOrigen,
                            uv = mo.uv
                        }).ToList()
                    };
                    _context.DetallesEquivalencia.Add(detalle);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Estudio de equivalencia guardado como borrador exitosamente.";
                return RedirectToPage("/historial/EstudiosEquivalencia/Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ocurrió un error al guardar el estudio: " + ex.Message);
                await CargarMaterias();
                await PrepararDetallesJson();
                return Page();
            }
        }

        public async Task<JsonResult> OnGetBuscarMateriasAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new JsonResult(new List<object>());
            }

            var termLower = term.ToLower();

            var materias = await _context.Materias
                .Include(m => m.Pensum)
                    .ThenInclude(p => p!.Carrera)
                .Where(m => (m.NombreMateria != null && m.NombreMateria.ToLower().Contains(termLower)) || 
                            (m.CodigoMateria != null && m.CodigoMateria.ToLower().Contains(termLower)))
                .Take(25)
                .Select(m => new
                {
                    id = m.MateriaId,
                    codigo = m.CodigoMateria ?? string.Empty,
                    nombre = m.NombreMateria ?? string.Empty,
                    carrera = m.Pensum != null && m.Pensum.Carrera != null ? m.Pensum.Carrera.NombreCarrera : "Sin Carrera",
                    pensum = m.Pensum != null ? m.Pensum.NombrePensum : "Sin Pensum",
                    ciclo = m.Ciclo,
                    uv = m.uv
                })
                .ToListAsync();

            return new JsonResult(materias);
        }

        private async Task PrepararDetallesJson()
        {
            if (Detalles != null && Detalles.Any())
            {
                var materiaIds = Detalles.Select(d => d.MateriaDestinoId).Distinct().ToList();
                var materiasInfo = await _context.Materias
                    .Include(m => m.Pensum)
                        .ThenInclude(p => p!.Carrera)
                    .Where(m => materiaIds.Contains(m.MateriaId))
                    .ToDictionaryAsync(m => m.MateriaId);

                var listToSerialize = Detalles.Select(d => {
                    var mInfo = materiasInfo.TryGetValue(d.MateriaDestinoId, out var m) ? m : null;
                    return new {
                        materiaDestinoId = d.MateriaDestinoId,
                        notaEquivalencia = d.NotaEquivalencia,
                        materiaDestinoNombre = mInfo != null ? $"{mInfo.NombreMateria} ({mInfo.CodigoMateria})" : "",
                        materiaDestinoInfo = mInfo != null ? $"<strong>{mInfo.NombreMateria} ({mInfo.CodigoMateria})</strong><br>Carrera: {(mInfo.Pensum?.Carrera?.NombreCarrera ?? "Sin Carrera")}<br>Pensum: {(mInfo.Pensum?.NombrePensum ?? "Sin Pensum")}" : "",
                        materiaDestinoUvs = mInfo != null ? mInfo.uv : 0,
                        materiasOrigen = d.MateriasOrigen.Select(mo => new {
                            materiaOrigenCodigo = mo.MateriaOrigenCodigo,
                            materiaOrigenNombre = mo.MateriaOrigenNombre,
                            notaOrigen = mo.NotaOrigen,
                            uv = mo.uv
                        }).ToList()
                    };
                }).ToList();

                DetallesJson = System.Text.Json.JsonSerializer.Serialize(listToSerialize);
            }
            else
            {
                DetallesJson = "[]";
            }
        }

        private async Task CargarMaterias()
        {
            MateriasDisponibles = new List<Materia>();
            CarrerasDisponiblesUmoar = await _context.Carreras.OrderBy(c => c.NombreCarrera).ToListAsync();
        }

        public async Task<JsonResult> OnGetBuscarMateriasAprobadasAlumnoAsync(int alumnoId)
        {
            var materias = await _context.HistorialMateria
                .Include(hm => hm.Materia)
                .Include(hm => hm.HistorialCiclo)
                    .ThenInclude(hc => hc.HistorialAcademico)
                        .ThenInclude(ha => ha.Carrera)
                .Where(hm => hm.HistorialCiclo.HistorialAcademico.AlumnoId == alumnoId && hm.Aprobada)
                .Select(hm => new
                {
                    id = hm.MateriaId,
                    codigo = hm.Materia != null ? hm.Materia.CodigoMateria : (hm.MateriaCodigoLibre ?? string.Empty),
                    nombre = hm.Materia != null ? hm.Materia.NombreMateria : (hm.MateriaNombreLibre ?? string.Empty),
                    nota = hm.Promedio,
                    uv = hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0),
                    carrera = hm.HistorialCiclo.HistorialAcademico.Carrera != null ? hm.HistorialCiclo.HistorialAcademico.Carrera.NombreCarrera : "Sin Carrera",
                    ciclo = hm.HistorialCiclo.CicloTexto
                })
                .ToListAsync();

            return new JsonResult(materias);
        }
    }
}
