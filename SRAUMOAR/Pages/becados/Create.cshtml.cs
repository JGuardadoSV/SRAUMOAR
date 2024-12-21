using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
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
            //incluir nombre y apellidos de alumnos en el selectlist

            

        ViewData["AlumnoId"] = new SelectList(_context.Alumno.Select(a=> new {AlumnoId=a.AlumnoId,Apellidos=a.Nombres+" "+a.Apellidos  }), "AlumnoId", "Apellidos");

        ViewData["CicloId"] = new SelectList(_context.Ciclos.Where(x=>x.Activo==true).Select(a=> new {Id=a.Id,Ciclo=a.NCiclo+"/"+a.anio}), "Id", "Ciclo");
        ViewData["EntidadBecaId"] = new SelectList(_context.InstitucionesBeca, "EntidadBecaId", "Nombre");
            return Page();
        }

        [BindProperty]
        public Becados Becados { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Becados.Add(Becados);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
