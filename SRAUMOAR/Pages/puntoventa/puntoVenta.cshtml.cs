using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SRAUMOAR.Entidades;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Servicios;
using System.Text.Json;

namespace SRAUMOAR.Pages.puntoventa
{
    public class puntoVentaModel : PageModel
    {
      
            [BindProperty]
            public FacturaViewModel Factura { get; set; } = new FacturaViewModel();
        private readonly ICorrelativoService _correlativoService;
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfig _emisor;
        public puntoVentaModel(SRAUMOAR.Modelos.Contexto context, IOptions<EmisorConfig> emisorOptions, ICorrelativoService correlativoService)
        {
            _context = context;
            _emisor = emisorOptions.Value;
            _correlativoService = correlativoService;
        }
        public void OnGet()
            {
                // Aquí cargarías los datos del emisor desde tu fuente de datos
                CargarDatosEmisor();
            }

            public IActionResult OnPostAgregarProducto()
            {
                // Verificar que se haya seleccionado un tipo de documento
                if (string.IsNullOrEmpty(Factura.TipoDocumento))
                {
                    ModelState.AddModelError("Factura.TipoDocumento", "Debe seleccionar un tipo de documento antes de agregar productos.");
                    CargarDatosEmisor();
                    return Page();
                }

                // Validar código de país para donaciones
                if (Factura.TipoDocumento == "15" && string.IsNullOrEmpty(Factura.CodigoPais))
                {
                    ModelState.AddModelError("Factura.CodigoPais", "Debe seleccionar un código de país para documentos de donación.");
                    CargarDatosEmisor();
                    return Page();
                }

                // Validar campos requeridos para Crédito Fiscal
                if (Factura.TipoDocumento == "02")
                {
                    if (string.IsNullOrEmpty(Factura.Receptor.Nit))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nit", "El NIT del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.Nrc))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nrc", "El NRC del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.CodActividad))
                    {
                        ModelState.AddModelError("Factura.Receptor.CodActividad", "El código de actividad económica del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.DescActividad))
                    {
                        ModelState.AddModelError("Factura.Receptor.DescActividad", "El giro del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                }

                // Validar campos requeridos para Consumidor Final
                if (Factura.TipoDocumento == "01")
                {
                    if (string.IsNullOrEmpty(Factura.Receptor.Nombre))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nombre", "El nombre del receptor es requerido para Consumidor Final.");
                        CargarDatosEmisor();
                        return Page();
                    }
                }

                if (!string.IsNullOrEmpty(Factura.CodigoProducto) &&
                    !string.IsNullOrEmpty(Factura.DescripcionProducto) &&
                    Factura.CantidadProducto > 0 &&
                    Factura.PrecioProducto > 0)
                {
                    var nuevoProducto = new ProductoVenta
                    {
                        Id = Factura.Productos.Count + 1,
                        Codigo = Factura.CodigoProducto,
                        Descripcion = Factura.DescripcionProducto,
                        Cantidad = Factura.CantidadProducto,
                        PrecioUnitario = Factura.PrecioProducto,
                        EsExento = Factura.ProductoExento
                    };

                    Factura.Productos.Add(nuevoProducto);

                    // Limpiar solo los campos del producto, no todo el ModelState
                    ModelState.Remove("Factura.CodigoProducto");
                    ModelState.Remove("Factura.DescripcionProducto");
                    ModelState.Remove("Factura.CantidadProducto");
                    ModelState.Remove("Factura.PrecioProducto");
                    ModelState.Remove("Factura.ProductoExento");

                    // Limpiar el formulario para nuevos valores
                    Factura.CodigoProducto = string.Empty;
                    Factura.DescripcionProducto = string.Empty;
                    Factura.CantidadProducto = 0;
                    Factura.PrecioProducto = 0;
                    Factura.ProductoExento = false;
                }

                return Page();
            }

            public IActionResult OnPostEliminarProducto(int id)
            {
                // Preservar datos del receptor
                var receptorActual = Factura.Receptor;
                
                var producto = Factura.Productos.FirstOrDefault(p => p.Id == id);
                if (producto != null)
                {
                    Factura.Productos.Remove(producto);
                }

                // Restaurar datos del receptor
                if (receptorActual != null)
                {
                    Factura.Receptor = receptorActual;
                }

                return Page();
            }

            public async Task<IActionResult> OnPostProcesarFacturaAsync()
            {
                // Limpiar campos de producto del ModelState ya que están vacíos porque los productos ya fueron agregados
                ModelState.Remove("Factura.CodigoProducto");
                ModelState.Remove("Factura.DescripcionProducto");
                ModelState.Remove("Factura.CantidadProducto");
                ModelState.Remove("Factura.PrecioProducto");
                ModelState.Remove("Factura.ProductoExento");
                ModelState.Remove("Factura.CodigoPais");

                // Validar que se haya seleccionado un tipo de documento
                if (string.IsNullOrEmpty(Factura.TipoDocumento))
                {
                    ModelState.AddModelError("Factura.TipoDocumento", "Debe seleccionar un tipo de documento antes de procesar la factura.");
                    CargarDatosEmisor();
                    return Page();
                }

                // Validar código de país para donaciones
                if (Factura.TipoDocumento == "15" && string.IsNullOrEmpty(Factura.CodigoPais))
                {
                    ModelState.AddModelError("Factura.CodigoPais", "Debe seleccionar un código de país para documentos de donación.");
                    CargarDatosEmisor();
                    return Page();
                }

                // Validar campos requeridos para Crédito Fiscal
                if (Factura.TipoDocumento == "02")
                {
                    if (string.IsNullOrEmpty(Factura.Receptor.Nit))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nit", "El NIT del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.Nrc))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nrc", "El NRC del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.CodActividad))
                    {
                        ModelState.AddModelError("Factura.Receptor.CodActividad", "El código de actividad económica del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                    
                    if (string.IsNullOrEmpty(Factura.Receptor.DescActividad))
                    {
                        ModelState.AddModelError("Factura.Receptor.DescActividad", "El giro del receptor es requerido para Crédito Fiscal.");
                        CargarDatosEmisor();
                        return Page();
                    }
                }
                
                // Validar campos requeridos para Consumidor Final
                if (Factura.TipoDocumento == "01")
                {
                    if (string.IsNullOrEmpty(Factura.Receptor.Nombre))
                    {
                        ModelState.AddModelError("Factura.Receptor.Nombre", "El nombre del receptor es requerido para Consumidor Final.");
                        CargarDatosEmisor();
                        return Page();
                    }
                }

                // Validar que haya productos agregados
                if (!Factura.Productos.Any())
                {
                    ModelState.AddModelError("", "Debe agregar al menos un producto antes de procesar la factura.");
                    CargarDatosEmisor();
                    return Page();
                }

                if (ModelState.IsValid)
                {
                if (Factura.TipoDocumento == "01")
                {
                    // CONSUMIDOR FINAL
                    //****************************************************
                    //CREACION DEL DTE
                    //****************************************************



                    string dteJson = "";
                    Guid codigoGeneracion = Guid.NewGuid();

                    Random random = new Random();
                    int i = random.Next(1, 100001); // Genera un número aleatorio entre 1 y 100000
                    string correlativo = i.ToString().PadLeft(15, '0'); // Rellena con ceros a la izquierda para que tenga 15 caracteres
                                                                        // Generar número de control

                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("01", "01"); ;
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "01" + "-" + "U0000001" + "-" + numeroFormateado;

                    // ESQUEMA PARA UN DTE DE CONSUMIDOR FINAL DTE 01

                    var identificacion = new
                    {
                        version = 1,
                        ambiente = "00",
                        tipoDte = "01",
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        tipoContingencia = (string)null,
                        motivoContin = (string)null,
                        fecEmi = DateTime.Now.ToString("yyyy-MM-dd"),
                        horEmi = DateTime.Now.ToString("HH:mm:ss"),
                        tipoMoneda = "USD"
                    };

                    // Crear el objeto para el emisor
                    var emisor = new
                    {
                        nit = _emisor.NIT,
                        nrc = _emisor.NRC,
                        nombre = _emisor.NOMBRE,
                        codActividad = _emisor.CODACTIVIDAD,
                        descActividad = _emisor.GIRO,
                        nombreComercial = _emisor.NOMBRECOMERCIAL,
                        tipoEstablecimiento = "02",
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        codEstableMH = (string)null,
                        codEstable = (string)null,
                        codPuntoVentaMH = (string)null,
                        codPuntoVenta = (string)null,
                        correo = _emisor.EMAIL
                    };

                    // Crear el objeto para el receptor
                    var receptor = new
                    {
                        tipoDocumento = "37",
                        numDocumento = (string)null,
                        nrc = (string)null,
                        nombre = Factura.Receptor.Nombre,
                        codActividad = (string)null,
                        descActividad = (string)null,
                        direccion = new
                        {
                            departamento = Factura.Receptor.CodigoDepartamento,
                            municipio = Factura.Receptor.CodigoMunicipio,
                            complemento = Factura.Receptor.Direccion.Complemento
                        },
                        telefono = Factura.Receptor.Telefono,
                        correo = Factura.Receptor.Correo
                    };

                    

                    // Crear el cuerpo del documento usando los datos de los aranceles obtenidos
                    var cuerpoDocumento = Factura.Productos
                        .Select((arancel, index) => new
                        {
                            numItem = index + 1,
                            tipoItem = 1,
                            numeroDocumento = (string)null,
                            cantidad = 1,
                            codigo = arancel.Codigo,
                            codTributo = (string)null,
                            uniMedida = 59,
                            descripcion = arancel.Descripcion,
                            precioUni = arancel.PrecioUnitario,
                            montoDescu = 0.0,
                            ventaNoSuj = 0.0,
                            ventaExenta = arancel.EsExento ? arancel.SubTotal : 0.0m,
                            ventaGravada = !arancel.EsExento ? arancel.SubTotal : 0.0m,
                            tributos = (string)null,
                            psv = arancel.PrecioUnitario,
                            noGravado = 0.0,
                            ivaItem = !arancel.EsExento ? Math.Round(arancel.SubTotal - (arancel.SubTotal / 1.13m), 2) : 0.0m
                        })
                        .ToArray();

                    decimal totalVenta = Factura.Productos.Where(a => a.EsExento == false).Sum(a => a.SubTotal);
                    decimal totalVentaExenta = Factura.Productos.Where(a => a.EsExento == true).Sum(a => a.SubTotal);

                    //ivaItem = Math.Round(arancelescostos[index] - (arancelescostos[index] / 1.13m), 6)
                    // Crear el resumen
                    // Calcular las variables primero
                    // decimal totalVentaExenta = arancelescostos.Sum();
                    //   decimal totalVenta = 0;
                    decimal subTotalVentas = totalVenta + totalVentaExenta;
                    decimal subTotal = totalVenta + totalVentaExenta;
                    decimal montoTotalOperacion = totalVenta + totalVentaExenta;
                    decimal totalPagar = totalVenta + totalVentaExenta;
                    string totalLetras = new Conversor().ConvertirNumeroALetras(totalPagar);
                    decimal totalIva = cuerpoDocumento.Sum(item => item.ivaItem);
                    //decimal totalIva = Math.Round(totalVenta - (totalVenta / 1.13m), 2);

                    // Crear el objeto resumen con las variables
                    var resumen = new
                    {
                        totalNoSuj = 0.00,
                        totalExenta = totalVentaExenta,
                        totalGravada = totalVenta,
                        subTotalVentas = subTotalVentas,
                        descuNoSuj = 0.0,
                        descuExenta = 0.0,
                        descuGravada = 0.0,
                        porcentajeDescuento = 0,
                        totalDescu = 0.0,
                        tributos = (string)null,
                        subTotal = subTotal,
                        ivaRete1 = 0.00,
                        reteRenta = 0.0,
                        montoTotalOperacion = montoTotalOperacion,
                        totalNoGravado = 0.0,
                        totalPagar = totalPagar,
                        totalLetras = totalLetras,
                        totalIva = totalIva,
                        saldoFavor = 0.0,
                        condicionOperacion = 1,
                        pagos = new[]
                        {
                     new
                    {
                        codigo = "01",
                        montoPago = totalPagar,
                        referencia = "0000",
                        periodo = (string)null,
                        plazo = (string)null
                    }
                },
                        numPagoElectronico = "0"
                    };

                    // Crear la extensión
                    var extension = new
                    {
                        nombEntrega = "ENCARGADO 1",
                        docuEntrega = "00000000-0",
                        nombRecibe = (string)null,
                        docuRecibe = (string)null,
                        observaciones = (string)null,
                        placaVehiculo = (string)null
                    };

                    // Serializar el objeto JSON completo
                    dteJson = JsonSerializer.Serialize(new
                    {
                        identificacion,
                        documentoRelacionado = (string)null,
                        emisor,
                        receptor,
                        ventaTercero = (string)null,
                        cuerpoDocumento,
                        resumen,
                        extension,
                        otrosDocumentos = (string)null,
                        apendice = (string)null
                    });


                    //ENVIO A LA API
                    var requestUnificado = new
                    {
                        Usuario = _emisor.NIT,
                        Password = _emisor.CLAVETESTAPI,
                        Ambiente = "00",
                        DteJson = dteJson,
                        Nit = _emisor.NIT,
                        PasswordPrivado = _emisor.CLAVETESTCERTIFICADO,
                        TipoDte = "01",
                        CodigoGeneracion = codigoGeneracion,
                        NumControl = numeroControl,
                        VersionDte = 1,
                        CorreoCliente = Factura.Receptor.Correo
                    };
                    var selloRecibido = "";
                    using (HttpClient client = new HttpClient())
                    {
                        // LLAMADA ÚNICA
                        var response = client.PostAsJsonAsync("http://207.58.153.147:7122/api/procesar-dte", requestUnificado).Result;
                        var responseData = response.Content.ReadAsStringAsync().Result;

                        if (!response.IsSuccessStatusCode)
                            throw new Exception($"Error al procesar DTE: {responseData}");

                        var resultado = JsonDocument.Parse(responseData).RootElement;
                        var selloRecepcion = resultado.TryGetProperty("selloRecibido", out var sello)
                            ? sello.GetString()
                            : null;
                        selloRecibido = selloRecepcion;
                    }


                    Factura factura = new Factura();
                    factura.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                    factura.NumeroControl = numeroControl;
                    factura.SelloRecepcion = selloRecibido;
                    factura.JsonDte = dteJson;
                    string fechaHoraString = $"{identificacion.fecEmi} {identificacion.horEmi}";
                    DateTime fechaHora = DateTime.ParseExact(fechaHoraString, "yyyy-MM-dd HH:mm:ss", null);
                    factura.Fecha = fechaHora;
                    factura.TipoDTE = identificacion.tipoDte == "01" ? 1 : 2; // Asumiendo que 01 es Factura y 03 CCF
                    factura.TotalGravado = totalVenta;
                    factura.TotalExento = totalVentaExenta;
                    factura.TotalIva = totalIva;
                    factura.TotalPagar = totalPagar;
                    _context.Facturas.Add(factura);
                    await _context.SaveChangesAsync();

                    //****************************************************
                    //FIN CREACION DEL DTE
                    //****************************************************
                }
                else if (Factura.TipoDocumento == "02")
                {
                    // CRÉDITO FISCAL
                    //****************************************************
                    //CREACION DEL DTE
                    //****************************************************

                    string dteJson = "";
                    Guid codigoGeneracion = Guid.NewGuid();

                    // Generar número de control
                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("02", "01");
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "02" + "-" + "U0000001" + "-" + numeroFormateado;

                    // ESQUEMA PARA UN DTE DE CRÉDITO FISCAL DTE 02

                    var identificacion = new
                    {
                        version = 1,
                        ambiente = "00",
                        tipoDte = "02",
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        tipoContingencia = (string)null,
                        motivoContin = (string)null,
                        fecEmi = DateTime.Now.ToString("yyyy-MM-dd"),
                        horEmi = DateTime.Now.ToString("HH:mm:ss"),
                        tipoMoneda = "USD"
                    };

                    // Crear el objeto para el emisor
                    var emisor = new
                    {
                        nit = _emisor.NIT,
                        nrc = _emisor.NRC,
                        nombre = _emisor.NOMBRE,
                        codActividad = _emisor.CODACTIVIDAD,
                        descActividad = _emisor.GIRO,
                        nombreComercial = _emisor.NOMBRECOMERCIAL,
                        tipoEstablecimiento = "02",
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        codEstableMH = (string)null,
                        codEstable = (string)null,
                        codPuntoVentaMH = (string)null,
                        codPuntoVenta = (string)null,
                        correo = _emisor.EMAIL
                    };

                    // Crear el objeto para el receptor (con datos de actividad económica)
                    var receptor = new
                    {
                        nit = Factura.Receptor.Nit,
                        nrc = Factura.Receptor.Nrc,
                        nombre = Factura.Receptor.Nombre,
                        codActividad = Factura.Receptor.CodActividad,
                        descActividad = Factura.Receptor.DescActividad,
                        nombreComercial = Factura.Receptor.NombreComercial,
                        direccion = new
                        {
                            departamento = Factura.Receptor.CodigoDepartamento,
                            municipio = Factura.Receptor.CodigoMunicipio,
                            complemento = Factura.Receptor.Direccion.Complemento
                        },
                        telefono = Factura.Receptor.Telefono,
                        correo = Factura.Receptor.Correo
                    };

                    // Crear el cuerpo del documento
                    var cuerpoDocumento = Factura.Productos
                        .Select((producto, index) => new
                        {
                            numItem = index + 1,
                            tipoItem = 1,
                            numeroDocumento = (string)null,
                            cantidad = producto.Cantidad,
                            codigo = producto.Codigo,
                            codTributo = (string)null,
                            uniMedida = 59,
                            descripcion = producto.Descripcion,
                            precioUni = producto.PrecioUnitario,
                            montoDescu = 0.0,
                            ventaNoSuj = 0.0,
                            ventaExenta = producto.EsExento ? producto.SubTotal : 0.0m,
                            ventaGravada = !producto.EsExento ? producto.SubTotal : 0.0m,
                            tributos = new[]
                            {
                                new
                                {
                                    codigo = "20",
                                    descripcion = "IVA",
                                    valor = !producto.EsExento ? Math.Round(producto.SubTotal * 0.13m, 2) : 0.0m
                                }
                            },
                            psv = producto.PrecioUnitario,
                            noGravado = 0.0,
                            ivaItem = !producto.EsExento ? Math.Round(producto.SubTotal * 0.13m, 2) : 0.0m
                        })
                        .ToArray();

                    decimal totalVenta = Factura.Productos.Where(p => !p.EsExento).Sum(p => p.SubTotal);
                    decimal totalVentaExenta = Factura.Productos.Where(p => p.EsExento).Sum(p => p.SubTotal);
                    decimal totalIva = Factura.Productos.Where(p => !p.EsExento).Sum(p => p.SubTotal * 0.13m);

                    decimal subTotalVentas = totalVenta + totalVentaExenta;
                    decimal subTotal = totalVenta + totalVentaExenta;
                    decimal montoTotalOperacion = totalVenta + totalVentaExenta + totalIva;
                    decimal totalPagar = totalVenta + totalVentaExenta + totalIva;
                    string totalLetras = new Conversor().ConvertirNumeroALetras(totalPagar);

                    // Crear el objeto resumen
                    var resumen = new
                    {
                        totalNoSuj = 0.00,
                        totalExenta = totalVentaExenta,
                        totalGravada = totalVenta,
                        subTotalVentas = subTotalVentas,
                        descuNoSuj = 0.0,
                        descuExenta = 0.0,
                        descuGravada = 0.0,
                        porcentajeDescuento = 0,
                        totalDescu = 0.0,
                        tributos = new[]
                        {
                            new
                            {
                                codigo = "20",
                                descripcion = "IVA",
                                valor = totalIva
                            }
                        },
                        subTotal = subTotal,
                        ivaRete1 = 0.00,
                        reteRenta = 0.0,
                        montoTotalOperacion = montoTotalOperacion,
                        totalNoGravado = 0.0,
                        totalPagar = totalPagar,
                        totalLetras = totalLetras,
                        totalIva = totalIva,
                        saldoFavor = 0.0,
                        condicionOperacion = 1,
                        pagos = new[]
                        {
                            new
                            {
                                codigo = "01",
                                montoPago = totalPagar,
                                referencia = "0000",
                                periodo = (string)null,
                                plazo = (string)null
                            }
                        },
                        numPagoElectronico = "0"
                    };

                    // Crear la extensión
                    var extension = new
                    {
                        nombEntrega = "ENCARGADO 1",
                        docuEntrega = "00000000-0",
                        nombRecibe = (string)null,
                        docuRecibe = (string)null,
                        observaciones = (string)null,
                        placaVehiculo = (string)null
                    };

                    // Serializar el objeto JSON completo
                    dteJson = JsonSerializer.Serialize(new
                    {
                        identificacion,
                        documentoRelacionado = (string)null,
                        emisor,
                        receptor,
                        ventaTercero = (string)null,
                        cuerpoDocumento,
                        resumen,
                        extension,
                        otrosDocumentos = (string)null,
                        apendice = (string)null
                    });

                    //ENVIO A LA API
                    var requestUnificado = new
                    {
                        Usuario = _emisor.NIT,
                        Password = _emisor.CLAVEPRODAPI,
                        Ambiente = "01",
                        DteJson = dteJson,
                        Nit = _emisor.NIT,
                        PasswordPrivado = _emisor.CLAVEPRODCERTIFICADO,
                        TipoDte = "02",
                        CodigoGeneracion = codigoGeneracion,
                        NumControl = numeroControl,
                        VersionDte = 1,
                        CorreoCliente = Factura.Receptor.Correo
                    };

                    var selloRecibido = "";
                    using (HttpClient client = new HttpClient())
                    {
                        // LLAMADA ÚNICA
                        var response = client.PostAsJsonAsync("http://207.58.153.147:7122/api/procesar-dte", requestUnificado).Result;
                        var responseData = response.Content.ReadAsStringAsync().Result;

                        if (!response.IsSuccessStatusCode)
                            throw new Exception($"Error al procesar DTE: {responseData}");

                        var resultado = JsonDocument.Parse(responseData).RootElement;
                        var selloRecepcion = resultado.TryGetProperty("selloRecibido", out var sello)
                            ? sello.GetString()
                            : null;
                        selloRecibido = selloRecepcion;
                    }

                    Factura factura = new Factura();
                    factura.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                    factura.NumeroControl = numeroControl;
                    factura.SelloRecepcion = selloRecibido;
                    factura.JsonDte = dteJson;
                    string fechaHoraString = $"{identificacion.fecEmi} {identificacion.horEmi}";
                    DateTime fechaHora = DateTime.ParseExact(fechaHoraString, "yyyy-MM-dd HH:mm:ss", null);
                    factura.Fecha = fechaHora;
                    factura.TipoDTE = 2; // Crédito Fiscal
                    factura.TotalGravado = totalVenta;
                    factura.TotalExento = totalVentaExenta;
                    factura.TotalIva = totalIva;
                    factura.TotalPagar = totalPagar;
                    _context.Facturas.Add(factura);
                    await _context.SaveChangesAsync();

                    //****************************************************
                    //FIN CREACION DEL DTE
                    //****************************************************
                }

                TempData["Success"] = "Factura procesada exitosamente";
                return RedirectToPage("../facturas/Index");
            }

                return Page();
            }

            private void CargarDatosEmisor()
            {
                // Preservar datos del receptor si ya existen
                var receptorActual = Factura.Receptor;
                
                // Ejemplo de carga de datos del emisor
                Factura.Emisor = new Emisor
                {
                    Nit = "04331410931010",
                    Nrc = "1029819",
                    Nombre = "Universidad Monseñor Oscar Arnulfo Romero",
                    CodActividad = "85499",
                    DescActividad = "Enseñanza superior universitaria",
                    NombreComercial = "UMOAR",
                    TipoEstablecimiento = "02",
                    Direccion = new Direccion
                    {
                        Departamento = "04",
                        Municipio = "35",
                        Complemento = "KM 52 1/2 CARRETERA TEJUTLA, CHAlATENANGO"
                    },
                    Telefono = "23021800",
                    Correo = "umoar@enviosdte.email"
                };
                
                // Restaurar datos del receptor si existían
                if (receptorActual != null)
                {
                    Factura.Receptor = receptorActual;
                }
            }
        }
    }

