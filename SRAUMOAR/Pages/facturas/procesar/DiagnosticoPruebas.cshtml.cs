using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;
using Microsoft.Extensions.Options;
using SRAUMOAR.Entidades.Generales;
using System.Net.Http;
using System.Text.Json;
using OfficeOpenXml;
using System.IO;

namespace SRAUMOAR.Pages.facturas.procesar
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class DiagnosticoPruebasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfigService _emisorConfig;
        private readonly EmisorConfig _emisor;
        private readonly ICorrelativoService _correlativoService;

        public DiagnosticoPruebasModel(
            SRAUMOAR.Modelos.Contexto context,
            EmisorConfigService emisorConfig,
            IOptions<EmisorConfig> emisorOptions,
            ICorrelativoService correlativoService)
        {
            _context = context;
            _emisorConfig = emisorConfig;
            _emisor = emisorOptions.Value;
            _correlativoService = correlativoService;
        }

        public List<FacturaInfo> FacturasPruebas { get; set; } = new List<FacturaInfo>();

        public class FacturaInfo
        {
            public int FacturaId { get; set; }
            public DateTime Fecha { get; set; }
            public string NumeroControl { get; set; } = string.Empty;
            public string CodigoGeneracion { get; set; } = string.Empty;
            public int TipoDTE { get; set; }
            public decimal TotalPagar { get; set; }
            public string ReceptorNombre { get; set; } = string.Empty;
            public string ReceptorCorreo { get; set; } = string.Empty;
            public bool Anulada { get; set; }
            public bool TieneSello { get; set; }
        }

        public async Task OnGetAsync()
        {
            await CargarFacturasPruebas();
        }

        private async Task CargarFacturasPruebas()
        {
            // Obtener todas las facturas que tienen JsonDte
            var facturas = await _context.Facturas
                .Where(f => !string.IsNullOrEmpty(f.JsonDte))
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();

            FacturasPruebas.Clear();

            foreach (var factura in facturas)
            {
                try
                {
                    var jsonObj = JObject.Parse(factura.JsonDte);
                    var ambiente = jsonObj["identificacion"]?["ambiente"]?.ToString();

                    // Solo incluir facturas con ambiente "00" (pruebas)
                    if (ambiente == "00")
                    {
                        var receptor = jsonObj["receptor"];
                        var facturaInfo = new FacturaInfo
                        {
                            FacturaId = factura.FacturaId,
                            Fecha = factura.Fecha,
                            NumeroControl = factura.NumeroControl,
                            CodigoGeneracion = factura.CodigoGeneracion,
                            TipoDTE = factura.TipoDTE,
                            TotalPagar = factura.TotalPagar,
                            ReceptorNombre = receptor?["nombre"]?.ToString() ?? "N/A",
                            ReceptorCorreo = receptor?["correo"]?.ToString() ?? "N/A",
                            Anulada = factura.Anulada,
                            TieneSello = !string.IsNullOrEmpty(factura.SelloRecepcion)
                        };

                        FacturasPruebas.Add(facturaInfo);
                    }
                }
                catch
                {
                    // Si hay error al parsear el JSON, continuar con la siguiente factura
                    continue;
                }
            }
        }

        // Método para anular una factura de pruebas
        public async Task<IActionResult> OnPostAnularAsync(int id)
        {
            try
            {
                var factura = await _context.Facturas.FindAsync(id);
                if (factura == null)
                {
                    TempData["Error"] = "Factura no encontrada.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Verificar que la factura no esté ya anulada
                if (factura.Anulada)
                {
                    TempData["Error"] = "Esta factura ya ha sido anulada anteriormente.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Verificar que la factura tenga el JSON del DTE
                if (string.IsNullOrEmpty(factura.JsonDte))
                {
                    TempData["Error"] = "Esta factura no tiene el JSON del DTE necesario para la anulación.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Verificar que la factura tenga el sello de recepción
                if (string.IsNullOrEmpty(factura.SelloRecepcion))
                {
                    TempData["Error"] = "Esta factura no tiene el sello de recepción necesario para la anulación.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                var jsonObj = JObject.Parse(factura.JsonDte);
                var ambiente = jsonObj["identificacion"]["ambiente"]?.ToString();

                // Verificar que sea ambiente de pruebas
                if (ambiente != "00")
                {
                    TempData["Error"] = "Esta factura no es de ambiente de pruebas.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Obtener fecha y hora de El Salvador
                var timeZoneElSalvador = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
                var fechaHoraElSalvador = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneElSalvador);

                // Construir JSON de anulación
                var identificacion = new
                {
                    version = 2,
                    ambiente = ambiente,
                    codigoGeneracion = Guid.NewGuid().ToString().ToUpper(),
                    fecAnula = fechaHoraElSalvador.ToString("yyyy-MM-dd"),
                    horAnula = fechaHoraElSalvador.ToString("HH:mm:ss")
                };

                var emisorJson = jsonObj["emisor"];
                string procesarSiNulo(JToken token) => token?.Type == JTokenType.Null ? null : token?.ToString();

                var emisor = new
                {
                    nit = emisorJson["nit"].ToString(),
                    nombre = emisorJson["nombre"].ToString(),
                    tipoEstablecimiento = emisorJson["tipoEstablecimiento"].ToString(),
                    nomEstablecimiento = emisorJson["nombreComercial"].ToString(),
                    codEstableMH = procesarSiNulo(emisorJson["codEstableMH"]) ?? "M001",
                    codEstable = procesarSiNulo(emisorJson["codEstable"]) ?? "M001",
                    codPuntoVentaMH = procesarSiNulo(emisorJson["codPuntoVentaMH"]) ?? "P001",
                    codPuntoVenta = procesarSiNulo(emisorJson["codPuntoVenta"]) ?? "P001",
                    telefono = emisorJson["telefono"].ToString(),
                    correo = emisorJson["correo"].ToString()
                };

                string tipoDocumento = null;
                string numDocumento = null;

                if (factura.TipoDTE == 1)
                {
                    tipoDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["tipoDocumento"]?.ToString()) ? null : jsonObj["receptor"]["tipoDocumento"].ToString();
                    numDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["numDocumento"]?.ToString()) ? null : jsonObj["receptor"]["numDocumento"].ToString();
                }
                else
                {
                    numDocumento = string.IsNullOrEmpty(jsonObj["receptor"]["nit"]?.ToString()) ? null : jsonObj["receptor"]["nit"].ToString();
                    if (!string.IsNullOrEmpty(numDocumento))
                    {
                        if (numDocumento.Length == 14)
                            tipoDocumento = "36";
                        else if (numDocumento.Length == 9)
                            tipoDocumento = "13";
                    }
                }

                var documento = new
                {
                    tipoDte = jsonObj["identificacion"]["tipoDte"].ToString(),
                    codigoGeneracion = jsonObj["identificacion"]["codigoGeneracion"].ToString(),
                    selloRecibido = factura.SelloRecepcion,
                    numeroControl = jsonObj["identificacion"]["numeroControl"].ToString(),
                    fecEmi = jsonObj["identificacion"]["fecEmi"].ToString(),
                    montoIva = factura.TotalIva == 0 ? (decimal?)null : factura.TotalIva,
                    codigoGeneracionR = (string)null,
                    tipoDocumento = tipoDocumento,
                    numDocumento = numDocumento,
                    nombre = jsonObj["receptor"]["nombre"].ToString(),
                    telefono = "00000000",
                    correo = "reclamaciones@dteelsalvador.info"
                };

                var motivo = new
                {
                    tipoAnulacion = 2,
                    motivoAnulacion = "Anulación de factura emitida en ambiente de pruebas",
                    nombreResponsable = "ADMINISTRADOR",
                    tipDocResponsable = "37",
                    numDocResponsable = "200001",
                    nombreSolicita = "Sistema",
                    tipDocSolicita = "37",
                    numDocSolicita = "00001"
                };

                var jsonAnulacion = new JObject
                {
                    { "identificacion", JObject.Parse(JsonConvert.SerializeObject(identificacion)) },
                    { "emisor", JObject.Parse(JsonConvert.SerializeObject(emisor)) },
                    { "documento", JObject.Parse(JsonConvert.SerializeObject(documento)) },
                    { "motivo", JObject.Parse(JsonConvert.SerializeObject(motivo)) }
                };

                string jsonFormateado = jsonAnulacion.ToString(Formatting.None);

                // Enviar anulación a API
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);

                    if (string.IsNullOrEmpty(_emisor.CLAVETESTAPI))
                        throw new Exception("La clave de pruebas de la API no está configurada");
                    if (string.IsNullOrEmpty(_emisor.CLAVETESTCERTIFICADO))
                        throw new Exception("La clave de pruebas del certificado no está configurada");

                    var requestAnulacion = new
                    {
                        Usuario = jsonObj["emisor"]["nit"].ToString(),
                        Password = _emisor.CLAVETESTAPI,
                        Ambiente = ambiente,
                        DteJson = jsonFormateado,
                        Nit = jsonObj["emisor"]["nit"].ToString(),
                        PasswordPrivado = _emisor.CLAVETESTCERTIFICADO
                    };

                    var response = await client.PostAsJsonAsync("http://207.58.153.147:7122/api/anular-dte", requestAnulacion);
                    var responseData = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error al anular DTE. Status: {response.StatusCode}, Response: {responseData}");
                    }

                    var resultado = JsonDocument.Parse(responseData).RootElement;
                    string selloRecibido = resultado.TryGetProperty("selloRecibido", out var sello) ? sello.GetString() : null;

                    // Actualizar factura
                    factura.JsonAnulacion = jsonFormateado;
                    factura.SelloAnulacion = selloRecibido;
                    factura.Anulada = true;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Factura {factura.FacturaId} anulada exitosamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al anular la factura: {ex.Message}";
            }

            await CargarFacturasPruebas();
            return Page();
        }

        // Método para procesar una factura a producción
        public async Task<IActionResult> OnPostProcesarAsync(int id)
        {
            try
            {
                var factura = await _context.Facturas.FindAsync(id);
                if (factura == null)
                {
                    TempData["Error"] = "Factura no encontrada.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Verificar que la factura tenga JsonDte válido
                if (string.IsNullOrEmpty(factura.JsonDte))
                {
                    TempData["Error"] = "Esta factura no tiene el JSON del DTE necesario.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                var jsonObj = JObject.Parse(factura.JsonDte);
                var ambienteActual = jsonObj["identificacion"]["ambiente"]?.ToString();

                // Verificar que sea ambiente de pruebas
                if (ambienteActual != "00")
                {
                    TempData["Error"] = "Esta factura no es de ambiente de pruebas.";
                    await CargarFacturasPruebas();
                    return Page();
                }

                // Guardar código antiguo para actualizar CobrosArancel
                var codigoGeneracionAntiguo = factura.CodigoGeneracion;

                // Modificar ambiente a producción
                jsonObj["identificacion"]["ambiente"] = "01";

                // Generar nuevo código de generación
                var nuevoCodigoGeneracion = Guid.NewGuid().ToString().ToUpper();
                jsonObj["identificacion"]["codigoGeneracion"] = nuevoCodigoGeneracion;

                // Obtener nuevo correlativo de producción
                var tipoDte = jsonObj["identificacion"]["tipoDte"].ToString();
                var nuevoCorrelativo = await _correlativoService.ObtenerSiguienteCorrelativo(tipoDte, "01");
                var numeroFormateado = nuevoCorrelativo.ToString("D15");
                var nuevoNumeroControl = $"DTE-{tipoDte}-M001P001-{numeroFormateado}";
                jsonObj["identificacion"]["numeroControl"] = nuevoNumeroControl;

                // Actualizar fecha y hora
                var fechaHoraActual = DateTime.Now;
                jsonObj["identificacion"]["fecEmi"] = fechaHoraActual.ToString("yyyy-MM-dd");
                jsonObj["identificacion"]["horEmi"] = fechaHoraActual.ToString("HH:mm:ss");

                // Serializar nuevo JsonDte
                var nuevoJsonDte = jsonObj.ToString(Formatting.None);

                // Enviar a API con credenciales de producción
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);

                    if (string.IsNullOrEmpty(_emisor.CLAVEPRODAPI))
                        throw new Exception("La clave de producción de la API no está configurada");
                    if (string.IsNullOrEmpty(_emisor.CLAVEPRODCERTIFICADO))
                        throw new Exception("La clave de producción del certificado no está configurada");

                    var receptor = jsonObj["receptor"];
                    var requestUnificado = new
                    {
                        Usuario = _emisor.NIT,
                        Password = _emisor.CLAVEPRODAPI,
                        Ambiente = "01",
                        DteJson = nuevoJsonDte,
                        Nit = _emisor.NIT,
                        PasswordPrivado = _emisor.CLAVEPRODCERTIFICADO,
                        TipoDte = tipoDte,
                        CodigoGeneracion = nuevoCodigoGeneracion,
                        NumControl = nuevoNumeroControl,
                        VersionDte = int.Parse(jsonObj["identificacion"]["version"]?.ToString() ?? "1"),
                        CorreoCliente = receptor?["correo"]?.ToString() ?? _emisorConfig.EMAIL
                    };

                    var response = await client.PostAsJsonAsync("http://207.58.153.147:7122/api/procesar-dte", requestUnificado);
                    var responseData = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error al procesar DTE. Status: {response.StatusCode}, Response: {responseData}");
                    }

                    var resultado = JsonDocument.Parse(responseData).RootElement;
                    string selloRecibido = resultado.TryGetProperty("selloRecibido", out var sello) ? sello.GetString() : null;

                    if (string.IsNullOrEmpty(selloRecibido))
                    {
                        throw new Exception("No se obtuvo sello de recepción en la respuesta.");
                    }

                    // Actualizar la factura en BD
                    factura.CodigoGeneracion = nuevoCodigoGeneracion;
                    factura.NumeroControl = nuevoNumeroControl;
                    factura.JsonDte = nuevoJsonDte;
                    factura.SelloRecepcion = selloRecibido;
                    factura.Fecha = fechaHoraActual; // Actualizar fecha para que coincida con la fecha del DTE
                    
                    // Limpiar datos de anulación de pruebas ya que ahora está en producción
                    factura.JsonAnulacion = null;
                    factura.SelloAnulacion = null;
                    factura.Anulada = false; // Ya no está anulada porque es una nueva factura en producción

                    // Actualizar CobrosArancel si existe relación
                    var cobroArancel = await _context.CobrosArancel
                        .FirstOrDefaultAsync(c => c.CodigoGeneracion == codigoGeneracionAntiguo);

                    if (cobroArancel != null)
                    {
                        cobroArancel.CodigoGeneracion = nuevoCodigoGeneracion;
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Factura {factura.FacturaId} procesada exitosamente a producción. Nuevo código: {nuevoCodigoGeneracion}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar la factura: {ex.Message}";
            }

            await CargarFacturasPruebas();
            return Page();
        }

        // Método para generar reporte Excel de facturas de pruebas
        public async Task<IActionResult> OnGetGenerarExcelAsync()
        {
            ExcelPackage.License.SetNonCommercialOrganization("SRAUMOAR");
            try
            {
                // Obtener facturas de pruebas directamente de la BD con todos los datos
                var facturas = await _context.Facturas
                    .Where(f => !string.IsNullOrEmpty(f.JsonDte))
                    .OrderByDescending(f => f.Fecha)
                    .ToListAsync();

                var facturasPruebas = new List<Factura>();

                foreach (var factura in facturas)
                {
                    try
                    {
                        var jsonObj = JObject.Parse(factura.JsonDte);
                        var ambiente = jsonObj["identificacion"]?["ambiente"]?.ToString();

                        if (ambiente == "00")
                        {
                            facturasPruebas.Add(factura);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (!facturasPruebas.Any())
                {
                    TempData["Error"] = "No hay facturas de pruebas para generar el reporte Excel";
                    return RedirectToPage();
                }

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Facturas de Pruebas");

                // Configurar encabezados
                var headers = new[] { 
                    "ID", "Fecha", "Estado", "Tipo DTE", "Número Control", 
                    "Código Generación", "Receptor", "Correo Receptor",
                    "Total Gravado", "Total Exento", "IVA", "Total a Pagar",
                    "Tiene Sello", "Anulada"
                };

                // Agregar encabezados
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    worksheet.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Agregar datos
                var facturasOrdenadas = facturasPruebas
                    .OrderBy(f => f.Fecha)
                    .ThenBy(f => f.FacturaId)
                    .ToList();

                for (int row = 0; row < facturasOrdenadas.Count; row++)
                {
                    var factura = facturasOrdenadas[row];
                    var excelRow = row + 2;

                    // Extraer receptor del JSON
                    string receptorNombre = "N/A";
                    string receptorCorreo = "N/A";
                    try
                    {
                        var jsonObj = JObject.Parse(factura.JsonDte);
                        receptorNombre = jsonObj["receptor"]?["nombre"]?.ToString() ?? "N/A";
                        receptorCorreo = jsonObj["receptor"]?["correo"]?.ToString() ?? "N/A";
                    }
                    catch { }

                    worksheet.Cells[excelRow, 1].Value = factura.FacturaId;
                    worksheet.Cells[excelRow, 2].Value = factura.Fecha.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cells[excelRow, 3].Value = factura.Anulada ? "Anulada" : "Activa";
                    
                    var tipoDTEText = factura.TipoDTE switch
                    {
                        1 => "CF - Consumidor Final",
                        3 => "CCF - Crédito Fiscal",
                        5 => "NC - Nota de Crédito",
                        14 => "SE - Sujeto Excluido",
                        15 => "DON - Donación",
                        _ => factura.TipoDTE.ToString()
                    };
                    worksheet.Cells[excelRow, 4].Value = tipoDTEText;
                    worksheet.Cells[excelRow, 5].Value = factura.NumeroControl;
                    worksheet.Cells[excelRow, 6].Value = factura.CodigoGeneracion;
                    worksheet.Cells[excelRow, 7].Value = receptorNombre;
                    worksheet.Cells[excelRow, 8].Value = receptorCorreo;
                    worksheet.Cells[excelRow, 9].Value = factura.TotalGravado;
                    worksheet.Cells[excelRow, 10].Value = factura.TotalExento;
                    worksheet.Cells[excelRow, 11].Value = factura.TotalIva;
                    worksheet.Cells[excelRow, 12].Value = factura.TotalPagar;
                    worksheet.Cells[excelRow, 13].Value = !string.IsNullOrEmpty(factura.SelloRecepcion) ? "Sí" : "No";
                    worksheet.Cells[excelRow, 14].Value = factura.Anulada ? "Sí" : "No";

                    // Aplicar formato de moneda a las columnas de montos
                    worksheet.Cells[excelRow, 9].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 10].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 11].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 12].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 12].Style.Font.Bold = true;

                    // Aplicar bordes
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cells[excelRow, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                }

                // Agregar totales
                var lastRow = facturasPruebas.Count + 2;
                var facturasActivas = facturasPruebas.Where(f => !f.Anulada).ToList();
                var totalGravado = facturasActivas.Sum(f => f.TotalGravado);
                var totalExento = facturasActivas.Sum(f => f.TotalExento);
                var totalIva = facturasActivas.Sum(f => f.TotalIva);
                var totalGeneral = facturasActivas.Sum(f => f.TotalPagar);

                worksheet.Cells[lastRow + 1, 8].Value = "Total Gravado:";
                worksheet.Cells[lastRow + 1, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 1, 9].Value = totalGravado;
                worksheet.Cells[lastRow + 1, 9].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 2, 8].Value = "Total Exento:";
                worksheet.Cells[lastRow + 2, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 2, 10].Value = totalExento;
                worksheet.Cells[lastRow + 2, 10].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 3, 8].Value = "Total IVA:";
                worksheet.Cells[lastRow + 3, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 3, 11].Value = totalIva;
                worksheet.Cells[lastRow + 3, 11].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 4, 8].Value = "Total General:";
                worksheet.Cells[lastRow + 4, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 4, 12].Value = totalGeneral;
                worksheet.Cells[lastRow + 4, 12].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[lastRow + 4, 12].Style.Font.Bold = true;

                // Ajustar ancho de columnas
                worksheet.Cells.AutoFitColumns();

                // Generar archivo
                var memoryStream = new MemoryStream();
                package.SaveAs(memoryStream);
                memoryStream.Position = 0;

                var fileName = $"FacturasPruebas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
                
                return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte Excel: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
