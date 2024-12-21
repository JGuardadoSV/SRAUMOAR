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
    [Authorize(Roles = "Administrador,Administracion")]
    public class BusquedaModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public BusquedaModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Alumno> Alumno { get; set; } = default!;
        public string busqueda;
        public async Task OnGetAsync()
        {
            this.busqueda = Request.Query["buscar"];
            Alumno = await _context.Alumno.Where(a => a.Nombres.Contains(busqueda) || a.Apellidos.Contains(busqueda) || a.TelefonoPrimario.Contains(busqueda)).ToListAsync();
        }
    }
}
