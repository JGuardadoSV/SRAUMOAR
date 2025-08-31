using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Generales;
using Microsoft.Extensions.Options;

namespace SRAUMOAR.Pages.facturas
{
    public class DeleteModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfig _emisor;
        public DeleteModel(SRAUMOAR.Modelos.Contexto context,IOptions<EmisorConfig> emisorOptions)
        {
            _context = context;
            _emisor = emisorOptions.Value;
        }
        public static DateTime ObtenerFechaHoraElSalvador()
        {
            try
            {
                var timeZoneElSalvador = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneElSalvador);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback si no se encuentra la zona horaria
                return DateTime.UtcNow.AddHours(-6);
            }
        }
        [BindProperty]
        public Factura Factura { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas.FirstOrDefaultAsync(m => m.FacturaId == id);

            if (factura is not null)
            {
                Factura = factura;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas.FindAsync(id);
            if (factura != null)
            {
                // Verificar que la factura no esté ya anulada
                if (factura.Anulada)
                {
                    TempData["ErrorAnulacion"] = "Esta factura ya ha sido anulada anteriormente.";
                    return RedirectToPage("./Index");
                }

                // Verificar que la factura tenga el JSON del DTE
                if (string.IsNullOrEmpty(factura.JsonDte))
                {
                    TempData["ErrorAnulacion"] = "Esta factura no tiene el JSON del DTE necesario para la anulación.";
                    return RedirectToPage("./Index");
                }

                // Verificar que la factura tenga el sello de recepción
                if (string.IsNullOrEmpty(factura.SelloRecepcion))
                {
                    TempData["ErrorAnulacion"] = "Esta factura no tiene el sello de recepción necesario para la anulación.";
                    return RedirectToPage("./Index");
                }

                Factura = factura;
                var venta = Factura;

                //**************************************************
               
                var jsonObj = JObject.Parse(venta.JsonDte); // OBTENER EL JSON ORIGINAL PARA LEERLO EN CADA SECCION

                // Validar que el JSON original tenga la estructura esperada
                if (jsonObj == null || !jsonObj.HasValues)
                {
                    throw new Exception("El JSON del DTE original está vacío o es inválido");
                }

                // Validar campos críticos del JSON original
                if (jsonObj["identificacion"] == null || jsonObj["emisor"] == null || jsonObj["receptor"] == null)
                {
                    throw new Exception("El JSON del DTE no tiene la estructura esperada (identificacion, emisor, receptor)");
                }

                // Validar que el emisor tenga las claves necesarias
                if (string.IsNullOrEmpty(jsonObj["emisor"]["nit"]?.ToString()))
                {
                    throw new Exception("El NIT del emisor no puede estar vacío");
                }

                // Validar que el emisor esté configurado en la base de datos
                if (_emisor == null)
                {
                    throw new Exception("La configuración del emisor no está disponible en la base de datos");
                }

                // Validar que el ambiente sea válido
                var ambiente = jsonObj["identificacion"]["ambiente"]?.ToString();
                if (string.IsNullOrEmpty(ambiente) || (ambiente != "00" && ambiente != "01"))
                {
                    throw new Exception($"El ambiente del DTE no es válido: {ambiente}");
                }

                // Sección de Identificación
                // Opción 1: Usando TimeZoneInfo (recomendado)
                var timeZoneElSalvador = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
                var fechaHoraElSalvador = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneElSalvador);

                var identificacion = new
                {
                    version = 2,
                    ambiente = ambiente,
                    codigoGeneracion = Guid.NewGuid().ToString().ToUpper(),
                    fecAnula = fechaHoraElSalvador.ToString("yyyy-MM-dd"),
                    horAnula = fechaHoraElSalvador.ToString("HH:mm:ss")
                };

                // Sección de Emisor
                var emisorJson = jsonObj["emisor"];
                string procesarSiNulo(JToken token) => token?.Type == JTokenType.Null ? null : token?.ToString();

                var emisor = new
                {
                    nit = emisorJson["nit"].ToString(),
                    nombre = emisorJson["nombre"].ToString(),
                    tipoEstablecimiento = emisorJson["tipoEstablecimiento"].ToString(),
                    nomEstablecimiento = emisorJson["nombreComercial"].ToString(),
                    codEstableMH = procesarSiNulo(emisorJson["codEstableMH"]),
                    codEstable = procesarSiNulo(emisorJson["codEstable"]),
                    codPuntoVentaMH = procesarSiNulo(emisorJson["codPuntoVentaMH"]),
                    codPuntoVenta = procesarSiNulo(emisorJson["codPuntoVenta"]),
                    telefono = emisorJson["telefono"].ToString(),
                    correo = emisorJson["correo"].ToString()
                };
                string tipoDocumento = (string)null; // asumiendo que es otro documento
                string numDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["nit"]?.ToString()) ? null : jsonObj["receptor"]["nit"].ToString();
                
                if (venta.TipoDTE == 1)
                { // en consumidor final
                    tipoDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["tipoDocumento"]?.ToString()) ? null : jsonObj["receptor"]["tipoDocumento"].ToString();
                    numDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["numDocumento"]?.ToString()) ? null : jsonObj["receptor"]["numDocumento"].ToString();
                }
                else
                {
                    //en credito fiscal puede usar el nit o el dui homologado
                    numDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["nit"]?.ToString()) ? null : jsonObj["receptor"]["nit"].ToString();

                    if (numDocumento.Length == 14)
                    {
                        tipoDocumento = "36";
                    }
                    if (numDocumento.Length == 9)
                    {
                        tipoDocumento = "13";
                    }

                }
                // Sección de Documento
                var documento = new
                {
                    tipoDte = jsonObj["identificacion"]["tipoDte"].ToString(),
                    codigoGeneracion = jsonObj["identificacion"]["codigoGeneracion"].ToString(),
                    selloRecibido = venta.SelloRecepcion,
                    numeroControl = jsonObj["identificacion"]["numeroControl"].ToString(),
                    fecEmi = jsonObj["identificacion"]["fecEmi"].ToString(),
                    montoIva = venta.TotalIva == 0 ? (decimal?)null : venta.TotalIva,
                codigoGeneracionR = (string)null,
                    tipoDocumento = tipoDocumento,
                    numDocumento = numDocumento,
                    nombre = jsonObj["receptor"]["nombre"].ToString(),
                    telefono = "00000000",
                    correo = "reclamaciones@dteelsalvador.info"
                };

                // Sección de Motivo
                var motivo = new
                {
                    tipoAnulacion = 2,
                    motivoAnulacion = "Anulación solicitada por el cliente",
                    nombreResponsable = "CAJERO",
                    tipDocResponsable = "37",
                    numDocResponsable = "200001",
                    nombreSolicita = "Cliente",
                    tipDocSolicita = "37",
                    numDocSolicita = "00001"
                };

                // Construcción del JSON final
                // Versión simplificada usando Newtonsoft.Json
                var jsonAnulacion = new JObject
            {
                { "identificacion", JObject.Parse(JsonConvert.SerializeObject(identificacion)) },
                { "emisor", JObject.Parse(JsonConvert.SerializeObject(emisor)) },
                { "documento", JObject.Parse(JsonConvert.SerializeObject(documento)) },
                { "motivo", JObject.Parse(JsonConvert.SerializeObject(motivo)) }
            };
                string jsonFormateado = jsonAnulacion.ToString(Newtonsoft.Json.Formatting.None);

                // Validar que el JSON de anulación no esté vacío
                if (string.IsNullOrEmpty(jsonFormateado))
                {
                    throw new Exception("El JSON de anulación generado está vacío");
                }

                // Log del JSON de anulación para debugging
                Console.WriteLine($"[DEBUG] JSON de anulación generado: {jsonFormateado}");



                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        // Agregar timeout para evitar bloqueos
                        client.Timeout = TimeSpan.FromMinutes(2);

                        // Validar que las claves del emisor estén configuradas
                        if (ambiente == "01")
                        {
                            if (string.IsNullOrEmpty(_emisor.CLAVEPRODAPI))
                                throw new Exception("La clave de producción de la API no está configurada");
                            if (string.IsNullOrEmpty(_emisor.CLAVEPRODCERTIFICADO))
                                throw new Exception("La clave de producción del certificado no está configurada");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(_emisor.CLAVETESTAPI))
                                throw new Exception("La clave de pruebas de la API no está configurada");
                            if (string.IsNullOrEmpty(_emisor.CLAVETESTCERTIFICADO))
                                throw new Exception("La clave de pruebas del certificado no está configurada");
                        }

                        // Crear el request según el ambiente
                        var requestAnulacion = ambiente == "01"
                            ? new // Ambiente de Producción (01)
                            {
                                Usuario = jsonObj["emisor"]["nit"].ToString(),
                                Password = _emisor.CLAVEPRODAPI, // Clave de producción
                                Ambiente = ambiente,
                                DteJson = jsonFormateado,
                                Nit = jsonObj["emisor"]["nit"].ToString(),
                                PasswordPrivado = _emisor.CLAVEPRODCERTIFICADO, // Clave de producción
                            }
                            : new // Ambiente de Pruebas (00)
                            {
                                Usuario = jsonObj["emisor"]["nit"].ToString(),
                                Password = _emisor.CLAVETESTAPI, // Clave de pruebas
                                Ambiente = ambiente,
                                DteJson = jsonFormateado,
                                Nit = jsonObj["emisor"]["nit"].ToString(),
                                PasswordPrivado = _emisor.CLAVETESTCERTIFICADO, // Clave de pruebas
                            };

                        // Log del request para debugging
                        var requestJson = JsonConvert.SerializeObject(requestAnulacion);
                        Console.WriteLine($"[DEBUG] Request de anulación: {requestJson}");

                        // LLAMADA ÚNICA a la API
                        var response = client.PostAsJsonAsync("http://207.58.153.147:7122/api/anular-dte", requestAnulacion).Result;
                        var responseData = response.Content.ReadAsStringAsync().Result;

                        Console.WriteLine($"[DEBUG] Response Status: {response.StatusCode}");
                        Console.WriteLine($"[DEBUG] Response Content: {responseData}");

                        //*****************
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorMessage = $"Error al procesar DTE. Status: {response.StatusCode}, Response: {responseData}";
                            Console.WriteLine($"[ERROR] {errorMessage}");
                            throw new Exception(errorMessage);
                        }

                        var resultado2 = JsonDocument.Parse(responseData).RootElement;
                        string selloRecibido = resultado2.TryGetProperty("selloRecibido", out var sello2)
                            ? sello2.GetString()
                            : null;

                        // Actualizar los datos del proceso de anulacion
                        Factura.JsonAnulacion = jsonFormateado;
                        Factura.SelloAnulacion = selloRecibido;
                        Factura.Anulada = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Excepción durante la anulación: {ex.Message}");
                    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                    
                    // Agregar el error a TempData para mostrarlo en la vista
                    TempData["ErrorAnulacion"] = $"Error al anular la factura: {ex.Message}";
                    
                    return RedirectToPage("./Index");
                }
                //*****************************************************
                // Actualizar solo los campos específicos
                _context.Entry(Factura).Property(f => f.JsonAnulacion).IsModified = true;
                _context.Entry(Factura).Property(f => f.SelloAnulacion).IsModified = true;
                _context.Entry(Factura).Property(f => f.Anulada).IsModified = true;


                var arancel = await _context.CobrosArancel.FirstOrDefaultAsync(c => c.CodigoGeneracion == jsonObj["identificacion"]["codigoGeneracion"].ToString());
                if (arancel != null)
                {
                    _context.CobrosArancel.Remove(arancel);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
