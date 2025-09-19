using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
{
    public class IngresoDeNotasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IngresoDeNotasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        
        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        [BindProperty]
        public IList<IngresoNotasView> IngresoNotasView { get; set; } = default!;
        [BindProperty]
        public int idgrupo { get; set; } = default!;
        public Grupo Grupo { get; set; } = default!;
        public string NombreMateria { get; set; } = default!;
        public string Actividad { get; set; } = "";
        public bool EsEdicion { get; set; } = false;
        public IActionResult OnGet(int idgrupo, string materia, int actividadid)
        {


            //averiguar si el grupo ya tiene nota para la actividad
            var notas = _context.Notas
                .Where(x => x.MateriasInscritas.MateriasGrupoId == idgrupo && x.ActividadAcademicaId == actividadid).ToList();

            




            if (notas.Count>0)
            {
                this.EsEdicion = true;
            }
            this.idgrupo = idgrupo;
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).First();
            NombreMateria = materia;
            Actividad = _context.ActividadesAcademicas.Where(x => x.ActividadAcademicaId == actividadid).First().Nombre;

            Grupo = _context.MateriasInscritas
                        .Include(mi => mi.MateriasGrupo)
                            .ThenInclude(g => g.Grupo)
                            .ThenInclude(mi => mi.Carrera)
                        .Include(mi => mi.MateriasGrupo)
                            .ThenInclude(g => g.Grupo)
                            .ThenInclude(mi => mi.Docente)
                        .Where(mi => mi.MateriasGrupoId == idgrupo)
                        .Select(mi => mi.MateriasGrupo.Grupo)
                        .FirstOrDefault() ?? new Grupo(); // Proporciona un valor por defecto

            var docenteInfo = _context.MateriasGrupo
                                .Where(mi => mi.MateriasGrupoId == idgrupo)
                                .Select(mi => new
                                {
                                    NombreCompleto = $"{mi.Docente.Nombres} {mi.Docente.Apellidos}"
                                })
                                .FirstOrDefault();

            ViewData["Docente"] = docenteInfo.NombreCompleto;

            MateriasInscritas = _context.MateriasInscritas
                                .Include(m => m.Alumno)
                                .Include(m => m.MateriasGrupo)
                                .Where(m => m.MateriasGrupoId == idgrupo)
                                .ToList();

            IngresoNotasView = new List<IngresoNotasView>();

            foreach (var item in MateriasInscritas)
            {
                var alumno = new IngresoNotasView
                {
                    idincripcion = item.MateriasInscritasId,
                    nombre = item.Alumno.Nombres + " " + item.Alumno.Apellidos,
                    actividadid = actividadid,
                    nota = notas.Where(x => x.MateriasInscritasId == item.MateriasInscritasId).FirstOrDefault()?.Nota ?? 0.0m

                };
                IngresoNotasView.Add(alumno);
            }

            return Page();
        }


        [BindProperty]
        public Notas Notas { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            foreach (var item in IngresoNotasView)
            {
                // Buscar si ya existe una nota para este alumno y actividad
                var notaExistente = _context.Notas
                    .FirstOrDefault(n => n.MateriasInscritasId == item.idincripcion && 
                                       n.ActividadAcademicaId == item.actividadid);

                if (notaExistente != null)
                {
                    // Actualizar nota existente
                    notaExistente.Nota = item.nota;
                    notaExistente.FechaRegistro = DateTime.Now; // Actualizar fecha de modificación
                }
                else
                {
                    // Crear nueva nota
                    Notas = new Notas
                    {
                        Nota = item.nota,
                        MateriasInscritasId = item.idincripcion,
                        ActividadAcademicaId = item.actividadid
                    };
                    _context.Notas.Add(Notas);
                }
            }
            
            await _context.SaveChangesAsync();
            //redireccionar aqui /ListadoEstudiantes?id=16 
            return RedirectToPage("./ListadoEstudiantes", new { id = this.idgrupo });

            
        }

       
    }
}
