using SRAUMOAR.Servicios.FacturacionElectronica;
﻿using System;
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
            ViewData["CarreraId"] = new SelectList(_context.Carreras, "CarreraId", "NombreCarrera", Alumno?.CarreraId);
        }

        [BindProperty]
        public Alumno Alumno { get; set; } = default!;

        [BindProperty] 
        public IFormFile FotoUpload { get; set; }

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!CatalogoTerritorialFiscal.EsValida(Alumno.CodigoDepartamentoFiscal, Alumno.CodigoMunicipioFiscal, Alumno.CodigoDistritoFiscal))
                ModelState.AddModelError("Alumno.CodigoDistritoFiscal", "Seleccione Departamento → Municipio → Distrito de la misma ubicación fiscal.");
            if (string.IsNullOrWhiteSpace(Alumno.DireccionDeResidencia) || Alumno.DireccionDeResidencia.Length > 200)
                ModelState.AddModelError("Alumno.DireccionDeResidencia", "La dirección es obligatoria y admite hasta 200 caracteres.");
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

            Alumno.CodigoDepartamentoFiscal = CatalogoTerritorialFiscal.Codigo(Alumno.CodigoDepartamentoFiscal);
            Alumno.CodigoMunicipioFiscal = CatalogoTerritorialFiscal.Codigo(Alumno.CodigoMunicipioFiscal);
            Alumno.CodigoDistritoFiscal = CatalogoTerritorialFiscal.Codigo(Alumno.CodigoDistritoFiscal);
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
