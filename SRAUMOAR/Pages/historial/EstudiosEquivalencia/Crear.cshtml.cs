using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Materias;
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

        public class DetalleEquivalenciaViewModel
        {
            [Required(ErrorMessage = "El código de materia origen es requerido")]
            public string MateriaOrigenCodigo { get; set; } = string.Empty;

            [Required(ErrorMessage = "El nombre de materia origen es requerido")]
            public string MateriaOrigenNombre { get; set; } = string.Empty;

            [Required]
            [Range(0, 10, ErrorMessage = "La nota origen debe estar entre 0 y 10")]
            public decimal NotaOrigen { get; set; }

            [Required(ErrorMessage = "Debe seleccionar una materia de destino")]
            public int MateriaDestinoId { get; set; }

            [Required]
            [Range(7.0, 10.0, ErrorMessage = "La nota homologada en UMOAR debe ser aprobada (entre 7.0 y 10.0)")]
            public decimal NotaEquivalencia { get; set; }
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
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
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
                    FechaAprobacion = null
                };

                _context.EstudiosEquivalencia.Add(estudio);
                await _context.SaveChangesAsync(); // Generar ID de cabecera

                foreach (var det in Detalles)
                {
                    var detalle = new DetalleEquivalencia
                    {
                        EstudioEquivalenciaId = estudio.EstudioEquivalenciaId,
                        MateriaOrigenCodigo = det.MateriaOrigenCodigo,
                        MateriaOrigenNombre = det.MateriaOrigenNombre,
                        NotaOrigen = det.NotaOrigen,
                        MateriaDestinoId = det.MateriaDestinoId,
                        NotaEquivalencia = det.NotaEquivalencia
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
                return Page();
            }
        }

        private async Task CargarMaterias()
        {
            MateriasDisponibles = await _context.Materias
                .OrderBy(m => m.NombreMateria)
                .ToListAsync();
        }
    }
}
