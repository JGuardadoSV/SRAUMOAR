using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Becados> Becados { get; set; } = default!;
        
        // Propiedades para los filtros
        [BindProperty(SupportsGet = true)]
        public int? CarreraId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? EntidadBecaId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? TipoBeca { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? Estado { get; set; }

        // Listas para los dropdowns de filtros
        public SelectList Carreras { get; set; } = default!;
        public SelectList EntidadesBeca { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // Cargar las listas para los filtros
            Carreras = new SelectList(await _context.Carreras
                .Where(c => c.Activa == true)
                .OrderBy(c => c.NombreCarrera)
                .ToListAsync(), "CarreraId", "NombreCarrera");
                
            EntidadesBeca = new SelectList(await _context.InstitucionesBeca
                .OrderBy(e => e.Nombre)
                .ToListAsync(), "EntidadBecaId", "Nombre");

            // Construir la consulta base
            var query = _context.Becados
                .Include(b => b.Alumno)
                    .ThenInclude(a => a.Carrera)
                .Include(b => b.Ciclo)
                .Include(b => b.EntidadBeca)
                .Where(x => x.Ciclo.Activo == true);

            // Aplicar filtros
            if (CarreraId.HasValue)
            {
                query = query.Where(b => b.Alumno.CarreraId == CarreraId.Value);
            }

            if (EntidadBecaId.HasValue)
            {
                query = query.Where(b => b.EntidadBecaId == EntidadBecaId.Value);
            }

            if (!string.IsNullOrEmpty(TipoBeca))
            {
                if (TipoBeca == "Completa")
                {
                    query = query.Where(b => b.TipoBeca == 1);
                }
                else if (TipoBeca == "Parcial")
                {
                    query = query.Where(b => b.TipoBeca == 2);
                }
            }

            if (!string.IsNullOrEmpty(Estado))
            {
                if (Estado == "Activo")
                {
                    query = query.Where(b => b.Estado == true);
                }
                else if (Estado == "Inactivo")
                {
                    query = query.Where(b => b.Estado == false);
                }
            }

            // Ejecutar la consulta
            Becados = await query
                .OrderBy(b => b.Alumno.Apellidos)
                .ThenBy(b => b.Alumno.Nombres)
                .ToListAsync();
        }
    }
}
