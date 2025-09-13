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
            var alumno = await _context.Alumno
                .Include(a => a.Carrera)
                .Include(a => a.Municipio)
                .FirstOrDefaultAsync(m => m.AlumnoId == id);
            if (alumno == null)
            {
                return NotFound();
            }
            Alumno = alumno;
            
            // Debug: Verificar que los campos se cargan correctamente
            System.Diagnostics.Debug.WriteLine($"Carnet: '{Alumno.Carnet}'");
            System.Diagnostics.Debug.WriteLine($"MunicipioNacimiento: '{Alumno.MunicipioNacimiento}'");
            System.Diagnostics.Debug.WriteLine($"DepartamentoNacimiento: '{Alumno.DepartamentoNacimiento}'");
            System.Diagnostics.Debug.WriteLine($"EstudiosFinanciadoPor: '{Alumno.EstudiosFinanciadoPor}'");
            
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

            // Buscar el alumno existente en la base de datos
            var alumnoExistente = await _context.Alumno.FindAsync(Alumno.AlumnoId);
            if (alumnoExistente == null)
            {
                return NotFound();
            }

            // Debug: Verificar los valores que vienen del formulario
            System.Diagnostics.Debug.WriteLine($"POST - Carnet: '{Alumno.Carnet}'");
            System.Diagnostics.Debug.WriteLine($"POST - MunicipioNacimiento: '{Alumno.MunicipioNacimiento}'");
            System.Diagnostics.Debug.WriteLine($"POST - DepartamentoNacimiento: '{Alumno.DepartamentoNacimiento}'");
            System.Diagnostics.Debug.WriteLine($"POST - EstudiosFinanciadoPor: '{Alumno.EstudiosFinanciadoPor}'");

            // Actualizar solo los campos específicos
            alumnoExistente.Nombres = Alumno.Nombres;
            alumnoExistente.Apellidos = Alumno.Apellidos;
            alumnoExistente.FechaDeNacimiento = Alumno.FechaDeNacimiento;
            alumnoExistente.Email = Alumno.Email;
            alumnoExistente.DUI = Alumno.DUI;
            alumnoExistente.TelefonoPrimario = Alumno.TelefonoPrimario;
            alumnoExistente.Whatsapp = Alumno.Whatsapp;
            alumnoExistente.TelefonoSecundario = Alumno.TelefonoSecundario;
            alumnoExistente.ContactoDeEmergencia = Alumno.ContactoDeEmergencia;
            alumnoExistente.NumeroDeEmergencia = Alumno.NumeroDeEmergencia;
            alumnoExistente.DireccionDeResidencia = Alumno.DireccionDeResidencia;
            alumnoExistente.Carnet = Alumno.Carnet;
            alumnoExistente.MunicipioNacimiento = Alumno.MunicipioNacimiento;
            alumnoExistente.DepartamentoNacimiento = Alumno.DepartamentoNacimiento;
            alumnoExistente.EstudiosFinanciadoPor = Alumno.EstudiosFinanciadoPor;
            alumnoExistente.Genero = Alumno.Genero;
            alumnoExistente.CarreraId = Alumno.CarreraId;
            alumnoExistente.PermiteInscripcionSinPago = Alumno.PermiteInscripcionSinPago;
            alumnoExistente.ExentoMora = Alumno.ExentoMora;
            alumnoExistente.Casado = Alumno.Casado;

            // Solo actualizar la foto si se subió una nueva
            if (FotoUpload != null)
            {
                alumnoExistente.Foto = Alumno.Foto;
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
