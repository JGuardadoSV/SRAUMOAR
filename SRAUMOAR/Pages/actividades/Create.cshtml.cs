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

namespace SRAUMOAR.Pages.actividades
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            var cicloActivo = _context.Ciclos.FirstOrDefault(c => c.Activo == true);
            CargarListas(
                cicloId: cicloActivo?.Id,
                arancelGeneralSeleccionadoId: null,
                arancelEspecializacionSeleccionadoId: null,
                soloCiclosActivos: true
            );
            return Page();
        }

        private void CargarListas(int? cicloId, int? arancelGeneralSeleccionadoId, int? arancelEspecializacionSeleccionadoId, bool soloCiclosActivos)
        {
            var ciclosQuery = _context.Ciclos.AsNoTracking();
            if (soloCiclosActivos)
            {
                ciclosQuery = ciclosQuery.Where(c => c.Activo == true);
            }
            else if (cicloId.HasValue)
            {
                ciclosQuery = ciclosQuery.Where(c => c.Activo == true || c.Id == cicloId.Value);
            }

            ViewData["CicloId"] = new SelectList(
                ciclosQuery
                    .Select(c => new
                    {
                        c.Id,
                        Descripcion = $"Ciclo {c.NCiclo} - {c.anio}"
                    }),
                "Id",
                "Descripcion",
                cicloId
            );

            // Mantener el arancel actual tal cual (incluye obligatorios y especializacion)
            var arancelesQuery = _context.Aranceles.AsNoTracking();
            if (cicloId.HasValue)
            {
                // Solo aranceles del ciclo seleccionado.
                arancelesQuery = arancelesQuery.Where(a => a.CicloId == cicloId.Value);
            }

            var aranceles = arancelesQuery
                .Where(a => a.Activo && (a.Obligatorio || a.EsEspecializacion))
                .OrderBy(a => a.EsEspecializacion)
                .ThenBy(a => a.Nombre)
                .Select(a => new
                {
                    a.ArancelId,
                    Descripcion = $"{a.Nombre} {(a.EsEspecializacion ? "(Especializacion)" : "(General)")}" 
                })
                .ToList();

            var arancelesEspecializacion = arancelesQuery
                .Where(a => a.Activo && a.EsEspecializacion)
                .OrderBy(a => a.Nombre)
                .Select(a => new { a.ArancelId, a.Nombre })
                .ToList();

            ViewData["ArancelId"] = new SelectList(aranceles, "ArancelId", "Descripcion", arancelGeneralSeleccionadoId);
            ViewData["ArancelEspecializacionId"] = new SelectList(arancelesEspecializacion, "ArancelId", "Nombre", arancelEspecializacionSeleccionadoId);
        }

        [BindProperty]
        public ActividadAcademica ActividadAcademica { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CargarListas(
                    cicloId: ActividadAcademica?.CicloId,
                    arancelGeneralSeleccionadoId: ActividadAcademica?.ArancelId,
                    arancelEspecializacionSeleccionadoId: ActividadAcademica?.ArancelEspecializacionId,
                    soloCiclosActivos: true
                );
                return Page();
            }
            ActividadAcademica.ActivarIngresoNotas = false;
            ActividadAcademica.Fecha = DateTime.Now;
            _context.ActividadesAcademicas.Add(ActividadAcademica);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
