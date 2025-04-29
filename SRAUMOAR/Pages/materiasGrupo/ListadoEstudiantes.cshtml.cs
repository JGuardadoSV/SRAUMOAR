using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
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
        public int idgrupo { get; set; } = default!;
        public async Task OnGetAsync(int id)
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).First();
            idgrupo = id;
            ActividadAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloactual.Id )
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
                .Include(m=> m.Notas)
                .ThenInclude(m=>m.ActividadAcademica)
                .Where(m => m.MateriasGrupoId == id)
                .ToListAsync();

            /*
            var resultados = _context.MateriasInscritas
     .Where(mi => mi.MateriasGrupoId == idgrupo)
     .Include(mi => mi.Alumno)
     .Include(mi => mi.Notas)
         .ThenInclude(n => n.ActividadAcademica)
     .Select(mi => new
     {
         AlumnoNombre = mi.Alumno.Nombres + " " + mi.Alumno.Apellidos,
            Alumnoid = mi.Alumno.AlumnoId,
         Promedio = mi.Notas.Where(n => n.ActividadAcademica.TipoActividad == 2).Sum(n => n.Nota * n.ActividadAcademica.Porcentaje / 100)
                     + mi.Notas.Where(n => n.ActividadAcademica.TipoActividad == 1).Sum(n => n.Nota * n.ActividadAcademica.Porcentaje / 100),
         Notas = mi.Notas.Select(n => new
         {
             n.Nota,
             n.ActividadAcademica.Nombre,
             n.ActividadAcademica.Porcentaje,
             n.ActividadAcademica.TipoActividad
         }).ToList()
     })
     .ToList();

            */ //CALCULO DE PROMEDIO
            /* para la vista
             @model IEnumerable<dynamic>

<table class="table">
    <thead>
        <tr>
            <th>Nombre del Alumno</th>
            <th>Promedio</th>
            <th>Notas</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in Model)
        {
            <tr>
                <td>@item.AlumnoNombre</td>
                <td>@item.Promedio</td>
                <td>
                    <ul>
                        @foreach (var nota in item.Notas)
                        {
                            <li>Nota: @nota.Nota, Actividad: @nota.Nombre, Porcentaje: @nota.Porcentaje%, Tipo: @nota.TipoActividad</li>
                        }
                    </ul>
                </td>
            </tr>
        }
    </tbody>
</table>

             
             */




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