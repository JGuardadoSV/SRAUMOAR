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
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public int idgrupo = 0;
        public IActionResult OnGet(int? id)
        {
            idgrupo = id.Value;
            //var carrera =_context.Grupo.Include(x=>x.Carrera).Where(x=>x.GrupoId==id).FirstOrDefault().Carrera.CarreraId;
        ViewData["GrupoId"] = new SelectList(_context.Grupo.Where(x=>x.GrupoId==id), "GrupoId", "Nombre");
        ViewData["MateriaId"] = new SelectList(
            _context.Materias
            .Include(x => x.Pensum)
            .ThenInclude(x=>x.Carrera)
            .Where(x=>x.Pensum.PensumId==id)
            , "MateriaId", "NombreMateria");
            return Page();
        }

        [BindProperty]
        public MateriasGrupo MateriasGrupo { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.MateriasGrupo.Add(MateriasGrupo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
