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
using Microsoft.EntityFrameworkCore;

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

        [BindProperty(SupportsGet = true)]
        public string? BuscarMunicipio { get; set; }

        public IActionResult OnGet()
        {
            CargarCombos();
            return Page();
        }

        private void CargarCombos()
        {
            var query = _context.Municipios
                .Include(m => m.Distrito)
                .AsQueryable();

            if (!string.IsNullOrEmpty(BuscarMunicipio))
            {
                query = query.Where(m => m.NombreMunicipio.Contains(BuscarMunicipio) || 
                                       m.Distrito.NombreDistrito.Contains(BuscarMunicipio));
            }

            var orderedQuery = query.OrderBy(m => m.Distrito.NombreDistrito)
                                  .ThenBy(m => m.NombreMunicipio);

            ViewData["MunicipioId"] = new SelectList(
                orderedQuery.Select(m => new {
                    MunicipioId = m.MunicipioId,
                    NombreCompleto = $"{m.NombreMunicipio} - {m.Distrito.NombreDistrito}"
                }),
                "MunicipioId",
                "NombreCompleto",
                Alumno?.MunicipioId
            );

            ViewData["CarreraId"] = new SelectList(_context.Carreras, "CarreraId", "NombreCarrera", Alumno?.CarreraId);
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
                CargarCombos();
                return Page();
            }

            if (FotoUpload != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await FotoUpload.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    using (var image = SixLabors.ImageSharp.Image.Load(memoryStream))
                    {
                        var compressedImageStream = new MemoryStream();
                        image.Save(compressedImageStream, new JpegEncoder { Quality = 75 });
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
