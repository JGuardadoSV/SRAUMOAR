using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.materias
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public Materia Materia { get; set; } = default!;
        public List<MateriaPrerequisito> Prerrequisitos { get; set; } = new List<MateriaPrerequisito>();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var materia = await _context.Materias
                .Include(m => m.Pensum)
                .Include(m => m.Prerrequisitos)
                .ThenInclude(p => p.PrerrequisoMateria)
                .FirstOrDefaultAsync(m => m.MateriaId == id);

            if (materia == null)
            {
                return NotFound();
            }

            Materia = materia;
            Prerrequisitos = materia.Prerrequisitos?.ToList() ?? new List<MateriaPrerequisito>();

            return Page();
        }
    }
}
