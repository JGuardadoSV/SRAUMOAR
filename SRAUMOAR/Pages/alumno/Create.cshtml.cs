using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using Microsoft.AspNetCore.Authorization;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
namespace SRAUMOAR.Pages.alumno
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            ViewData["MunicipioId"] = new SelectList(_context.Municipios, "MunicipioId", "NombreMunicipio", selectedValue: null);
            ViewData["CarreraId"] = new SelectList(_context.Carreras, "CarreraId", "NombreCarrera", selectedValue: null);
            return Page();
        }

        [BindProperty]
        public Alumno Alumno { get; set; } = default!;

        [BindProperty] 
        public IFormFile FotoUpload { get; set; }

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (FotoUpload != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await FotoUpload.CopyToAsync(memoryStream);
                    memoryStream.Position = 0; // Reiniciar posición del stream antes de leerlo

                    using (var image = SixLabors.ImageSharp.Image.Load(memoryStream))
                    {
                        var compressedImageStream = new MemoryStream();

                        var encoder = new JpegEncoder
                        {
                            Quality = 50 // Calidad de 0 a 100
                        };

                        image.Save(compressedImageStream, encoder);
                        Alumno.Foto = compressedImageStream.ToArray();
                    }
                }
            }
            _context.Alumno.Add(Alumno);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        //private ImageCodecInfo GetEncoder(ImageFormat format)
        //{
        //    var codecs = ImageCodecInfo.GetImageDecoders();
        //    foreach (var codec in codecs)
        //    {
        //        if (codec.FormatID == format.Guid)
        //        {
        //            return codec;
        //        }
        //    }
        //    return null;
        //}
    }
}
