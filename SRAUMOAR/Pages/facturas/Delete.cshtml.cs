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
                Factura = factura;
                var venta = Factura;

                //**************************************************
               
                var jsonObj = JObject.Parse(venta.JsonDte); // OBTENER EL JSON ORIGINAL PARA LEERLO EN CADA SECCION

                // Sección de Identificación
                var identificacion = new
                {
                    version = 2,
                    ambiente = jsonObj["identificacion"]["ambiente"].ToString(),
                    codigoGeneracion = Guid.NewGuid().ToString().ToUpper(),
                    fecAnula = DateTime.Now.ToString("yyyy-MM-dd"),
                    horAnula = DateTime.Now.ToString("HH:mm:ss")
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
                    montoIva = (string)null,
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



                try
                {
                    using (HttpClient client = new HttpClient())
                    {



                        // Obtener el ambiente del JSON
                        string ambiente = jsonObj["identificacion"]["ambiente"].ToString();

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

                        //  var json2 = JsonConvert.SerializeObject(requestUnificado);
                        //  Console.WriteLine(json2); // Verifica que el JSON esté bien

                        // LLAMADA ÚNICA a la API
                        var response = client.PostAsJsonAsync("http://207.58.153.147:7122/api/anular-dte", requestAnulacion).Result;
                        var responseData = response.Content.ReadAsStringAsync().Result;

                        //*****************

                            if (!response.IsSuccessStatusCode)
                            throw new Exception($"Error al procesar DTE : {responseData}");

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
                catch (Exception)
                {
                    return RedirectToPage("./Index");
                }
                //*****************************************************
                // Actualizar solo los campos específicos
                _context.Entry(Factura).Property(f => f.JsonAnulacion).IsModified = true;
                _context.Entry(Factura).Property(f => f.SelloAnulacion).IsModified = true;
                _context.Entry(Factura).Property(f => f.Anulada).IsModified = true;

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
