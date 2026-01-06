using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    [IgnoreAntiforgeryToken] // Deshabilitar completamente para toda la página
    public class CobrarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CobrarModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Arancel> Arancel { get; set; } = default!;
        public IList<Arancel> ArancelesObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesNoObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesEspecializacion { get; set; } = default!;
        public Dictionary<int, decimal> PreciosPersonalizados { get; set; } = new Dictionary<int, decimal>();
        public Dictionary<int, decimal> PorcentajesDescuento { get; set; } = new Dictionary<int, decimal>();
        public bool AlumnoTieneBecaParcial { get; set; } = false;

        public async Task<string> GenerarReporteDebug(int? alumnoId)
        {
            try
            {
                var reporte = new System.Text.StringBuilder();
                reporte.AppendLine($"=== REPORTE DE DEBUG - ALUMNO ID: {alumnoId} ===");
                reporte.AppendLine($"Fecha: {DateTime.Now}");
                reporte.AppendLine($"Usuario: {User?.Identity?.Name ?? "No autenticado"}");
                
                if (alumnoId.HasValue)
                {
                    // Verificar alumno
                    var alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.AlumnoId == alumnoId.Value);
                    if (alumno != null)
                    {
                        reporte.AppendLine($"Alumno encontrado: {alumno.Nombres} {alumno.Apellidos}");
                        reporte.AppendLine($"Estado: {alumno.Estado}");
                        reporte.AppendLine($"Carrera: {alumno.CarreraId}");
                    }
                    else
                    {
                        reporte.AppendLine("Alumno NO encontrado");
                    }

                    // Verificar ciclo activo
                    var cicloActivo = await _context.Ciclos.Where(x => x.Activo).FirstOrDefaultAsync();
                    if (cicloActivo != null)
                    {
                        reporte.AppendLine($"Ciclo activo: {cicloActivo.NCiclo} - {cicloActivo.anio}");
                    }
                    else
                    {
                        reporte.AppendLine("NO hay ciclo activo");
                    }

                    // Verificar aranceles
                    var totalAranceles = await _context.Aranceles.CountAsync();
                    reporte.AppendLine($"Total de aranceles en BD: {totalAranceles}");

                    if (cicloActivo != null)
                    {
                        var arancelesCiclo = await _context.Aranceles
                            .Where(x => x.Ciclo != null && x.Ciclo.Id == cicloActivo.Id)
                            .CountAsync();
                        reporte.AppendLine($"Aranceles del ciclo activo: {arancelesCiclo}");
                    }

                    // Verificar cobros existentes
                    var cobrosExistentes = await _context.CobrosArancel
                        .Where(c => c.AlumnoId == alumnoId.Value)
                        .CountAsync();
                    reporte.AppendLine($"Cobros existentes del alumno: {cobrosExistentes}");
                }

                reporte.AppendLine("=== FIN DEL REPORTE ===");
                return reporte.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generando reporte: {ex.Message}";
            }
        }

        public async Task<bool> VerificarIntegridadRelaciones(int alumnoId)
        {
            try
            {
                // Verificar que el alumno tiene las relaciones básicas
                var alumno = await _context.Alumno
                    .Include(a => a.Carrera)
                    .Include(a => a.Municipio)
                    .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId);

                if (alumno == null)
                {
                    System.Diagnostics.Debug.WriteLine($"VerificarIntegridadRelaciones: Alumno no encontrado: {alumnoId}");
                    return false;
                }

                // Verificar que hay un ciclo activo
                var cicloActivo = await _context.Ciclos.Where(x => x.Activo).FirstOrDefaultAsync();
                if (cicloActivo == null)
                {
                    System.Diagnostics.Debug.WriteLine("VerificarIntegridadRelaciones: No hay ciclo activo");
                    return false;
                }

                // Verificar que hay aranceles disponibles
                var arancelesDisponibles = await _context.Aranceles
                    .Where(x => (x.Ciclo != null && x.Ciclo.Id == cicloActivo.Id) || (!x.Obligatorio && x.Ciclo == null))
                    .CountAsync();

                if (arancelesDisponibles == 0)
                {
                    System.Diagnostics.Debug.WriteLine("VerificarIntegridadRelaciones: No hay aranceles disponibles");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"VerificarIntegridadRelaciones: Integridad verificada para alumno {alumnoId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en VerificarIntegridadRelaciones: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ValidarAlumno(int? alumnoId)
        {
            try
            {
                if (alumnoId == null || alumnoId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ValidarAlumno: ID de alumno inválido: {alumnoId}");
                    return false;
                }

                var alumno = await _context.Alumno
                    .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId && a.Estado == 1);
                
                if (alumno == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ValidarAlumno: Alumno no encontrado o inactivo: {alumnoId}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"ValidarAlumno: Alumno válido encontrado: {alumno.Nombres} {alumno.Apellidos} (ID: {alumnoId})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ValidarAlumno: {ex.Message}");
                return false;
            }
        }

        public async Task OnGetAsync(int? id)
        {
            try
            {
                // Validar que el alumno existe y está activo
                if (!await ValidarAlumno(id))
                {
                    throw new ArgumentException($"No se pudo validar el alumno con ID: {id}");
                }

                // Verificar integridad de las relaciones
                if (!await VerificarIntegridadRelaciones(id.Value))
                {
                    throw new InvalidOperationException($"Problemas de integridad en las relaciones para el alumno con ID: {id}");
                }

                var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);
                if (alumno == null)
                {
                    throw new ArgumentException($"No se encontró el alumno con ID: {id}");
                }
                
                ViewData["Alumno"] = alumno;
                
                Ciclo cicloactual = await _context.Ciclos.Where(x => x.Activo).FirstAsync();
                if (cicloactual == null)
                {
                    throw new InvalidOperationException("No hay un ciclo activo en el sistema");
                }

                // Verificar si el alumno tiene beca parcial
                var becaAlumno = await _context.Becados
                    .Include(b => b.Alumno)
                    .Where(b => b.AlumnoId == id && b.Estado && b.TipoBeca == 2) // TipoBeca = 2 es parcial
                    .FirstOrDefaultAsync();

                AlumnoTieneBecaParcial = becaAlumno != null;

                // Obtener aranceles personalizados si el alumno tiene beca parcial
                if (AlumnoTieneBecaParcial)
                {
                    var arancelesPersonalizados = await _context.ArancelesBecados
                        .Where(ab => ab.BecadosId == becaAlumno.BecadosId && ab.Activo)
                        .Include(ab => ab.Arancel)
                        .ToListAsync();

                    foreach (var arancelPersonalizado in arancelesPersonalizados)
                    {
                        PreciosPersonalizados[arancelPersonalizado.ArancelId] = arancelPersonalizado.PrecioPersonalizado;
                        PorcentajesDescuento[arancelPersonalizado.ArancelId] = arancelPersonalizado.PorcentajeDescuento;
                    }
                }

                // Verificar si el alumno está inscrito en algún grupo de especialización del ciclo actual
                var alumnoEnGrupoEspecializacion = await _context.MateriasInscritas
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Grupo)
                    .Where(mi => mi.AlumnoId == id && 
                                 mi.MateriasGrupo.Grupo.CicloId == cicloactual.Id &&
                                 mi.MateriasGrupo.Grupo.EsEspecializacion)
                    .AnyAsync();

                // Obtener los IDs de los aranceles que ya pagó el alumno
                var arancelesPagados = await _context.CobrosArancel
                    .Where(c => c.AlumnoId == id)
                    .SelectMany(c => c.DetallesCobroArancel.DefaultIfEmpty())
                    .Select(dc => dc.ArancelId)
                    .ToListAsync();

                Arancel = await _context.Aranceles
                    .Where(x => (x.Ciclo != null && x.Ciclo.Id == cicloactual.Id) || (!x.Obligatorio && x.Ciclo == null))
                    .Include(a => a.Ciclo).ToListAsync();

                // Separar aranceles en tres categorías:
                // 1. Aranceles obligatorios normales (no de especialización)
                // 2. Aranceles de especialización (obligatorios o no obligatorios)
                // 3. Otros aranceles no obligatorios (no de especialización)
                
                if (alumnoEnGrupoEspecializacion)
                {
                    // Si el estudiante está en un grupo de especialización:
                    // - Solo mostrar aranceles obligatorios que sean de especialización
                    // - Mostrar todos los aranceles no obligatorios
                    ArancelesObligatorios = Arancel.Where(a => a.Obligatorio && a.EsEspecializacion).ToList();
                    ArancelesNoObligatorios = Arancel.Where(a => !a.Obligatorio && !a.EsEspecializacion).ToList();
                    ArancelesEspecializacion = Arancel.Where(a => a.EsEspecializacion).ToList();
                }
                else
                {
                    // Comportamiento normal: separar por tipo
                    ArancelesObligatorios = Arancel.Where(a => a.Obligatorio && !a.EsEspecializacion).ToList();
                    ArancelesNoObligatorios = Arancel.Where(a => !a.Obligatorio && !a.EsEspecializacion).ToList();
                    // Mostrar aranceles de especialización siempre disponibles para selección manual
                    ArancelesEspecializacion = Arancel.Where(a => a.EsEspecializacion).ToList();
                }
            }
            catch (Exception ex)
            {
                // Generar reporte de debug
                var reporteDebug = await GenerarReporteDebug(id);
                System.Diagnostics.Debug.WriteLine(reporteDebug);
                
                // Log del error para debugging
                System.Diagnostics.Debug.WriteLine($"Error en OnGetAsync para alumno ID {id}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                
                // Agregar error al ModelState para mostrar al usuario
                ModelState.AddModelError(string.Empty, $"Error al cargar los datos del alumno: {ex.Message}");
                
                // Inicializar las propiedades para evitar errores de null reference
                Arancel = new List<Arancel>();
                ArancelesObligatorios = new List<Arancel>();
                ArancelesNoObligatorios = new List<Arancel>();
                ArancelesEspecializacion = new List<Arancel>();
                PreciosPersonalizados = new Dictionary<int, decimal>();
                PorcentajesDescuento = new Dictionary<int, decimal>();
                AlumnoTieneBecaParcial = false;
            }
        }

        [IgnoreAntiforgeryToken]
        public IActionResult OnPost(List<int> selectedAranceles, int alumnoId)
        {
            if (selectedAranceles == null || !selectedAranceles.Any())
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar al menos un arancel.");
                return Page();
            }

            // Redirigir a la página de cobro con los IDs de los aranceles seleccionados
            return RedirectToPage("./Facturar", new { arancelIds = string.Join(",", selectedAranceles),idalumno=alumnoId });
        }

        public async Task<IActionResult> OnPostEliminarPago(int id, int? arancelId, int? alumnoId)
        {
            System.Diagnostics.Debug.WriteLine($"Handler ejecutado: id={id}, arancelId={arancelId}, alumnoId={alumnoId}");

            if (arancelId == null || alumnoId == null)
            {
                ModelState.AddModelError(string.Empty, "Faltan parámetros requeridos para eliminar el pago.");
                await OnGetAsync(id); // Usar el id de la ruta
                return Page();
            }

            // Buscar el detalle de cobro correspondiente
            var detalle = await _context.DetallesCobrosArancel
                .Include(d => d.CobroArancel)
                .FirstOrDefaultAsync(d => d.ArancelId == arancelId.Value && d.CobroArancel.AlumnoId == alumnoId.Value);

            if (detalle != null)
            {
                // Eliminar el detalle
                _context.DetallesCobrosArancel.Remove(detalle);

                // Si el cobro no tiene más detalles, eliminar el cobro principal
                var cobro = detalle.CobroArancel;
                var otrosDetalles = _context.DetallesCobrosArancel
                    .Where(d => d.CobroArancelId == cobro.CobroArancelId && d.ArancelId != arancelId.Value)
                    .ToList();

                if (!otrosDetalles.Any())
                {
                    _context.CobrosArancel.Remove(cobro);
                }

                await _context.SaveChangesAsync();
            }

            // Redirigir de nuevo a la página de cobro
            return RedirectToPage(new { id = alumnoId.Value });
        }

        public bool AlumnoHaPagado(int arancelId, int alumnoId) {
            try
            {
                if (arancelId <= 0 || alumnoId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"AlumnoHaPagado: Parámetros inválidos - arancelId: {arancelId}, alumnoId: {alumnoId}");
                    return false;
                }

                var resultado = _context.DetallesCobrosArancel
                   .Any(d => d.ArancelId == arancelId &&
                             d.CobroArancel.AlumnoId == alumnoId);
                
                System.Diagnostics.Debug.WriteLine($"AlumnoHaPagado: arancelId {arancelId}, alumnoId {alumnoId} - Resultado: {resultado}");
                return resultado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en AlumnoHaPagado: {ex.Message}");
                return false;
            }
        }

        public decimal ObtenerPrecioConDescuento(Arancel arancel, int alumnoId)
        {
            try
            {
                if (arancel == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ObtenerPrecioConDescuento: Arancel es null para alumnoId: {alumnoId}");
                    return 0;
                }

                if (!AlumnoTieneBecaParcial)
                    return arancel.Costo;

                if (PreciosPersonalizados.ContainsKey(arancel.ArancelId))
                {
                    var precioPersonalizado = PreciosPersonalizados[arancel.ArancelId];
                    System.Diagnostics.Debug.WriteLine($"ObtenerPrecioConDescuento: Precio personalizado para arancel {arancel.ArancelId}: {precioPersonalizado}");
                    return precioPersonalizado;
                }

                return arancel.Costo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerPrecioConDescuento: {ex.Message}");
                return arancel?.Costo ?? 0;
            }
        }

        public decimal ObtenerPrecioConDescuentoYMora(Arancel arancel, int alumnoId)
        {
            try
            {
                if (arancel == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ObtenerPrecioConDescuentoYMora: Arancel es null para alumnoId: {alumnoId}");
                    return 0;
                }

                var precioBase = ObtenerPrecioConDescuento(arancel, alumnoId);
                
                // Verificar si el alumno está exento de mora
                var alumno = _context.Alumno.FirstOrDefault(a => a.AlumnoId == alumnoId);
                if (alumno?.ExentoMora == true)
                {
                    System.Diagnostics.Debug.WriteLine($"ObtenerPrecioConDescuentoYMora: Alumno {alumnoId} exento de mora");
                    return precioBase;
                }

                // Aplicar mora si está vencido
                if (arancel.EstaVencido)
                {
                    var precioConMora = precioBase + arancel.ValorMora;
                    System.Diagnostics.Debug.WriteLine($"ObtenerPrecioConDescuentoYMora: Arancel {arancel.ArancelId} vencido, precio con mora: {precioConMora}");
                    return precioConMora;
                }

                return precioBase;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerPrecioConDescuentoYMora: {ex.Message}");
                return arancel?.Costo ?? 0;
            }
        }

        public decimal ObtenerPorcentajeDescuento(int arancelId)
        {
            return PorcentajesDescuento.ContainsKey(arancelId) ? PorcentajesDescuento[arancelId] : 0;
        }

        public bool TienePrecioPersonalizado(int arancelId)
        {
            return PreciosPersonalizados.ContainsKey(arancelId);
        }

        public int CantidadArancelesPersonalizados()
        {
            return PreciosPersonalizados.Count;
        }
    }
}
