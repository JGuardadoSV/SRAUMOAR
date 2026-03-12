using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.grupos
{
    [Authorize(Roles = "Administrador,Administracion,Docentes")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Grupo> Grupo { get;set; } = default!;
        public IList<Carrera> Carreras { get; set; } = default!;
        [BindProperty(SupportsGet = true)]
        public int? CarreraId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Carreras = await _context.Carreras.Where(c => c.Activa).ToListAsync();
            var userId = User.FindFirstValue("UserId")??"0"; // Si lo guardaste con el nombre "UserId"
            if (!int.TryParse(userId, out int idusuario) || idusuario <= 0)
            {
                return Unauthorized();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(x => x.IdUsuario == idusuario);
            if (usuario == null)
            {
                return Unauthorized();
            }

            int rol = usuario.NivelAccesoId;
            IQueryable<Grupo> query;
            if (rol == 1 || rol == 2)
            {
                query = _context.Grupo
                   .Where(x => x.Ciclo.Activo == true)
                   .Include(g => g.Carrera)
                   .Include(g => g.Ciclo)
                   .Include(g => g.Docente);
            }
            else if (rol == 3)
            {
                var docente = await _context.Docentes.FirstOrDefaultAsync(x => x.UsuarioId == idusuario);
                if (docente == null)
                {
                    Grupo = new List<Grupo>();
                    return Page();
                }

                int IdDocente = docente.DocenteId;
                query = _context.Grupo
               .Where(x => x.Ciclo.Activo == true && x.Docente.DocenteId==IdDocente)
               .Include(g => g.Carrera)
               .Include(g => g.Ciclo)
               .Include(g => g.Docente);
            }
            else
            {
                return Unauthorized();
            }
            if (CarreraId.HasValue && CarreraId.Value > 0)
            {
                query = query.Where(g => g.CarreraId == CarreraId.Value);
            }
            Grupo = await query.ToListAsync();
            return Page();
        }
    }
}
