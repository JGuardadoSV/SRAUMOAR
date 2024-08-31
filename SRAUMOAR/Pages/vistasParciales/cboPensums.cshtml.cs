using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;


namespace SRAUMOAR.Pages.vistasParciales
{
    public class cboPensumsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public cboPensumsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public JsonResult OnGetPensumsByCarrera(int carreraId)
        {
            var pensums = _context.Pensums
                .Where(p => p.CarreraId == carreraId)
                .Select(p => new { p.PensumId, p.NombrePensum })
                .ToList();

            return new JsonResult(pensums);
        }
    }
}
