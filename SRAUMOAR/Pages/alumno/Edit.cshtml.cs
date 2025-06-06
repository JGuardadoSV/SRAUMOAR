using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.alumno
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Alumno Alumno { get; set; } = default!;
        [BindProperty]
        public IFormFile FotoUpload { get; set; }
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            ViewData["CarreraId"] = new SelectList(_context.Carreras, "CarreraId", "NombreCarrera");
            ViewData["MunicipioId"] = new SelectList(_context.Municipios, "MunicipioId", "NombreMunicipio");
            var alumno =  await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);
            if (alumno == null)
            {
                return NotFound();
            }
            Alumno = alumno;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Alumno.Fotografia");
            ModelState.Remove("Alumno.Foto");
            ModelState.Remove("FotoUpload");
            ModelState.Remove("Alumno.DireccionDeResidencia");
            ModelState.Remove("Alumno.MunicipioId");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Procesar la foto si se subió una nueva
            if (FotoUpload != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await FotoUpload.CopyToAsync(memoryStream);
                    using (var originalImage = Image.FromStream(memoryStream))
                    {
                        var compressedImageStream = new MemoryStream();
                        var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 20L);
                        originalImage.Save(compressedImageStream, jpegEncoder, encoderParameters);
                        Alumno.Foto = compressedImageStream.ToArray();
                    }
                }
            }

            // Adjuntar la entidad y marcar solo los campos específicos como modificados
            _context.Attach(Alumno);

            // Marcar como modificados solo los campos que quieres actualizar
            // Elimina las líneas de los campos que NO quieres actualizar
            _context.Entry(Alumno).Property(a => a.Nombres).IsModified = true;
            _context.Entry(Alumno).Property(a => a.Apellidos).IsModified = true;
            _context.Entry(Alumno).Property(a => a.FechaDeNacimiento).IsModified = true;
            _context.Entry(Alumno).Property(a => a.FechaDeRegistro).IsModified = false;
            _context.Entry(Alumno).Property(a => a.Email).IsModified = false;
            _context.Entry(Alumno).Property(a => a.DUI).IsModified = true;
            _context.Entry(Alumno).Property(a => a.TelefonoPrimario).IsModified = true;
            _context.Entry(Alumno).Property(a => a.Whatsapp).IsModified = true;
            _context.Entry(Alumno).Property(a => a.TelefonoSecundario).IsModified = true;
            _context.Entry(Alumno).Property(a => a.ContactoDeEmergencia).IsModified = true;
            _context.Entry(Alumno).Property(a => a.NumeroDeEmergencia).IsModified = true;
            _context.Entry(Alumno).Property(a => a.DireccionDeResidencia).IsModified = true;
            _context.Entry(Alumno).Property(a => a.Estado).IsModified = false;
            _context.Entry(Alumno).Property(a => a.IngresoPorEquivalencias).IsModified = false;
            _context.Entry(Alumno).Property(a => a.Fotografia).IsModified = false;
            _context.Entry(Alumno).Property(a => a.Carnet).IsModified = true;
            _context.Entry(Alumno).Property(a => a.Genero).IsModified = false;
            _context.Entry(Alumno).Property(a => a.UsuarioId).IsModified = false;
            _context.Entry(Alumno).Property(a => a.MunicipioId).IsModified = false;
            _context.Entry(Alumno).Property(a => a.CarreraId).IsModified = false;

            // Solo marcar la foto como modificada si se subió una nueva
            if (FotoUpload != null)
            {
                _context.Entry(Alumno).Property(a => a.Foto).IsModified = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AlumnoExists(Alumno.AlumnoId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private bool AlumnoExists(int id)
        {
            return _context.Alumno.Any(e => e.AlumnoId == id);
        }
    }
}
