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
            if (!int.TryParse(userId, out int idusuario) || idusuario <= 0)
            {
                MateriasGrupo = new List<MateriasGrupo>();
                return;
            }

            var docente = await _context.Docentes
                .FirstOrDefaultAsync(x => x.UsuarioId == idusuario);

            if (docente == null)
            {
                MateriasGrupo = new List<MateriasGrupo>();
                return;
            }
             
            // Obtener ciclo actual
            var cicloActual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
            if (cicloActual == null)
            {
                MateriasGrupo = new List<MateriasGrupo>();
                return;
            }
             
            MateriasGrupo = await _context.MateriasGrupo
                .Where(x => x.DocenteId == docente.DocenteId && x.Grupo.CicloId == cicloActual.Id)
                .Include(m => m.Grupo)
                .Include(m => m.Materia)
                .ToListAsync();
        }
    }
}
