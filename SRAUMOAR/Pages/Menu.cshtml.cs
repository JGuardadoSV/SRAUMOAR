using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRAUMOAR.Pages
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class MenuModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
