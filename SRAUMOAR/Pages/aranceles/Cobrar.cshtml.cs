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
        public Dictionary<int, decimal> PreciosPersonalizados { get; set; } = new Dictionary<int, decimal>();
        public Dictionary<int, decimal> PorcentajesDescuento { get; set; } = new Dictionary<int, decimal>();
        public bool AlumnoTieneBecaParcial { get; set; } = false;

        public async Task OnGetAsync(int? id)
        {
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);
            ViewData["Alumno"] = alumno;
            
            Ciclo cicloactual = await _context.Ciclos.Where(x => x.Activo).FirstAsync();

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

            // Obtener los IDs de los aranceles que ya pagó el alumno
            var arancelesPagados = await _context.CobrosArancel
                .Where(c => c.AlumnoId == id)
                .SelectMany(c => c.DetallesCobroArancel.DefaultIfEmpty())
                .Select(dc => dc.ArancelId)
                .ToListAsync();

            Arancel = await _context.Aranceles
                .Where(x => (x.Ciclo != null && x.Ciclo.Id == cicloactual.Id) || (!x.Obligatorio && x.Ciclo == null))
                .Include(a => a.Ciclo).ToListAsync();

            // Separar aranceles obligatorios y no obligatorios
            ArancelesObligatorios = Arancel.Where(a => a.Obligatorio).ToList();
            ArancelesNoObligatorios = Arancel.Where(a => !a.Obligatorio).ToList();
        }

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

        public bool AlumnoHaPagado(int arancelId, int alumnoId) {
            return _context.DetallesCobrosArancel
               .Any(d => d.ArancelId == arancelId &&
                         d.CobroArancel.AlumnoId == alumnoId);
        }

        public decimal ObtenerPrecioConDescuento(Arancel arancel, int alumnoId)
        {
            if (!AlumnoTieneBecaParcial)
                return arancel.Costo;

            if (PreciosPersonalizados.ContainsKey(arancel.ArancelId))
            {
                var precioPersonalizado = PreciosPersonalizados[arancel.ArancelId];
                return precioPersonalizado;
            }

            return arancel.Costo;
        }

        public decimal ObtenerPrecioConDescuentoYMora(Arancel arancel, int alumnoId)
        {
            var precioBase = ObtenerPrecioConDescuento(arancel, alumnoId);
            
            // Verificar si el alumno está exento de mora
            var alumno = _context.Alumno.FirstOrDefault(a => a.AlumnoId == alumnoId);
            if (alumno?.ExentoMora == true)
                return precioBase;

            // Aplicar mora si está vencido
            if (arancel.EstaVencido)
                return precioBase + arancel.ValorMora;

            return precioBase;
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
