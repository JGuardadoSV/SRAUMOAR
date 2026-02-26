using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.alumno
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class busquedaAjaxModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public busquedaAjaxModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Alumno> Alumno { get;set; } = default!;

        public async Task<IActionResult> OnGetSearch(string term)

        {
            var students = await _context.Alumno
            .Where(s => s.Nombres.Contains(term))
            .Select(s => new
            {
                id = s.AlumnoId,
                name = s.Nombres,
                photoUrl = s.Foto
            })
            .Take(10)
            .ToListAsync();

            return new JsonResult(students);
        }
    }
}

