using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.usuarios
{
    public class usuariosalumnosModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public usuariosalumnosModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Usuario> Usuario { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Usuario = await _context.Usuarios
                .Include(u => u.NivelAcceso).Where(x=>x.IdUsuario>1).ToListAsync();
        }
    }
}
