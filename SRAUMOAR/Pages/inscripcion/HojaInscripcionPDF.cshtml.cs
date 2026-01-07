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
    public class HojaInscripcionPDFModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IConverter _converter;
        private readonly PdfService _pdfService;

        public HojaInscripcionPDFModel(
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
        public Ciclo CicloActual { get; set; }

        public IActionResult OnGet(int id)
        {
            CicloActual = _context.Ciclos.Where(x => x.Activo == true).FirstOrDefault();
            if (CicloActual == null)
            {
                return BadRequest("No hay un ciclo activo");
            }
            Alumno = _context.Alumno.Where(x => x.AlumnoId == id).FirstOrDefault() ?? new Alumno();

            MateriasInscritas = _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Materia)
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Docente)
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Grupo)
                .Where(mi => mi.MateriasGrupo.Grupo.CicloId == CicloActual.Id &&
                             mi.Alumno.AlumnoId == Alumno.AlumnoId)
                .ToList();

            return Page();
        }

        // NUEVO m�todo para generar PDF

    }
}