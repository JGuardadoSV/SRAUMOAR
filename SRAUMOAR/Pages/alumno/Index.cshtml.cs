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
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.alumno
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly IAlumnoService _alumnoService;

        public IndexModel(IAlumnoService alumnoService)
        {
            _alumnoService = alumnoService;
        }

        public IList<Alumno> Alumno { get; set; } = default!;
        public string busqueda { get; set; }

        public async Task OnGetAsync()
        {
            Alumno = await _alumnoService.ObtenerAlumnosAsync();
        }
    }

}
