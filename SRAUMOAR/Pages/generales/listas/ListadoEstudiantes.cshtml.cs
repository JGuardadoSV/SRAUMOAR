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
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).First();
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
            Grupo = await _context.MateriasGrupo
                 .Include(g => g.Grupo)
                     .ThenInclude(g => g.Carrera)
                 .Include(g => g.Grupo)
                     .ThenInclude(g => g.Docente)
                 .Where(mg => mg.MateriasGrupoId == id)
                 .Select(mg => mg.Grupo)
                 .FirstOrDefaultAsync() ?? new Grupo();// Proporciona un valor por defecto

            MateriasInscritas = await _context.MateriasInscritas
                .Include(m => m.Alumno)
                .Include(m => m.MateriasGrupo)
                .Include(m => m.Notas)
                .ThenInclude(m => m.ActividadAcademica)
                .Where(m => m.MateriasGrupoId == id)
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