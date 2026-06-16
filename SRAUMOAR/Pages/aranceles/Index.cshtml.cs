using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Arancel> ArancelesObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesNoObligatorios { get; set; } = default!;
        public IList<Arancel> ArancelesEspecializacion { get; set; } = default!;
        public IList<Arancel> Arancel { get; set; } = default!;
        public Ciclo? CicloSeleccionado { get; set; }
        public int? CicloIdSeleccionado { get; set; }
        public SelectList CiclosDisponibles { get; set; } = default!;
        public SelectList CiclosOrigenDisponibles { get; set; } = default!;
        public Ciclo? CicloOrigenSeleccionado { get; set; }
        public IList<Arancel> ArancelesOrigen { get; set; } = new List<Arancel>();

        public async Task OnGetAsync(int? cicloId = null, int? cicloOrigenId = null)
        {
            CicloSeleccionado = cicloId.HasValue
                ? await _context.Ciclos.FirstOrDefaultAsync(x => x.Id == cicloId.Value)
                : await _context.Ciclos.FirstOrDefaultAsync(x => x.Activo);

            if (CicloSeleccionado == null)
            {
                ArancelesObligatorios = new List<Arancel>();
                ArancelesNoObligatorios = new List<Arancel>();
                ArancelesEspecializacion = new List<Arancel>();
                Arancel = new List<Arancel>();
                CargarCiclosDisponibles(null);
                CargarCiclosOrigenDisponibles(null, null);
                return;
            }

            CicloIdSeleccionado = CicloSeleccionado.Id;
            CargarCiclosDisponibles(CicloSeleccionado.Id);
            await CargarArancelesOrigenAsync(CicloSeleccionado.Id, cicloOrigenId);

            var todosLosAranceles = await _context.Aranceles
                .Where(x => x.Activo && ((x.CicloId != null && x.CicloId == CicloSeleccionado.Id) || (!x.Obligatorio && x.CicloId == null)))
                .Include(a => a.Ciclo)
                .ToListAsync();

            ArancelesObligatorios = todosLosAranceles.Where(a => a.Obligatorio && !a.EsEspecializacion).ToList();
            ArancelesNoObligatorios = todosLosAranceles.Where(a => !a.Obligatorio && !a.EsEspecializacion).ToList();
            ArancelesEspecializacion = todosLosAranceles.Where(a => a.EsEspecializacion).ToList();
            Arancel = todosLosAranceles;
        }

        public async Task<IActionResult> OnPostCopiarArancelesAsync(int cicloDestinoId, int cicloOrigenId, List<int> arancelIds)
        {
            var cicloDestinoExiste = await _context.Ciclos.AnyAsync(x => x.Id == cicloDestinoId);
            var cicloOrigenExiste = await _context.Ciclos.AnyAsync(x => x.Id == cicloOrigenId);
            if (!cicloDestinoExiste || !cicloOrigenExiste || cicloDestinoId == cicloOrigenId)
            {
                TempData["Error"] = "Seleccione ciclos validos para copiar aranceles.";
                return RedirectToPage(new { cicloId = cicloDestinoId });
            }

            if (arancelIds == null || !arancelIds.Any())
            {
                TempData["Error"] = "Seleccione al menos un arancel para copiar.";
                return RedirectToPage(new { cicloId = cicloDestinoId, cicloOrigenId });
            }

            var arancelesOrigen = await _context.Aranceles
                .Where(a => a.CicloId == cicloOrigenId && arancelIds.Contains(a.ArancelId))
                .ToListAsync();

            var arancelesDestino = await _context.Aranceles
                .Where(a => a.CicloId == cicloDestinoId)
                .Select(a => new { a.Nombre, a.Obligatorio, a.EsEspecializacion })
                .ToListAsync();

            var copiados = 0;
            var omitidos = 0;

            foreach (var arancel in arancelesOrigen)
            {
                var yaExiste = arancelesDestino.Any(a =>
                    a.Nombre == arancel.Nombre &&
                    a.Obligatorio == arancel.Obligatorio &&
                    a.EsEspecializacion == arancel.EsEspecializacion);

                if (yaExiste)
                {
                    omitidos++;
                    continue;
                }

                _context.Aranceles.Add(new Arancel
                {
                    Nombre = arancel.Nombre,
                    Costo = arancel.Costo,
                    Activo = arancel.Activo,
                    FechaInicio = arancel.FechaInicio,
                    FechaFin = arancel.FechaFin,
                    CicloId = cicloDestinoId,
                    Exento = arancel.Exento,
                    Obligatorio = arancel.Obligatorio,
                    EsEspecializacion = arancel.EsEspecializacion,
                    ValorMora = arancel.ValorMora
                });

                arancelesDestino.Add(new
                {
                    arancel.Nombre,
                    arancel.Obligatorio,
                    arancel.EsEspecializacion
                });
                copiados++;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Se copiaron {copiados} arancel(es). Se omitieron {omitidos} por duplicado(s).";
            return RedirectToPage(new { cicloId = cicloDestinoId });
        }

        private void CargarCiclosDisponibles(int? selectedValue)
        {
            CiclosDisponibles = new SelectList(
                _context.Ciclos
                    .OrderByDescending(x => x.Activo)
                    .ThenByDescending(x => x.anio)
                    .ThenByDescending(x => x.NCiclo)
                    .Select(x => new
                    {
                        x.Id,
                        NombreCiclo = x.NCiclo + " - " + x.anio + (x.Activo ? " (Activo)" : " (En preparacion/Inactivo)")
                    }),
                "Id",
                "NombreCiclo",
                selectedValue
            );
        }

        private async Task CargarArancelesOrigenAsync(int cicloDestinoId, int? cicloOrigenId)
        {
            CargarCiclosOrigenDisponibles(cicloDestinoId, cicloOrigenId);

            if (!cicloOrigenId.HasValue || cicloOrigenId.Value == cicloDestinoId)
            {
                return;
            }

            CicloOrigenSeleccionado = await _context.Ciclos.FirstOrDefaultAsync(x => x.Id == cicloOrigenId.Value);
            if (CicloOrigenSeleccionado == null)
            {
                return;
            }

            ArancelesOrigen = await _context.Aranceles
                .Where(a => a.CicloId == cicloOrigenId.Value)
                .Include(a => a.Ciclo)
                .OrderByDescending(a => a.Obligatorio)
                .ThenByDescending(a => a.EsEspecializacion)
                .ThenBy(a => a.Nombre)
                .ToListAsync();
        }

        private void CargarCiclosOrigenDisponibles(int? cicloDestinoId, int? selectedValue)
        {
            CiclosOrigenDisponibles = new SelectList(
                _context.Ciclos
                    .Where(x => !cicloDestinoId.HasValue || x.Id != cicloDestinoId.Value)
                    .OrderByDescending(x => x.Activo)
                    .ThenByDescending(x => x.anio)
                    .ThenByDescending(x => x.NCiclo)
                    .Select(x => new
                    {
                        x.Id,
                        NombreCiclo = x.NCiclo + " - " + x.anio + (x.Activo ? " (Activo)" : " (En preparacion/Inactivo)")
                    }),
                "Id",
                "NombreCiclo",
                selectedValue
            );
        }
    }
}
