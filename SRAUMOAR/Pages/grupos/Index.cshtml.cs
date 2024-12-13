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

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue("UserId")??"0"; // Si lo guardaste con el nombre "UserId"
            int idusuario = int.Parse(userId);
            int rol=_context.Usuarios.Where(x => x.IdUsuario == idusuario).First().NivelAccesoId;
            if (rol == 1 || rol == 2)
            {
                Grupo = await _context.Grupo
                   .Where(x => x.Ciclo.Activo == true)
                   .Include(g => g.Carrera)
                   .Include(g => g.Ciclo)
                   .Include(g => g.Docente).ToListAsync();
            }
            else if (rol == 3)
            {
                int IdDocente = _context.Docentes.Where(x => x.UsuarioId == idusuario).First().DocenteId;
                //extraer los grupos del docente unicamente
                Grupo = await _context.Grupo
               .Where(x => x.Ciclo.Activo == true && x.Docente.DocenteId==IdDocente)
               .Include(g => g.Carrera)
               .Include(g => g.Ciclo)
               .Include(g => g.Docente).ToListAsync();
            }
            else
            {
                return Unauthorized();
            }
            return Page();

        }
    }
}
