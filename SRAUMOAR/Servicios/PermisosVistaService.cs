using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios
{
    public class PermisosVistaService
    {
        private readonly Contexto _context;

        public PermisosVistaService(Contexto context)
        {
            _context = context;
        }

        public async Task<HashSet<string>> ObtenerCodigosPermitidosAsync(ClaimsPrincipal user)
        {
            var rol = user.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrWhiteSpace(rol))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var codigos = await _context.PermisosModuloRol
                .AsNoTracking()
                .Where(p => p.PuedeVer && p.NivelAcceso != null && p.NivelAcceso.Nombre == rol && p.ModuloPermiso != null && p.ModuloPermiso.Activo)
                .Select(p => p.ModuloPermiso!.Codigo)
                .ToListAsync();

            return codigos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
