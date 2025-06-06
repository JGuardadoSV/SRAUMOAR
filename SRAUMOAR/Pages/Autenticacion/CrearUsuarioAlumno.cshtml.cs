﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.Autenticacion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CrearUsuarioAlumnoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CrearUsuarioAlumnoModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty(SupportsGet = true)]
        public int AlumnoId { get; set; }

        public string nombre { get; set; }
        public string email { get; set; }
        public IActionResult OnGet()
        {
            
            Alumno resultado = _context.Alumno.Where(x => x.AlumnoId == AlumnoId).FirstOrDefault();

            nombre = $"{resultado.Nombres} {resultado.Apellidos}";
            email = resultado.Email;


            ViewData["NivelAccesoId"] = new SelectList(_context.NivelesAcceso.Where(x=>x.Id==4), "Id", "Nombre");
            return Page();
        }

        [BindProperty]
        public Usuario Usuario { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            Usuario.Activo = true;
            _context.Usuarios.Add(Usuario);
            await _context.SaveChangesAsync();


            //actualizando el id de usuario en el alumno

            int nuevoUsuarioId = Usuario.IdUsuario;

            // Buscar el alumno correspondiente (supongamos que tienes el AlumnoId disponible)
            Alumno? alumno = await _context.Alumno.FindAsync(AlumnoId); // Reemplaza AlumnoId con la forma en que obtienes el ID del alumno
            alumno.UsuarioId = nuevoUsuarioId;
            // Guardar los cambios en el contexto
            await _context.SaveChangesAsync();

            return Redirect("/alumno");

        }
    }
}
