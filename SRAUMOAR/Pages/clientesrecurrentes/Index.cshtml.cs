using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.clientesrecurrentes;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public class IndexModel : PageModel
{
    private readonly Contexto _context;
    public IReadOnlyList<ClienteRecurrente> Clientes { get; private set; } = Array.Empty<ClienteRecurrente>();

    public IndexModel(Contexto context) => _context = context;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Clientes = await _context.ClientesRecurrentes
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .ToListAsync(cancellationToken);
    }
}
