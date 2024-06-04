using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRAUMOAR.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnPost()
        {
            // Lógica para procesar el formulario

            // Redirigir a la página Index
            return RedirectToPage("/Home");
        }
    }
}
