using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.EntityFrameworkCore;
namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class DetallesCobroModel : PageModel
    {

        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetallesCobroModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }


        public async Task<IActionResult> OnGet(int id)
        {
            var cobros = await _context.DetallesCobrosArancel
                .Include(d => d.Arancel)
                .Include(d => d.CobroArancel)
                    .ThenInclude(c => c.Alumno)
                .Include(d => d.CobroArancel)
                    .ThenInclude(c => c.Ciclo) 
                .Where(d => d.CobroArancelId == id)
                .ToListAsync();

            var cobro = await _context.CobrosArancel
                .Include(c => c.Alumno).Include(c => c.Ciclo)
                .FirstOrDefaultAsync(c => c.CobroArancelId == id);

            if (cobros == null)
            {
                return NotFound();
            }

            var arancelesDetalles = cobros.Select(d => new
            {
                Arancel = d.Arancel.Nombre,
                Costo = d.Arancel.Costo
            }).ToList();

            return new JsonResult(new
            {
                Fecha = cobro.Fecha.ToString("dd/MM/yyyy"),
                Alumno = $"{cobro.Alumno?.Nombres} {cobro.Alumno?.Apellidos}",
                Ciclo = $"{cobro.Ciclo?.NCiclo}/{cobro.Ciclo?.anio}",
                CostoTotal = cobro.Total,
                EfectivoRecibido = cobro.EfectivoRecibido,
                Cambio = cobro.Cambio,
                Nota = cobro.nota,
                ArancelesDetalles = arancelesDetalles
            });
        }


    }
}


