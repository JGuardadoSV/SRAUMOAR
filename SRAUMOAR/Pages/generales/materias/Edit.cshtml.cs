using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.materias
{
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Materia Materia { get; set; } = default!;

        [BindProperty]
        public List<int> PrerrequisitosSeleccionados { get; set; } = new List<int>();

        public List<MateriaPrerequisito> PrerrequisitosExistentes { get; set; } = new List<MateriaPrerequisito>();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var materia = await _context.Materias
                .Include(pe=>pe.Pensum)
                .Include(m => m.Prerrequisitos)
                .ThenInclude(p => p.PrerrequisoMateria)
                .FirstOrDefaultAsync(m => m.MateriaId == id);

            if (materia == null)
            {
                return NotFound();
            }

            Materia = materia;
            PrerrequisitosExistentes = materia.Prerrequisitos?.ToList() ?? new List<MateriaPrerequisito>();
            PrerrequisitosSeleccionados = PrerrequisitosExistentes.Select(p => p.PrerrequisoMateriaId).ToList();

            ViewData["PensumId"] = new SelectList(_context.Pensums.Where(x => x.PensumId == materia.PensumId), "PensumId", "CodigoPensum");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["PensumId"] = new SelectList(_context.Pensums, "PensumId", "CodigoPensum");
                return Page();
            }

            _context.Attach(Materia).State = EntityState.Modified;

            try
            {
                // Eliminar prerrequisitos existentes
                var prerrequisitosExistentes = await _context.MateriasPrerrequisitos
                    .Where(p => p.MateriaId == Materia.MateriaId)
                    .ToListAsync();
                _context.MateriasPrerrequisitos.RemoveRange(prerrequisitosExistentes);

                // Agregar nuevos prerrequisitos
                if (PrerrequisitosSeleccionados != null && PrerrequisitosSeleccionados.Any())
                {
                    foreach (var prerrequisitoId in PrerrequisitosSeleccionados)
                    {
                        var materiaPrerrequisito = new MateriaPrerequisito
                        {
                            MateriaId = Materia.MateriaId,
                            PrerrequisoMateriaId = prerrequisitoId
                        };
                        _context.MateriasPrerrequisitos.Add(materiaPrerrequisito);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MateriaExists(Materia.MateriaId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetMateriasDelPensumAsync(int pensumId, int materiaActualId)
        {
            var materias = await _context.Materias
                .Where(m => m.PensumId == pensumId && m.MateriaId != materiaActualId)
                .Select(m => new { m.MateriaId, Nombre = $"{m.CodigoMateria} - {m.NombreMateria}" })
                .ToListAsync();

            return new JsonResult(materias);
        }

        private bool MateriaExists(int id)
        {
            return _context.Materias.Any(e => e.MateriaId == id);
        }
    }
}
