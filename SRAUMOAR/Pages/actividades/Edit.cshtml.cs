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
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public ActividadAcademica ActividadAcademica { get; set; } = default!;

        private void CargarListas(int cicloId, int arancelGeneralSeleccionadoId, int? arancelEspecializacionSeleccionadoId)
        {
            ViewData["CicloId"] = new SelectList(
                _context.Ciclos
                    .AsNoTracking()
                    .Where(c => c.Activo == true || c.Id == cicloId)
                    .Select(c => new
                    {
                        c.Id,
                        Descripcion = $"Ciclo {c.NCiclo} - {c.anio}"
                    }),
                "Id",
                "Descripcion",
                cicloId
            );

            // Mantener el arancel requerido tal cual (obligatorios o de especializacion)
            var arancelesRequeridos = _context.Aranceles
                .AsNoTracking()
                .Where(a =>
                    // incluir el seleccionado aunque este inactivo/otro ciclo para no romper el edit
                    (a.ArancelId == arancelGeneralSeleccionadoId || a.Activo) &&
                    // Solo aranceles del ciclo de la actividad (y el seleccionado)
                    (a.ArancelId == arancelGeneralSeleccionadoId || a.CicloId == cicloId) &&
                    // Solo obligatorios o de especializacion (y el seleccionado)
                    (a.ArancelId == arancelGeneralSeleccionadoId || a.Obligatorio || a.EsEspecializacion))
                .OrderBy(a => a.EsEspecializacion)
                .ThenBy(a => a.Nombre)
                .Select(a => new
                {
                    a.ArancelId,
                    Descripcion = $"{a.Nombre} {(a.EsEspecializacion ? "(Especializacion)" : "(General)")}" 
                })
                .ToList();

            var arancelesEspecializacion = _context.Aranceles
                .AsNoTracking()
                .Where(a =>
                    // incluir el seleccionado aunque este inactivo/otro ciclo para no romper el edit
                    (arancelEspecializacionSeleccionadoId.HasValue && a.ArancelId == arancelEspecializacionSeleccionadoId.Value) ||
                    (a.Activo && a.CicloId == cicloId && a.EsEspecializacion))
                .OrderBy(a => a.Nombre)
                .Select(a => new { a.ArancelId, a.Nombre })
                .ToList();

            ViewData["ArancelId"] = new SelectList(arancelesRequeridos, "ArancelId", "Descripcion", arancelGeneralSeleccionadoId);
            ViewData["ArancelEspecializacionId"] = new SelectList(arancelesEspecializacion, "ArancelId", "Nombre", arancelEspecializacionSeleccionadoId);
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actividadacademica =  await _context.ActividadesAcademicas.FirstOrDefaultAsync(m => m.ActividadAcademicaId == id);
            if (actividadacademica == null)
            {
                return NotFound();
            }
            ActividadAcademica = actividadacademica;

            CargarListas(
                cicloId: actividadacademica.CicloId,
                arancelGeneralSeleccionadoId: actividadacademica.ArancelId,
                arancelEspecializacionSeleccionadoId: actividadacademica.ArancelEspecializacionId
            );
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CargarListas(
                    cicloId: ActividadAcademica.CicloId,
                    arancelGeneralSeleccionadoId: ActividadAcademica.ArancelId,
                    arancelEspecializacionSeleccionadoId: ActividadAcademica.ArancelEspecializacionId
                );
                return Page();
            }

            _context.Attach(ActividadAcademica).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ActividadAcademicaExists(ActividadAcademica.ActividadAcademicaId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ActividadAcademicaExists(int id)
        {
            return _context.ActividadesAcademicas.Any(e => e.ActividadAcademicaId == id);
        }
    }
}
