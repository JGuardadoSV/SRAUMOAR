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
    public class MateriasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public MateriasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Materia> Materia { get;set; } = default!;
        public int idPensum { get; set; }
        public async Task OnGetAsync(int? id)
        {
            idPensum = id.Value;
            Materia = await _context.Materias
                .Include(m => m.Pensum).Where(x => x.Pensum.CarreraId == id).ToListAsync();
        }
    }
}
