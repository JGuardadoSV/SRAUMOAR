using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.facturas.procesar
{
    public class EnviarContingenciaModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfigService _emisorConfig;

        public EnviarContingenciaModel(SRAUMOAR.Modelos.Contexto context, EmisorConfigService emisorConfig)
        {
            _context = context;
            _emisorConfig = emisorConfig;
        }

        [BindProperty]
        public Factura Factura { get; set; } = default!;

        [BindProperty]
        public string Observaciones { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas.FirstOrDefaultAsync(m => m.FacturaId == id);
            if (factura == null)
            {
                return NotFound();
            }

            // Verificar que la factura esté pendiente
            if (!string.IsNullOrEmpty(factura.SelloRecepcion))
            {
                TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede ser procesada.";
                return RedirectToPage("./Index");
            }

            Factura = factura;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var factura = await _context.Facturas.FindAsync(id);
            if (factura == null)
            {
                return NotFound();
            }

            // Verificar que la factura esté pendiente
            if (!string.IsNullOrEmpty(factura.SelloRecepcion))
            {
                TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede ser procesada.";
                return RedirectToPage("./Index");
            }

            if (string.IsNullOrEmpty(factura.JsonDte))
            {
                TempData["Error"] = "JSON original vacío.";
                return Page();
            }

            try
            {
                var jsonObj = JObject.Parse(factura.JsonDte);
                bool contingenciaRecibida = false;

                // PASO 1: NOTIFICAR CONTINGENCIA
                try
                {
                    // Armar la petición de contingencia
                    var identificacion = new
                    {
                        version = 3,
                        ambiente = _emisorConfig.AmbienteString,
                        codigoGeneracion = Guid.NewGuid().ToString().ToUpper(),
                        fTransmision = DateTime.Now.ToString("yyyy-MM-dd"),
                        hTransmision = DateTime.Now.ToString("HH:mm:ss")
                    };

                    // Emisor
                    var emisor = new
                    {
                        nit = _emisorConfig.NIT?.Replace(" ", "").Replace("-", "").Trim(),
                        nombre = _emisorConfig.NOMBRECOMERCIAL,
                        nombreResponsable = "Administrador general",
                        tipoDocResponsable = "37",
                        numeroDocResponsable = "0000001",
                        tipoEstablecimiento = "02",
                        codEstableMH = "M001",
                        codPuntoVenta = (string)null,
                        telefono = _emisorConfig.TELEFONO?.Replace(" ", "").Replace("-", "").Trim(),
                        correo = _emisorConfig.EMAIL
                    };

                    // Detalle DTE
                    var detalleDTE = new[]
                    {
                        new
                        {
                            noItem = 1,
                            codigoGeneracion = jsonObj["identificacion"]["codigoGeneracion"]?.ToString() ?? "",
                            tipoDoc = jsonObj["identificacion"]["tipoDte"]?.ToString() ?? ""
                        }
                    };

                    // Motivo
                    var motivo = new
                    {
                        fInicio = jsonObj["identificacion"]["fecEmi"]?.ToString(),
                        fFin = jsonObj["identificacion"]["fecEmi"]?.ToString(),
                        hInicio = jsonObj["identificacion"]["horEmi"]?.ToString(),
                        hFin = jsonObj["identificacion"]["horEmi"]?.ToString(),
                        tipoContingencia = 4,
                        motivoContingencia = "Falla en el sistema de facturacion"
                    };

                    // Objeto completo del DTE para contingencia
                    var dteCompleto = JsonConvert.SerializeObject(new
                    {
                        identificacion = identificacion,
                        emisor = emisor,
                        detalleDTE = detalleDTE,
                        motivo = motivo
                    });

                    var solicitudContingencia = new
                    {
                        Usuario = _emisorConfig.NIT,
                        Password = _emisorConfig.PasswordAPI,
                        Ambiente = _emisorConfig.AmbienteString,
                        DteJson = dteCompleto,
                        Nit = _emisorConfig.NIT,
                        PasswordPrivado = _emisorConfig.PasswordCertificado
                    };

                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.PostAsJsonAsync($"{_emisorConfig.ApiUrl}/api/contingencia", solicitudContingencia);
                        var responseData = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonRecibido = JObject.Parse(responseData);
                            var estado = jsonRecibido["estado"]?.ToString();
                            var mensaje = jsonRecibido["mensaje"]?.ToString();
                            var selloContingencia = jsonRecibido["selloRecibido"]?.ToString();

                            if (!string.IsNullOrEmpty(selloContingencia) && estado == "RECIBIDO")
                            {
                                contingenciaRecibida = true;
                                TempData["Info"] = $"Contingencia notificada: {estado} - {mensaje} - {selloContingencia}. Se procederá a enviar el DTE";
                            }
                            else if (responseData.Contains("Ya existe envento de contingencia para codigo generacion"))
                            {
                                contingenciaRecibida = true;
                                TempData["Info"] = "Ya existe evento de contingencia para este código de generación. Se procederá a enviar el DTE";
                            }
                        }
                        else
                        {
                            TempData["Error"] = $"Error al notificar contingencia: {responseData}";
                            return Page();
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error al notificar contingencia: {ex.Message}";
                    return Page();
                }

                if (!contingenciaRecibida)
                {
                    TempData["Error"] = "Contingencia no se recibió, no se puede enviar el DTE.";
                    return Page();
                }

                // PASO 2: ENVIAR DTE EN MODO CONTINGENCIA
                try
                {
                    // Marcar DTE en modo contingencia
                    if (jsonObj["identificacion"] == null) jsonObj["identificacion"] = new JObject();
                    jsonObj["identificacion"]["tipoModelo"] = 2;
                    jsonObj["identificacion"]["tipoOperacion"] = 2;
                    jsonObj["identificacion"]["tipoContingencia"] = 4;
                    jsonObj["identificacion"]["motivoContin"] = "Falla en el sistema de facturacion";

                    // Convertir a JSON compacto para envío
                    var jsonEnviado = jsonObj.ToString(Formatting.None);

                    var request = new
                    {
                        Usuario = _emisorConfig.NIT,
                        Password = _emisorConfig.PasswordAPI,
                        Ambiente = _emisorConfig.AmbienteString,
                        DteJson = jsonEnviado,
                        Nit = _emisorConfig.NIT,
                        PasswordPrivado = _emisorConfig.PasswordCertificado,
                        TipoDte = jsonObj["identificacion"]["tipoDte"]?.ToString(),
                        CodigoGeneracion = jsonObj["identificacion"]["codigoGeneracion"]?.ToString(),
                        NumControl = jsonObj["identificacion"]["numeroControl"]?.ToString(),
                        VersionDte = int.Parse(jsonObj["identificacion"]["version"]?.ToString() ?? "1"),
                        CorreoCliente = string.IsNullOrEmpty(jsonObj["receptor"]?["correo"]?.ToString()) ? _emisorConfig.EMAIL : jsonObj["receptor"]?["correo"]?.ToString()
                    };

                    using (var client = new HttpClient())
                    {
                        var response = await client.PostAsJsonAsync($"{_emisorConfig.ApiUrl}/api/procesar-dte", request);
                        var responseData = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            TempData["Error"] = $"Error al procesar DTE enviado en contingencia: {responseData}";
                            return Page();
                        }

                        var resultado = JsonDocument.Parse(responseData).RootElement;
                        var sello = resultado.TryGetProperty("selloRecibido", out var sr) ? sr.GetString() : null;
                        
                        if (string.IsNullOrEmpty(sello))
                        {
                            TempData["Error"] = "No se obtuvo sello de recepción en la respuesta.";
                            return Page();
                        }

                        // Actualizar la factura con el sello recibido
                        factura.SelloRecepcion = sello;
                        factura.JsonDte = jsonEnviado; // Guardar el JSON compacto
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"DTE enviado en contingencia exitosamente. Sello: {sello}";
                        return RedirectToPage("./Index");
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error al procesar DTE en contingencia: {ex.Message}";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error durante el proceso de envío por contingencia: {ex.Message}";
                return Page();
            }
        }
    }
}
