using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using DinkToPdf;
using DinkToPdf.Contracts;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.inscripcion
{
    public class MateriasInscritasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IConverter _converter;
        private readonly PdfService _pdfService;

        public MateriasInscritasModel(
            SRAUMOAR.Modelos.Contexto context,
            IConverter converter,
            PdfService pdfService)
        {
            _context = context;
            _converter = converter;
            _pdfService = pdfService;
        }

        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public Alumno Alumno { get; set; }

        public IActionResult OnGet(int id)
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();

            MateriasInscritas = _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Materia)
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Docente)
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Grupo)
                .Where(mi => mi.MateriasGrupo.Grupo.CicloId == cicloactual &&
                             mi.Alumno.AlumnoId == Alumno.AlumnoId)
                .ToList();

            return Page();
        }

        // NUEVO método para generar PDF
        public IActionResult OnPostEliminar(int id)
        {
            var inscripcion = _context.MateriasInscritas.FirstOrDefault(x => x.MateriasInscritasId == id);
            if (inscripcion != null)
            {
                int alumnoId = inscripcion.AlumnoId;
                _context.MateriasInscritas.Remove(inscripcion);
                _context.SaveChanges();
                // Redirige al mismo resumen del alumno
                return RedirectToPage(new { id = alumnoId });
            }
            // Si no se encuentra, recarga la página actual
            return RedirectToPage();
        }

        // Método para desinscribir completamente al alumno del ciclo
        public IActionResult OnPostDesinscribir(int id)
        {
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault()?.Id ?? 0;
            
            // Buscar la inscripción del alumno en el ciclo actual
            var inscripcion = _context.Inscripciones
                .FirstOrDefault(i => i.AlumnoId == id && i.CicloId == cicloactual);

            if (inscripcion != null)
            {
                // Eliminar todas las materias inscritas del alumno en el ciclo actual
                var materiasInscritas = _context.MateriasInscritas
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Grupo)
                    .Where(mi => mi.AlumnoId == id && 
                                mi.MateriasGrupo.Grupo.CicloId == cicloactual)
                    .ToList();

                if (materiasInscritas.Any())
                {
                    _context.MateriasInscritas.RemoveRange(materiasInscritas);
                }

                // Eliminar la inscripción del ciclo
                _context.Inscripciones.Remove(inscripcion);
                _context.SaveChanges();

                // Redirigir a la página de inscripción
                return RedirectToPage("./Create", new { id = id });
            }

            // Si no se encuentra la inscripción, redirigir a la página de inscripción
            return RedirectToPage("./Create", new { id = id });
        }
    }
}