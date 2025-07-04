using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.grupos
{
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            var carreras = _context.Carreras.ToList();
            var pensums = _context.Pensums.ToList();
            carreras.Insert(0, new Carrera { CarreraId = 0, NombreCarrera = "Seleccione una carrera" });
            pensums.Insert(0, new Pensum { PensumId = 0, NombrePensum = "Seleccione un pensum" });
            ViewData["CarreraId"] = new SelectList(carreras, "CarreraId", "NombreCarrera");
            ViewData["PensumId"] = new SelectList(pensums, "PensumId", "NombrePensum");


            ViewData["CicloId"] = new SelectList(
                _context.Ciclos
                .Where(x => x.Activo == true)
                .Select(d => new
                {
                    Id = d.Id,
                    Ciclon = d.NCiclo + "-" + d.anio
                }), "Id", "Ciclon");


            ViewData["DocenteId"] = new SelectList(
                                                    _context.Docentes.Select(d => new
                                                    {
                                                        DocenteId = d.DocenteId,
                                                        NombreCompleto = d.Nombres + " " + d.Apellidos
                                                    }),
                                                    "DocenteId",
                                                    "NombreCompleto"
                                                );
            return Page();
        }

        [BindProperty]
        public Grupo Grupo { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Grupo.Add(Grupo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
