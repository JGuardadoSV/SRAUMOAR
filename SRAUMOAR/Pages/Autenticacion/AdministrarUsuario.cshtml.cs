using System;
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
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.Autenticacion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class AdministrarUsuarioModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public AdministrarUsuarioModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty(SupportsGet = true)]
        public int UsuarioId { get; set; }
        [BindProperty(SupportsGet = true)]
        public int Tipo { get; set; }
        
        public string nombre { get; set; }
        public string email { get; set; }
        public IActionResult OnGet()
        {
            if (Tipo == 1)
            {
                Alumno resultado = _context.Alumno.Where(x => x.AlumnoId == UsuarioId).FirstOrDefault();
                Usuario usuario = _context.Usuarios.Where(x => x.IdUsuario == resultado.UsuarioId).FirstOrDefault();
                nombre = $"{resultado.Nombres} {resultado.Apellidos}";
                email = usuario.NombreUsuario;


                ViewData["usuarioid"] = usuario.IdUsuario;
            }
            else
            {
                Docente resultado = _context.Docentes.Where(x => x.DocenteId == UsuarioId).FirstOrDefault();
                Usuario usuario = _context.Usuarios.Where(x => x.IdUsuario == resultado.UsuarioId).FirstOrDefault();
                nombre = $"{resultado.Nombres} {resultado.Apellidos}";
                email = usuario.NombreUsuario;


                ViewData["usuarioid"] = usuario.IdUsuario;
            }

            ViewData["tipo"] = Tipo;
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

            // Buscar el usuario existente en la base de datos
            var usuarioExistente = await _context.Usuarios.FindAsync(UsuarioId);

            if (usuarioExistente == null)
            {
                // Manejar el caso en que el usuario no sea encontrado
                return NotFound();
            }

            // Actualizar solo la propiedad Clave
            usuarioExistente.Clave = Usuario.Clave;

            // Guardar los cambios en la base de datos
            await _context.SaveChangesAsync();
            if (Tipo==1)
            {
                return Redirect("/alumno");
            }
            else
            {
                return Redirect("/generales/docentes");
            }
            
        }

    }
}
