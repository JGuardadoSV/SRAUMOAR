using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRAUMOAR.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnPost()
        {
            // L�gica para procesar el formulario

            // Redirigir a la p�gina Index
            return RedirectToPage("/Home");
        }
    }
}
