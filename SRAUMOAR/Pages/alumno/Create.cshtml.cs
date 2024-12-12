﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.alumno
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
            ViewData["MunicipioId"] = new SelectList(_context.Municipios, "MunicipioId", "NombreMunicipio", selectedValue: null);
            return Page();
        }

        [BindProperty]
        public Alumno Alumno { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Alumno.Add(Alumno);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
