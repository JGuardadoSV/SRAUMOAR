using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.docentes
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
            CargarListas();
            return Page();
        }

        [BindProperty]
        public Docente Docente { get; set; } = default!;

        [BindProperty]
        public bool CrearUsuario { get; set; }

        [BindProperty]
        public Usuario Usuario { get; set; } = new Usuario();

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            Docente.FechaDeRegistro=DateTime.Now;

            if (!User.IsInRole("Administrador"))
            {
                CrearUsuario = false;
            }

            if (CrearUsuario)
            {
                if (string.IsNullOrWhiteSpace(Usuario.Clave))
                {
                    ModelState.AddModelError("Usuario.Clave", "La contrasena es obligatoria para crear el usuario.");
                }

                var rolesPermitidos = await _context.NivelesAcceso
                    .Where(x => x.Nombre == "Docentes" || x.Nombre == "Administracion" || x.Nombre == "Contabilidad")
                    .Select(x => x.Id)
                    .ToListAsync();

                if (!rolesPermitidos.Contains(Usuario.NivelAccesoId))
                {
                    ModelState.AddModelError("Usuario.NivelAccesoId", "Seleccione un rol valido.");
                }

                var correoYaRegistrado = await _context.Usuarios.AnyAsync(u => u.NombreUsuario == Docente.Email);
                if (correoYaRegistrado)
                {
                    ModelState.AddModelError("Docente.Email", "Ya existe un usuario registrado con este correo.");
                }
            }

            if (!ModelState.IsValid)
            {
                CargarListas();
                return Page();
            }

            if (CrearUsuario)
            {
                Usuario.NombreUsuario = Docente.Email;
                Usuario.Email = Docente.Email;
                Usuario.Activo = true;
                _context.Usuarios.Add(Usuario);
                await _context.SaveChangesAsync();
                Docente.UsuarioId = Usuario.IdUsuario;
            }

             _context.Docentes.Add(Docente);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void CargarListas()
        {
            ViewData["ProfesionId"] = new SelectList(_context.Profesiones, "ProfesionId", "NombreProfesion");
            ViewData["NivelAccesoId"] = new SelectList(
                _context.NivelesAcceso.Where(x =>
                    x.Nombre == "Docentes" ||
                    x.Nombre == "Administracion" ||
                    x.Nombre == "Contabilidad"),
                "Id",
                "Nombre");
        }
    }
}
