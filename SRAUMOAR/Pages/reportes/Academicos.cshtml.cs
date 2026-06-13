using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.reportes
{
    public class AcademicosModel : PageModel
    {
        private readonly Contexto _context;

        public AcademicosModel(Contexto context)
        {
            _context = context;
        }

        public Alumno? AlumnoSelected { get; set; }
        public List<Ciclo> CiclosList { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? AlumnoId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCicloId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Cargar la lista de todos los ciclos
            CiclosList = await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .ToListAsync();

            // 2. Si hay un AlumnoId, cargar la información del alumno
            if (AlumnoId.HasValue && AlumnoId.Value > 0)
            {
                AlumnoSelected = await _context.Alumno
                    .Include(a => a.Carrera)
                        .ThenInclude(c => c!.Facultad)
                    .FirstOrDefaultAsync(a => a.AlumnoId == AlumnoId.Value);

                if (AlumnoSelected == null)
                {
                    TempData["Error"] = "El alumno especificado no fue encontrado o no existe.";
                    return Page();
                }

                // 3. Resolver el ciclo seleccionado por defecto
                if (!SelectedCicloId.HasValue || SelectedCicloId.Value <= 0)
                {
                    // Intentar buscar el ciclo activo
                    var cicloActivo = CiclosList.FirstOrDefault(c => c.Activo);
                    if (cicloActivo != null)
                    {
                        SelectedCicloId = cicloActivo.Id;
                    }
                    else if (CiclosList.Any())
                    {
                        // Si no hay ciclo activo, tomar el más reciente
                        SelectedCicloId = CiclosList.First().Id;
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnGetBuscarAlumnosAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new JsonResult(new List<object>());

            // Dividir el término por espacios para obtener palabras individuales
            var palabras = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var query = _context.Alumno.AsNoTracking().Where(a => a.Estado == 1);

            // Cada palabra debe coincidir en alguno de los campos clave (Apellidos, Nombres, Carnet o Email)
            foreach (var palabra in palabras)
            {
                var p = palabra.Trim();
                query = query.Where(a => a.Apellidos.Contains(p) ||
                                         a.Nombres.Contains(p) ||
                                         (a.Carnet != null && a.Carnet.Contains(p)) ||
                                         (a.Email != null && a.Email.Contains(p)));
            }

            var alumnos = await query
                .OrderBy(a => a.Apellidos)
                .ThenBy(a => a.Nombres)
                .Select(a => new
                {
                    a.AlumnoId,
                    a.Apellidos,
                    a.Nombres,
                    a.Email,
                    a.Carnet
                })
                .Take(20)
                .ToListAsync();

            var resultado = alumnos.Select(a =>
            {
                string carnet = string.IsNullOrWhiteSpace(a.Carnet)
                    ? (string.IsNullOrEmpty(a.Email) ? "Sin carnet" : a.Email.Split('@')[0])
                    : a.Carnet;

                return new
                {
                    id = a.AlumnoId,
                    label = $"{a.Apellidos}, {a.Nombres} - {carnet}",
                    value = $"{a.Apellidos}, {a.Nombres}",
                    carnet = carnet
                };
            }).ToList();

            return new JsonResult(resultado);
        }
    }
}
