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
        public int? SelectedGrupoId { get; set; }
        public async Task OnGetAsync()
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            ViewData["GrupoId"] = new SelectList(
              _context.Grupo
              .Where(x => x.CicloId== cicloactual)
              .Include(c => c.Carrera)
              .Select(c => new
              {
                  Id = c.GrupoId,
                  Grupo = c.Carrera.NombreCarrera + " - " + c.Nombre
              }), "Id", "Grupo",SelectedGrupoId);
            if (SelectedGrupoId.HasValue)
            {

                Inscripcion = await _context.Inscripciones
                .Include(i => i.Alumno)
                .Include(i => i.Grupro).Where(x=>x.Grupro.CicloId==cicloactual && x.Grupro.GrupoId==SelectedGrupoId).ToListAsync()?? new List<Inscripcion>();

                ViewData["TotalAlumnosInscritos"] = Inscripcion.Count;
               
                
            }
            else
            {
                Inscripcion= new List<Inscripcion>();
            }

            ViewData["TotalInscripciones"] = _context.Inscripciones.Where(x => x.Grupro.CicloId == cicloactual).Count();
        }
    }
}
