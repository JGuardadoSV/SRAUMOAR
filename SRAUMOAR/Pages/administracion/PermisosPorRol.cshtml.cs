using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class PermisosPorRolModel : PageModel
    {
        private readonly Contexto _context;

        public PermisosPorRolModel(Contexto context)
        {
            _context = context;
        }

        public sealed class PermisoFilaInput
        {
            public int ModuloPermisoId { get; set; }
            public string Codigo { get; set; } = string.Empty;
            public string Modulo { get; set; } = string.Empty;
            public bool Administrador { get; set; }
            public bool Administracion { get; set; }
            public bool Contabilidad { get; set; }
            public bool Docentes { get; set; }
            public bool Estudiantes { get; set; }
        }

        [BindProperty]
        public List<PermisoFilaInput> Permisos { get; set; } = new();

        public async Task OnGetAsync()
        {
            Permisos = await CargarPermisosAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Permisos = await CargarPermisosAsync();
                return Page();
            }

            var roles = await _context.NivelesAcceso
                .Where(r => r.Nombre != null)
                .ToDictionaryAsync(r => r.Nombre!, r => r.Id);

            foreach (var fila in Permisos)
            {
                await GuardarPermisoAsync(fila.ModuloPermisoId, roles, "Administrador", fila.Administrador);
                await GuardarPermisoAsync(fila.ModuloPermisoId, roles, "Administracion", fila.Administracion);
                await GuardarPermisoAsync(fila.ModuloPermisoId, roles, "Contabilidad", fila.Contabilidad);
                await GuardarPermisoAsync(fila.ModuloPermisoId, roles, "Docentes", fila.Docentes);
                await GuardarPermisoAsync(fila.ModuloPermisoId, roles, "Estudiantes", fila.Estudiantes);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Permisos actualizados correctamente.";
            return RedirectToPage();
        }

        private async Task<List<PermisoFilaInput>> CargarPermisosAsync()
        {
            var roles = await _context.NivelesAcceso
                .AsNoTracking()
                .Where(r => r.Nombre != null)
                .ToDictionaryAsync(r => r.Nombre!, r => r.Id);

            var permisos = await _context.PermisosModuloRol
                .AsNoTracking()
                .ToListAsync();

            var modulos = await _context.ModulosPermiso
                .AsNoTracking()
                .Where(m => m.Activo)
                .OrderBy(m => m.Orden)
                .ToListAsync();

            return modulos.Select(m => new PermisoFilaInput
            {
                ModuloPermisoId = m.Id,
                Codigo = m.Codigo,
                Modulo = m.Nombre,
                Administrador = TienePermiso(permisos, m.Id, roles, "Administrador"),
                Administracion = TienePermiso(permisos, m.Id, roles, "Administracion"),
                Contabilidad = TienePermiso(permisos, m.Id, roles, "Contabilidad"),
                Docentes = TienePermiso(permisos, m.Id, roles, "Docentes"),
                Estudiantes = TienePermiso(permisos, m.Id, roles, "Estudiantes")
            }).ToList();
        }

        private static bool TienePermiso(List<PermisoModuloRol> permisos, int moduloId, Dictionary<string, int> roles, string rolNombre)
        {
            if (!roles.TryGetValue(rolNombre, out var rolId))
            {
                return false;
            }

            return permisos.Any(p => p.ModuloPermisoId == moduloId && p.NivelAccesoId == rolId && p.PuedeVer);
        }

        private async Task GuardarPermisoAsync(int moduloId, Dictionary<string, int> roles, string rolNombre, bool puedeVer)
        {
            if (!roles.TryGetValue(rolNombre, out var rolId))
            {
                return;
            }

            var permiso = await _context.PermisosModuloRol
                .FirstOrDefaultAsync(p => p.ModuloPermisoId == moduloId && p.NivelAccesoId == rolId);

            if (permiso == null)
            {
                _context.PermisosModuloRol.Add(new PermisoModuloRol
                {
                    ModuloPermisoId = moduloId,
                    NivelAccesoId = rolId,
                    PuedeVer = puedeVer
                });
                return;
            }

            permiso.PuedeVer = puedeVer;
        }
    }
}
