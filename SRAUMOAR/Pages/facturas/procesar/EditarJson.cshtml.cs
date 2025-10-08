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
using System.Net.Http;
using System.Text.Json;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.facturas.procesar
{
    public class EditarJsonModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfigService _emisorConfig;

        public EditarJsonModel(SRAUMOAR.Modelos.Contexto context, EmisorConfigService emisorConfig)
        {
            _context = context;
            _emisorConfig = emisorConfig;
        }

        public Factura Factura { get; set; } = default!;

        [BindProperty]
        public string JsonEditado { get; set; } = string.Empty;

        public string JsonFormateado { get; set; } = string.Empty;

        // Propiedades para mostrar resultado de consulta
        public string ConsultaResultado { get; set; } = string.Empty;
        public string ConsultaEstado { get; set; } = string.Empty;
        public string ConsultaMensaje { get; set; } = string.Empty;
        public string ConsultaSello { get; set; } = string.Empty;
        public List<string> ConsultaErrores { get; set; } = new List<string>();

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
            JsonEditado = factura.JsonDte ?? string.Empty;
            
            // Formatear JSON para mostrar
            try
            {
                if (!string.IsNullOrEmpty(JsonEditado))
                {
                    var jsonObject = JsonConvert.DeserializeObject(JsonEditado);
                    JsonFormateado = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al formatear JSON: {ex.Message}";
                JsonFormateado = JsonEditado;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            try
            {
                // Debug: Verificar ModelState
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    TempData["Error"] = $"Error de validación del modelo: {errors}";
                    return await OnGetAsync(id);
                }

                // Verificar que el JSON no esté vacío
                if (string.IsNullOrWhiteSpace(JsonEditado))
                {
                    TempData["Error"] = "El JSON no puede estar vacío.";
                    return await OnGetAsync(id);
                }

                // Validar que el JSON sea válido
                try
                {
                    JsonConvert.DeserializeObject(JsonEditado);
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    TempData["Error"] = $"JSON inválido: {ex.Message}";
                    return await OnGetAsync(id);
                }

                var factura = await _context.Facturas.FindAsync(id);
                if (factura == null)
                {
                    TempData["Error"] = "Factura no encontrada.";
                    return RedirectToPage("./Index");
                }

                // Verificar que la factura esté pendiente
                if (!string.IsNullOrEmpty(factura.SelloRecepcion))
                {
                    TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede ser procesada.";
                    return RedirectToPage("./Index");
                }

                // Actualizar el JSON
                factura.JsonDte = JsonEditado;

                await _context.SaveChangesAsync();
                TempData["Success"] = "JSON actualizado correctamente. Redirigiendo al proceso de contingencia...";
                
                // Redirigir a la página de envío por contingencia
                return RedirectToPage("./EnviarContingencia", new { id = factura.FacturaId });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                TempData["Error"] = $"Error de concurrencia al actualizar la factura: {ex.Message}";
                return await OnGetAsync(id);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado: {ex.Message}";
                return await OnGetAsync(id);
            }
        }

        public async Task<IActionResult> OnPostConsultarAsync(int id)
        {
            try
            {
                var factura = await _context.Facturas.FindAsync(id);
                if (factura == null)
                {
                    TempData["Error"] = "Factura no encontrada.";
                    return await OnGetAsync(id);
                }

                if (string.IsNullOrEmpty(factura.JsonDte))
                {
                    TempData["Error"] = "No hay JSON asociado a esta factura para consultar.";
                    return await OnGetAsync(id);
                }

                var jsonObj = JObject.Parse(factura.JsonDte);

                // Crear solicitud de consulta (JSON vacío como especificaste)
                var solicitudConsulta = new
                {
                    Usuario = _emisorConfig.NIT,
                    Password = _emisorConfig.PasswordAPI,
                    Ambiente = _emisorConfig.AmbienteString,
                    DteJson = "{}", // JSON vacío como especificaste
                    TipoDte = jsonObj["identificacion"]?["tipoDte"]?.ToString() ?? string.Empty,
                    Nit = _emisorConfig.NIT,
                    CodigoGeneracion = jsonObj["identificacion"]?["codigoGeneracion"]?.ToString() ?? string.Empty
                };

                using (var client = new HttpClient())
                {
                    var response = await client.PostAsJsonAsync($"{_emisorConfig.ApiUrl}/api/consulta-dte", solicitudConsulta);
                    var responseData = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["Error"] = $"Error en consulta: {responseData}";
                        return await OnGetAsync(id);
                    }

                    var resultado = JsonDocument.Parse(responseData).RootElement;
                    
                    // Extraer información de la respuesta
                    ConsultaEstado = resultado.TryGetProperty("estado", out var estado) ? estado.GetString() ?? "" : "";
                    ConsultaMensaje = resultado.TryGetProperty("descripcionMsg", out var desc) ? desc.GetString() ?? "" : "";
                    ConsultaSello = resultado.TryGetProperty("selloRecibido", out var sello) ? sello.GetString() ?? "" : "";
                    ConsultaResultado = responseData;

                    // Extraer errores de observaciones
                    ConsultaErrores.Clear();
                    if (resultado.TryGetProperty("observaciones", out var observaciones) && observaciones.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var obs in observaciones.EnumerateArray())
                        {
                            if (obs.ValueKind == JsonValueKind.String)
                            {
                                ConsultaErrores.Add(obs.GetString() ?? "");
                            }
                        }
                    }

                    // Verificar si la factura ya fue procesada
                    if (ConsultaEstado == "PROCESADO" && ConsultaMensaje == "RECIBIDO" && !string.IsNullOrEmpty(ConsultaSello))
                    {
                        // Si el sello es diferente al que tenemos, actualizarlo
                        if (factura.SelloRecepcion != ConsultaSello)
                        {
                            factura.SelloRecepcion = ConsultaSello;
                            await _context.SaveChangesAsync();
                            TempData["Success"] = $"¡Factura ya procesada! Sello actualizado: {ConsultaSello}";
                        }
                        else
                        {
                            TempData["Info"] = $"Factura ya procesada. Sello: {ConsultaSello}";
                        }
                    }
                    else
                    {
                        TempData["Info"] = $"Consulta realizada. Estado: {ConsultaEstado}, Mensaje: {ConsultaMensaje}";
                    }
                }

                return await OnGetAsync(id);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error durante la consulta: {ex.Message}";
                return await OnGetAsync(id);
            }
        }

        private bool FacturaExists(int id)
        {
            return _context.Facturas.Any(e => e.FacturaId == id);
        }
    }
}
