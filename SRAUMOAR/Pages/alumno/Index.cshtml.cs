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

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int PageSize { get; set; } = 10;
        
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public async Task OnGetAsync()
        {
            TotalItems = await _alumnoService.ObtenerTotalAlumnosAsync();
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);

            Alumno = await _alumnoService.ObtenerAlumnosPaginadosAsync(PageNumber, PageSize);
        }
    }
}
