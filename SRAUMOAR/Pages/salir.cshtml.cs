using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
namespace SRAUMOAR.Pages
{
    public class salirModel : PageModel
    {
       // private readonly SignInManager<IdentityUser> _signInManager;

        public salirModel()
        {
          //  _signInManager = signInManager;
        }

        public async Task<IActionResult> OnGet()
        {
            // Cerrar sesión
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            return Redirect("/Index");
            // Redirigir después de cerrar sesión
            return RedirectToPage("/Index");
        }
    }
}
