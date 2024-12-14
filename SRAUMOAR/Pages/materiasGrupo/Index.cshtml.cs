using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
{
    [Authorize(Roles = "Administrador,Administracion,Docentes")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public int idgrupo = 0;
        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<MateriasGrupo> MateriasGrupo { get;set; } = default!;

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue("UserId") ?? "0"; // Si lo guardaste con el nombre "UserId"
            int idusuario = int.Parse(userId);
            int rol = _context.Usuarios.Where(x => x.IdUsuario == idusuario).First().NivelAccesoId;
            int iddocente= _context.Docentes.Where(x => x.UsuarioId == idusuario).First().DocenteId;
            
            MateriasGrupo = await _context.MateriasGrupo.Where(x=>x.DocenteId==iddocente)
                .Include(m => m.Grupo)
                .Include(m => m.Materia).ToListAsync();
        }
    }
}
