using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class RegistroManualModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public RegistroManualModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public CobroArancel CobroArancel { get; set; } = new CobroArancel();

        [BindProperty]
        public List<int> SelectedAranceles { get; set; } = new List<int>();

        [BindProperty]
        public List<decimal> PreciosPersonalizados { get; set; } = new List<decimal>();

        [BindProperty]
        public string CodigoGeneracion { get; set; } = "";

        [BindProperty]
        public DateTime FechaPago { get; set; } = DateTime.Now;

        // Listas para los dropdowns
        public SelectList Alumnos { get; set; } = new SelectList(new List<object>());
        public SelectList Aranceles { get; set; } = new SelectList(new List<object>());
        public SelectList Ciclos { get; set; } = new SelectList(new List<object>());

        // Propiedades para mostrar información
        public Alumno? AlumnoSeleccionado { get; set; }
        public List<Arancel> ArancelesDisponibles { get; set; } = new List<Arancel>();
        public Dictionary<int, decimal> PreciosConDescuento { get; set; } = new Dictionary<int, decimal>();
        public bool AlumnoTieneBecaParcial { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarDatosParaVista();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosParaVista();
                return Page();
            }

            // Validaciones básicas
            if (CobroArancel.AlumnoId == null)
            {
                ModelState.AddModelError("CobroArancel.AlumnoId", "Debe seleccionar un alumno");
                await CargarDatosParaVista();
                return Page();
            }

            if (SelectedAranceles == null || !SelectedAranceles.Any())
            {
                ModelState.AddModelError("SelectedAranceles", "Debe seleccionar al menos un arancel");
                await CargarDatosParaVista();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(CodigoGeneracion))
            {
                ModelState.AddModelError("CodigoGeneracion", "Debe ingresar el código de generación");
                await CargarDatosParaVista();
                return Page();
            }

            // Obtener el alumno
            var alumno = await _context.Alumno
                .Include(a => a.Carrera)
                .FirstOrDefaultAsync(a => a.AlumnoId == CobroArancel.AlumnoId);

            if (alumno == null)
            {
                ModelState.AddModelError("CobroArancel.AlumnoId", "El alumno seleccionado no existe");
                await CargarDatosParaVista();
                return Page();
            }

            // Verificar si el alumno tiene beca parcial
            var becaAlumno = await _context.Becados
                .Where(b => b.AlumnoId == CobroArancel.AlumnoId && b.Estado && b.TipoBeca == 2)
                .FirstOrDefaultAsync();

            Dictionary<int, decimal> preciosPersonalizados = new Dictionary<int, decimal>();
            if (becaAlumno != null)
            {
                var arancelesPersonalizados = await _context.ArancelesBecados
                    .Where(ab => ab.BecadosId == becaAlumno.BecadosId && ab.Activo)
                    .ToListAsync();

                foreach (var personalizado in arancelesPersonalizados)
                {
                    preciosPersonalizados[personalizado.ArancelId] = personalizado.PrecioPersonalizado;
                }
            }

            // Crear la lista de detalles de cobro
            List<DetallesCobroArancel> aranceles = new List<DetallesCobroArancel>();
            bool algunConMora = false;

            for (int i = 0; i < SelectedAranceles.Count; i++)
            {
                var arancelDb = await _context.Aranceles.FindAsync(SelectedAranceles[i]);
                if (arancelDb == null) continue;

                var vencido = arancelDb.EstaVencido;
                var mostrarMora = vencido && !alumno.ExentoMora;

                // Determinar el costo base (precio personalizado si existe, sino el original)
                decimal costoBase;
                if (preciosPersonalizados.ContainsKey(SelectedAranceles[i]))
                {
                    costoBase = preciosPersonalizados[SelectedAranceles[i]];
                }
                else
                {
                    costoBase = arancelDb.Costo;
                }

                // Aplicar mora si es necesario
                var costoFinal = mostrarMora ? costoBase + arancelDb.ValorMora : costoBase;

                if (mostrarMora)
                {
                    algunConMora = true;
                }

                DetallesCobroArancel arancel = new DetallesCobroArancel();
                arancel.ArancelId = SelectedAranceles[i];
                arancel.costo = costoFinal;
                aranceles.Add(arancel);
            }

            // Configurar el cobro de arancel
            CobroArancel.DetallesCobroArancel = aranceles;
            CobroArancel.Fecha = FechaPago;
            CobroArancel.CodigoGeneracion = CodigoGeneracion.ToUpper();

            // Agregar nota si algún arancel fue cobrado con mora
            if (algunConMora)
            {
                if (string.IsNullOrWhiteSpace(CobroArancel.nota))
                    CobroArancel.nota = "con mora incluida";
                else if (!CobroArancel.nota.Contains("con mora incluida"))
                    CobroArancel.nota += " (con mora incluida)";
            }

            // Asignar el total real cobrado (incluyendo mora si aplica)
            CobroArancel.Total = aranceles.Sum(a => a.costo);

            // Asignar el CicloId si existe un ciclo en los aranceles
            var cicloDeAranceles = await _context.Aranceles
                .Where(a => SelectedAranceles.Contains(a.ArancelId) && a.CicloId.HasValue)
                .Include(a => a.Ciclo)
                .FirstOrDefaultAsync();

            if (cicloDeAranceles?.Ciclo != null)
            {
                CobroArancel.CicloId = cicloDeAranceles.Ciclo.Id;
            }

            // Guardar en la base de datos
            _context.CobrosArancel.Add(CobroArancel);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Registro manual de arancel creado exitosamente. Total: ${CobroArancel.Total:F2}";
            return RedirectToPage("./Facturas");
        }

        public async Task<IActionResult> OnGetAlumnoInfoAsync(int alumnoId)
        {
            var alumno = await _context.Alumno
                .Include(a => a.Carrera)
                .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId);

            if (alumno == null)
            {
                return NotFound();
            }

            // Verificar si tiene beca parcial
            var becaAlumno = await _context.Becados
                .Where(b => b.AlumnoId == alumnoId && b.Estado && b.TipoBeca == 2)
                .FirstOrDefaultAsync();

            var tieneBecaParcial = becaAlumno != null;

            // Obtener aranceles disponibles
            var aranceles = await _context.Aranceles
                .Where(a => a.Activo)
                .Include(a => a.Ciclo)
                .OrderBy(a => a.Nombre)
                .ToListAsync();

            // Obtener precios personalizados si tiene beca
            Dictionary<int, decimal> preciosPersonalizados = new Dictionary<int, decimal>();
            if (tieneBecaParcial && becaAlumno != null)
            {
                var arancelesPersonalizados = await _context.ArancelesBecados
                    .Where(ab => ab.BecadosId == becaAlumno.BecadosId && ab.Activo)
                    .ToListAsync();

                foreach (var personalizado in arancelesPersonalizados)
                {
                    preciosPersonalizados[personalizado.ArancelId] = personalizado.PrecioPersonalizado;
                }
            }

            return new JsonResult(new
            {
                alumno = new
                {
                    nombres = alumno.Nombres,
                    apellidos = alumno.Apellidos,
                    carrera = alumno.Carrera?.NombreCarrera,
                    exentoMora = alumno.ExentoMora
                },
                tieneBecaParcial = tieneBecaParcial,
                aranceles = aranceles.Select(a => new
                {
                    id = a.ArancelId,
                    nombre = a.Nombre,
                    costo = a.Costo,
                    costoConMora = a.EstaVencido && !alumno.ExentoMora ? a.Costo + a.ValorMora : a.Costo,
                    precioPersonalizado = preciosPersonalizados.ContainsKey(a.ArancelId) ? preciosPersonalizados[a.ArancelId] : (decimal?)null,
                    estaVencido = a.EstaVencido,
                    valorMora = a.ValorMora,
                    ciclo = a.Ciclo != null ? $"Ciclo {a.Ciclo.NCiclo} - {a.Ciclo.anio}" : "Sin ciclo"
                }).ToList()
            });
        }

        private async Task CargarDatosParaVista()
        {
            // Cargar alumnos activos con información completa
            var alumnos = await _context.Alumno
                .Where(a => a.Estado == 1)
                .OrderBy(a => a.Apellidos)
                .ThenBy(a => a.Nombres)
                .ToListAsync();

            // Crear SelectList con nombre completo y carnet
            var alumnosSelectList = alumnos.Select(a => new
            {
                AlumnoId = a.AlumnoId,
                Text = $"{a.Apellidos}, {a.Nombres} - {a.Carnet}",
                NombreCompleto = $"{a.Nombres} {a.Apellidos}",
                Carnet = a.Carnet
            }).ToList();

            Alumnos = new SelectList(alumnosSelectList, "AlumnoId", "Text");

            // Cargar aranceles activos
            Aranceles = new SelectList(await _context.Aranceles
                .Where(a => a.Activo)
                .OrderBy(a => a.Nombre)
                .ToListAsync(), "ArancelId", "Nombre");

            // Cargar ciclos activos
            Ciclos = new SelectList(await _context.Ciclos
                .Where(c => c.Activo)
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .ToListAsync(), "Id", "NCiclo");

            // Cargar aranceles disponibles
            ArancelesDisponibles = await _context.Aranceles
                .Where(a => a.Activo)
                .Include(a => a.Ciclo)
                .OrderBy(a => a.Nombre)
                .ToListAsync();
        }
    }
} 