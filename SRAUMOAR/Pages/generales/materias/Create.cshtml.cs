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
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet(int? id)
        {
            ViewData["PensumId"] = new SelectList(_context.Pensums.Where(x => x.PensumId == id), "PensumId", "NombrePensum");
            return Page();
        }

        [BindProperty]
        public Materia Materia { get; set; } = default!;

        [BindProperty]
        public List<int> PrerrequisitosSeleccionados { get; set; } = new List<int>();

        public SelectList MateriasDelPensum { get; set; } = new SelectList(new List<object>(), "Value", "Text");

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["PensumId"] = new SelectList(_context.Pensums, "PensumId", "CodigoPensum");
                return Page();
            }

            _context.Materias.Add(Materia);
            await _context.SaveChangesAsync();

            // Agregar prerrequisitos si se seleccionaron
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
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetMateriasDelPensumAsync(int pensumId)
        {
            var materias = await _context.Materias
                .Where(m => m.PensumId == pensumId)
                .Select(m => new { m.MateriaId, Nombre = $"{m.CodigoMateria} - {m.NombreMateria}" })
                .ToListAsync();

            return new JsonResult(materias);
        }
    }
}
