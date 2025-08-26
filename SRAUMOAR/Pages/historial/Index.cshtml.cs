using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using System.ComponentModel;

namespace SRAUMOAR.Pages.historial
{
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public SelectList AlumnosList { get; set; } = null!;

        public async Task OnGetAsync()
        {
            // Obtener lista de alumnos activos para el dropdown
            var alumnos = await _context.Alumno
                .Where(a => a.Estado == 1) // Solo alumnos activos
                .OrderBy(a => a.Apellidos)
                .ThenBy(a => a.Nombres)
                .Select(a => new
                {
                    a.AlumnoId,
                    NombreCompleto = $"{a.Apellidos}, {a.Nombres}"
                })
                .ToListAsync();

            AlumnosList = new SelectList(alumnos, "AlumnoId", "NombreCompleto");
        }

        public async Task<IActionResult> OnGetBuscarAlumnosAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new JsonResult(new List<object>());

            var alumnos = await _context.Alumno
                .Where(a => a.Estado == 1 && 
                           (a.Apellidos.Contains(term) || 
                            a.Nombres.Contains(term) || 
                            a.Email.Contains(term)))
                .OrderBy(a => a.Apellidos)
                .ThenBy(a => a.Nombres)
                .Select(a => new
                {
                    a.AlumnoId,
                    a.Apellidos,
                    a.Nombres,
                    a.Email
                })
                .Take(20)
                .ToListAsync();

            // Procesar los resultados después de la consulta
            var resultado = alumnos.Select(a => new
            {
                id = a.AlumnoId,
                label = $"{a.Apellidos}, {a.Nombres} - {ExtraerCarnet(a.Email)}",
                value = $"{a.Apellidos}, {a.Nombres}",
                carnet = ExtraerCarnet(a.Email)
            }).ToList();

            return new JsonResult(resultado);
        }

        private static string ExtraerCarnet(string email)
        {
            if (string.IsNullOrEmpty(email))
                return "Sin carnet";

            // Buscar la posición del @
            int posicionArroba = email.IndexOf('@');
            if (posicionArroba > 0)
            {
                // Extraer todo lo que está antes del @
                string carnet = email.Substring(0, posicionArroba);
                return carnet;
            }

            return "Sin carnet";
        }
    }
}
