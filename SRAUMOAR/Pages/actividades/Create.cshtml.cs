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

            var arancelesQuery = _context.Aranceles.AsNoTracking();
            if (cicloActivo != null)
            {
                // Solo aranceles del ciclo activo, y que sean los que aplican a cobros academicos.
                // (Se excluyen constancias y otros aranceles no obligatorios del ciclo.)
                arancelesQuery = arancelesQuery.Where(a => a.CicloId == cicloActivo.Id);
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

            ViewData["ArancelId"] = new SelectList(aranceles, "ArancelId", "Descripcion");
            ViewData["CicloId"] = new SelectList(
                _context.Ciclos.Where(c => c.Activo == true)
                    .Select(c => new
                    {
                        c.Id,
                        Descripcion = $"Ciclo {c.NCiclo} - {c.anio}"
                    }),
                "Id",
                "Descripcion"
            );
            return Page();
        }

        [BindProperty]
        public ActividadAcademica ActividadAcademica { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
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
