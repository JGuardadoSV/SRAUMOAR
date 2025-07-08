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

        public IList<Materia> Materia { get; set; } = default!;
        public int idPensum { get; set; }
        public Pensum Pensum { get; set; }

        // Agregar esta propiedad
        public Dictionary<int, List<dynamic>> MateriasPrerrequisitos { get; set; } = new();

        public async Task OnGetAsync(int? id)
        {
            idPensum = id.Value;
            Pensum = await _context.Pensums.Include(p => p.Carrera)
                .FirstOrDefaultAsync(x => x.PensumId == id) ?? new Pensum();

            Materia = await _context.Materias
                .Include(m => m.Pensum)
                .Where(x => x.Pensum.PensumId == id)
                .ToListAsync();

            // Cargar todos los prerrequisitos de una vez
            var materiaIds = Materia.Select(m => m.MateriaId).ToList();
            var prerrequisitos = await _context.MateriasPrerrequisitos
                .Where(p => materiaIds.Contains(p.MateriaId))
                .Include(p => p.PrerrequisoMateria)
                .ThenInclude(pm => pm.Pensum)
                .Select(p => new
                {
                    p.MateriaId,
                    p.PrerrequisoMateria.CodigoMateria,
                    p.PrerrequisoMateria.NombreMateria,
                    p.PrerrequisoMateria.Ciclo,
                    p.PrerrequisoMateria.uv,
                    PensumNombre = p.PrerrequisoMateria.Pensum.NombrePensum
                })
                .ToListAsync();

            MateriasPrerrequisitos = prerrequisitos.GroupBy(p => p.MateriaId)
                .ToDictionary(g => g.Key, g => g.ToList<dynamic>());
        }
    }
}