using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Migrations;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.inscripcion
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Inscripcion> Inscripcion { get;set; } = default!;


        [BindProperty(SupportsGet = true)]
        public int? SelectedCarreraId { get; set; }
        public async Task OnGetAsync()
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            
            // Solo cargar datos si se seleccionó una carrera válida (no 0)
            if (SelectedCarreraId.HasValue && SelectedCarreraId.Value > 0)
            {
                Inscripcion = await _context.Inscripciones
                .Include(i => i.Alumno)
                .Include(i => i.Ciclo).Where(x=>x.CicloId==cicloactual && x.Alumno.CarreraId==SelectedCarreraId).ToListAsync()?? new List<Inscripcion>();

                ViewData["TotalAlumnosInscritos"] = Inscripcion.Count;
            }
            else
            {
                Inscripcion = new List<Inscripcion>();
                ViewData["TotalAlumnosInscritos"] = 0;
            }

            ViewData["GrupoId"] = new SelectList(
              _context.Carreras
              .Select(c => new
              {
                  Id = c.CarreraId,
                  Grupo = c.NombreCarrera 
              }), "Id", "Grupo", SelectedCarreraId);

            ViewData["TotalInscripciones"] = _context.Inscripciones.Where(x => x.CicloId == cicloactual).Count();
        }
    }
}
