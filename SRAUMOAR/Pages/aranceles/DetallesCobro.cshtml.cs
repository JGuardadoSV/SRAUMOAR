using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.EntityFrameworkCore;
namespace SRAUMOAR.Pages.aranceles
{
    public class DetallesCobroModel : PageModel
    {

        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetallesCobroModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }


        public async Task<IActionResult> OnGet(int id)
        {
            var cobro = await _context.CobrosArancel
                                      .Include(c => c.Arancel)
                                      .Include(c => c.Alumno)
                                      .Include(c => c.Ciclo)
                                      .FirstOrDefaultAsync(c => c.CobroArancelId == id);

            if (cobro == null)
            {
                return NotFound();
            }

            return new JsonResult(new
            {
                Fecha = cobro.Fecha.ToString("dd/MM/yyyy"),
                Arancel = cobro.Arancel?.Nombre,
                Alumno = $"{cobro.Alumno?.Nombres} {cobro.Alumno?.Apellidos}",
                Ciclo = $"{cobro.Ciclo?.NCiclo}/{cobro.Ciclo?.anio}",
                Costo = cobro.Costo,
                EfectivoRecibido = cobro.EfectivoRecibido,
                Cambio = cobro.Cambio,
                Nota = cobro.nota
            });
        }

    }
}


