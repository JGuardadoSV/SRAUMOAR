using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRAUMOAR.Pages.portal.docente
{
    //solo rol Docentes
    [Authorize(Roles = "Docentes")]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
