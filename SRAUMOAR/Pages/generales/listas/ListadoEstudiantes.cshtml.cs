using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.listas
{
    public class ListadoEstudiantesModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public ListadoEstudiantesModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademicas { get; set; } = default!;
        public Grupo Grupo { get; set; } = default!;
        public string NombreMateria { get; set; } = default!;
        public bool lista { get; set; }
        public int idgrupo { get; set; } = default!;
        public async Task OnGetAsync(int id, bool lista = false)
        {
            this.lista = lista;
            var cicloactual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
            if (cicloactual == null)
            {
                ActividadAcademicas = new List<ActividadAcademica>();
                MateriasInscritas = new List<MateriasInscritas>();
                Grupo = new Grupo();
                return;
            }
            
            idgrupo = id;
            ActividadAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloactual.Id)
                .ToListAsync();
            NombreMateria = await ObtenerNombreMateriaAsync(id);
            ViewData["ActividadAcademicaId"] = new SelectList(_context.ActividadesAcademicas
     .Where(a => a.CicloId == cicloactual.Id && a.ActivarIngresoNotas == true)
     .Select(a => new
     {
         Id = a.ActividadAcademicaId,
         Descripcion = $"{a.Nombre} - {a.Fecha.ToShortDateString()}"
     }),
     "Id",
     "Descripcion"
 );
            // Validar que el MateriasGrupo pertenezca al ciclo actual
            var materiaGrupo = await _context.MateriasGrupo
                 .Include(g => g.Grupo)
                     .ThenInclude(g => g.Carrera)
                 .Include(g => g.Grupo)
                     .ThenInclude(g => g.Docente)
                 .Where(mg => mg.MateriasGrupoId == id && mg.Grupo.CicloId == cicloactual.Id)
                 .FirstOrDefaultAsync();
                 
            if (materiaGrupo == null)
            {
                Grupo = new Grupo();
                MateriasInscritas = new List<MateriasInscritas>();
                return;
            }
            
            Grupo = materiaGrupo.Grupo ?? new Grupo();

            MateriasInscritas = await _context.MateriasInscritas
                .Include(m => m.Alumno)
                .Include(m => m.MateriasGrupo)
                    .ThenInclude(mg => mg.Grupo)
                .Include(m => m.Notas)
                .ThenInclude(m => m.ActividadAcademica)
                .Where(m => m.MateriasGrupoId == id && 
                            m.MateriasGrupo.Grupo.CicloId == cicloactual.Id)
                .ToListAsync();


        }

        private async Task<string> ObtenerNombreMateriaAsync(int inscripcionMateriaId)
        {
            return await _context.MateriasGrupo
                .Include(im => im.Materia)
                .Where(im => im.MateriasGrupoId == inscripcionMateriaId)
                .Select(im => im.Materia.NombreMateria)
                .FirstOrDefaultAsync();
        }

      
       
    }
    }