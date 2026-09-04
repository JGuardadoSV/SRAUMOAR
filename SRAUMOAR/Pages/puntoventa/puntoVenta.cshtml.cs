using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SRAUMOAR.Entidades;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Servicios;
using SRAUMOAR.Servicios.FacturacionElectronica;
using SRAUMOAR.Modelos.FacturacionElectronica;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SRAUMOAR.Pages.puntoventa
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class puntoVentaModel : PageModel
    {
        private int ambiente = 1;

        [BindProperty]
        public FacturaViewModel Factura { get; set; } = new FacturaViewModel();
        [BindProperty] public Guid OperacionEmisionId { get; set; }
        public IReadOnlyList<CatalogoFeItem> CatalogoDepartamentos { get; private set; } = Array.Empty<CatalogoFeItem>();
        public IReadOnlyList<CatalogoFeItem> CatalogoMunicipios { get; private set; } = Array.Empty<CatalogoFeItem>();
        public IReadOnlyList<CatalogoFeItem> CatalogoDistritos { get; private set; } = Array.Empty<CatalogoFeItem>();
        public IReadOnlyList<CatalogoFeItem> CatalogoPaises { get; private set; } = Array.Empty<CatalogoFeItem>();
        public IReadOnlyList<CatalogoFeItem> CatalogoDonaciones { get; private set; } = Array.Empty<CatalogoFeItem>();
        private readonly ICorrelativoService _correlativoService;
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfig _emisor;
        private readonly IDteTransmisionService _dteTransmision;
        private readonly IConfiguracionFeService _configuracionFe;
        private readonly ICatalogoFeService _catalogos;
        private readonly IDteEmailQueueService _emailQueue;
        private readonly IDteJsonFactory _jsonFactory;
        private readonly IDteEmisionRecuperacionService _recuperacionEmision;
        private string ambienteFiscal = "01";
        private string codigoEstablecimientoMh = "M001";
        private string codigoEstablecimiento = "M001";
        private string codigoPuntoVentaMh = "P001";
        private string codigoPuntoVenta = "P001";
        public puntoVentaModel(SRAUMOAR.Modelos.Contexto context, IOptions<EmisorConfig> emisorOptions, ICorrelativoService correlativoService, IDteTransmisionService dteTransmision, IConfiguracionFeService configuracionFe, ICatalogoFeService catalogos, IDteEmailQueueService emailQueue, IDteJsonFactory jsonFactory, IDteEmisionRecuperacionService recuperacionEmision)
        {
            _context = context;
            _emisor = emisorOptions.Value;
            _correlativoService = correlativoService;
            _dteTransmision = dteTransmision;
            _configuracionFe = configuracionFe;
            _catalogos = catalogos;
            _emailQueue = emailQueue;
            _jsonFactory = jsonFactory;
            _recuperacionEmision = recuperacionEmision;
        }
        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            OperacionEmisionId = Guid.NewGuid();
            await CargarCatalogosAsync(cancellationToken);
            try
            {
                var configuracion = await _configuracionFe.ObtenerActivaAsync(cancellationToken);
                Factura.Emisor = CrearEmisorDesdeConfiguracion(configuracion.Entidad);
                return;
            }
            catch
            {
                // Permite abrir la pantalla antes de aplicar la migracion o completar la configuracion fiscal.
            }

            // Aquí cargarías los datos del emisor desde tu fuente de datos
            CargarDatosEmisor();
        }
        public string DeterminarTipoDocumento(string nit)
        {
            string nitLimpio = Regex.Replace(nit, @"[\s-]", "");
            if (nitLimpio.Length == 9) return "13";  // DUI
            if (nitLimpio.Length == 14) return "36"; // NIT
            return "37"; // Otro
        }
        public async Task<IActionResult> OnPostAgregarProducto()
        {
            await CargarCatalogosAsync(CancellationToken.None);
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

            // Validar tipo de donación para donaciones
            if (Factura.TipoDocumento == "15" && string.IsNullOrEmpty(Factura.TipoDonacion))
            {
                ModelState.AddModelError("Factura.TipoDonacion", "Debe seleccionar un tipo de donación.");
                CargarDatosEmisor();
                return Page();
            }

            // Validar campos requeridos para Crédito Fiscal
            if (Factura.TipoDocumento is "02" or "03")
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

                // Limpiar solo los campos del producto, no los de retención ni tipo de operación
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

        public async Task<IActionResult> OnPostEliminarProducto(int id)
        {
            await CargarCatalogosAsync(CancellationToken.None);
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
            if (OperacionEmisionId == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty, "La operación perdió su identificador seguro. Recargue el formulario antes de emitir.");
                CargarDatosEmisor();
                return Page();
            }
            var claveOperacion = $"EMISION-PV:{OperacionEmisionId:N}";
            var recuperada = await _recuperacionEmision.ReanudarAsync(claveOperacion);
            if (recuperada is not null)
            {
                TempData[recuperada.Aceptado ? "Success" : "Error"] = recuperada.Mensaje;
                return RedirectToPage("../facturas/Index");
            }
            await CargarCatalogosAsync(CancellationToken.None);
            ConfiguracionFeActiva configuracionFiscal;
            EmisorConfig _emisor;
            try
            {
                configuracionFiscal = await _configuracionFe.ObtenerActivaAsync();
                _emisor = DteEmisorConfigMapper.Crear(configuracionFiscal.Entidad);
                ambienteFiscal = configuracionFiscal.Entidad.AmbienteFiscal;
                ambiente = ambienteFiscal == "01" ? 1 : 0;
                codigoEstablecimientoMh = configuracionFiscal.Entidad.CodigoEstablecimientoMh;
                codigoEstablecimiento = configuracionFiscal.Entidad.CodigoEstablecimiento;
                codigoPuntoVentaMh = configuracionFiscal.Entidad.CodigoPuntoVentaMh;
                codigoPuntoVenta = configuracionFiscal.Entidad.CodigoPuntoVenta;
                // El emisor se obtiene de configuración, nunca de los campos ocultos del navegador.
                Factura.Emisor = CrearEmisorDesdeConfiguracion(configuracionFiscal.Entidad);
                foreach (var clave in ModelState.Keys.Where(x => x.StartsWith("Factura.Emisor.", StringComparison.Ordinal)).ToArray())
                    ModelState.Remove(clave);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se puede procesar el DTE: {ex.Message}";
                CargarDatosEmisor();
                return Page();
            }

            // Limpiar campos de producto del ModelState ya que están vacíos porque los productos ya fueron agregados
            ModelState.Remove("Factura.CodigoProducto");
            ModelState.Remove("Factura.DescripcionProducto");
            ModelState.Remove("Factura.CantidadProducto");
            ModelState.Remove("Factura.PrecioProducto");
            ModelState.Remove("Factura.ProductoExento");
            ModelState.Remove("Factura.CodigoPais");
            ModelState.Remove("Factura.TipoDonacion");

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

            // Validar tipo de donación para donaciones
            if (Factura.TipoDocumento == "15" && string.IsNullOrEmpty(Factura.TipoDonacion))
            {
                ModelState.AddModelError("Factura.TipoDonacion", "Debe seleccionar un tipo de donación.");
                CargarDatosEmisor();
                return Page();
            }

            // Validar campos requeridos para Crédito Fiscal
            if (Factura.TipoDocumento is "02" or "03")
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

            // Validar campos para Sujeto Excluido
            if (Factura.TipoDocumento == "14")
            {
                if (string.IsNullOrEmpty(Factura.TipoOperacion))
                {
                    ModelState.AddModelError("Factura.TipoOperacion", "Debe seleccionar si es Bien o Servicio.");
                    CargarDatosEmisor();
                    return Page();
                }
                if (Factura.Retencion == null)
                {
                    Factura.Retencion = false;
                }
            }
            else
            {
                // Limpiar valores si no es Sujeto Excluido
                Factura.TipoOperacion = null;
                Factura.Retencion = false;
                ModelState.Remove("Factura.TipoOperacion");
                ModelState.Remove("Factura.Retencion");
            }

            string distritoReceptor;
            if (Factura.TipoDocumento == "15" && Factura.Receptor.CodigoDomicilioFiscal is not (1 or 2))
            {
                ModelState.AddModelError(string.Empty, "Seleccione el domicilio fiscal del donante; no se deduce del país de nacionalidad.");
                return Page();
            }
            try
            {
                distritoReceptor = DteDocumentoActualizador.ResolverDistritoReceptor(
                    Factura.Receptor.CodigoDepartamento, Factura.Receptor.CodigoMunicipio, Factura.Receptor.DistritoId,
                    CatalogoDepartamentos, CatalogoMunicipios, CatalogoDistritos);
                Factura.Receptor.Direccion.Departamento = Factura.Receptor.CodigoDepartamento;
                Factura.Receptor.Direccion.Municipio = Factura.Receptor.CodigoMunicipio;
                ModelState.Remove("Factura.Receptor.Direccion.Departamento");
                ModelState.Remove("Factura.Receptor.Direccion.Municipio");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }

            if (Factura.TipoDocumento is not ("01" or "02" or "03" or "14" or "15"))
            {
                ModelState.AddModelError(string.Empty, "Tipo de documento no soportado.");
                return Page();
            }

            if (ModelState.IsValid)
            {
                //se corrige si viene @gmail o @umoar y no terminan en .com o .edu.sv respectivamente
                Factura.Receptor.Correo = CorregirCorreo(Factura.Receptor.Correo);
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

                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("01", ambiente == 1 ? "01" : "00", codigoEstablecimiento, codigoPuntoVenta);
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "01" + "-" + codigoEstablecimiento + codigoPuntoVenta + "-" + numeroFormateado;

                    // ESQUEMA PARA UN DTE DE CONSUMIDOR FINAL DTE 01
                    string fecEmi = DateTime.Now.ToString("yyyy-MM-dd");
                    string horEmi = DateTime.Now.ToString("HH:mm:ss");
                    try
                    {
                        registroDTE registroDTE = new registroDTE();
                        registroDTE.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                        registroDTE.NumControl = numeroControl;
                        registroDTE.Fecha = DateOnly.Parse(fecEmi);
                        registroDTE.Hora = TimeOnly.Parse(horEmi);
                        registroDTE.Tipo = "01";
                        _context.registroDTEs.Add(registroDTE);
                        _context.SaveChanges();
                    }
                    catch (Exception)
                    {

                        // throw;
                    }

                    var identificacion = new
                    {
                        version = 1,
                        ambiente = ambiente == 1 ? "01" : "00",
                        tipoDte = "01",
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        tipoContingencia = (string)null,
                        motivoContin = (string)null,
                        fecEmi = fecEmi,
                        horEmi = horEmi,
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
                        tipoEstablecimiento = configuracionFiscal.Entidad.TipoEstablecimiento,
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            distrito = configuracionFiscal.Entidad.CodigoDistrito,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        codEstableMH = codigoEstablecimientoMh,
                        codEstable = codigoEstablecimiento,
                        codPuntoVentaMH = codigoPuntoVentaMh,
                        codPuntoVenta = codigoPuntoVenta,
                        correo = _emisor.EMAIL
                    };
                    var nitLimpio = Factura.Receptor.Nit?.Replace("-", "").Trim();

                    // Crear el objeto para el receptor
                    var receptor = new
                    {
                        tipoDocumento = string.IsNullOrWhiteSpace(nitLimpio)
                        ? "37"
                        : nitLimpio.Length == 9
                            ? "13"
                            : nitLimpio.Length == 14
                                ? "36"
                                : "37",
                        numDocumento = string.IsNullOrWhiteSpace(nitLimpio)
                        ? (string)null
                        : nitLimpio.Length == 9
                            ? nitLimpio.Insert(8, "-")
                            : nitLimpio,
                        nrc = (string)null,
                        nombre = Factura.Receptor.Nombre,
                        codActividad = (string)null,
                        descActividad = (string)null,
                        direccion = new
                        {
                            departamento = Factura.Receptor.CodigoDepartamento,
                            municipio = Factura.Receptor.CodigoMunicipio,
                            distrito = distritoReceptor,
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
                            cantidad = arancel.Cantidad,
                            codigo = arancel.Codigo,
                            codTributo = (string)null,
                            uniMedida = 59,
                            descripcion = arancel.Descripcion,
                            precioUni = arancel.PrecioUnitario,
                            montoDescu = 0.0,
                            ventaNoSuj = 0.0,
                            ventaExenta = arancel.EsExento ? Math.Round(arancel.SubTotal, 2) : 0.0m,
                            ventaGravada = !arancel.EsExento ? Math.Round(arancel.SubTotal, 2) : 0.0m,
                            tributos = (string)null,
                            psv = arancel.PrecioUnitario,
                            noGravado = 0.0,
                            ivaItem = !arancel.EsExento ? Math.Round(Math.Round(arancel.SubTotal, 2) - (Math.Round(arancel.SubTotal, 2) / 1.13m), 2) : 0.0m
                        })
                        .ToArray();

                    decimal totalVenta = Factura.Productos.Where(a => a.EsExento == false).Sum(a => Math.Round(a.SubTotal, 2));
                    decimal totalVentaExenta = Factura.Productos.Where(a => a.EsExento == true).Sum(a => Math.Round(a.SubTotal, 2));

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


                    if (!PrepararBorrador(ref dteJson)) return Page();
                    var factura = await GuardarFacturaPreparadaAsync(dteJson, codigoGeneracion, numeroControl,
                        identificacion.fecEmi, identificacion.horEmi, 1, "01", Factura.Receptor.Correo,
                        totalVenta, totalVentaExenta, totalIva, totalPagar);
                    if (factura is null) return OperacionYaProcesada();
                    var resultadoTransmision = await _dteTransmision.TransmitirAsync(
                        new DteTransmisionRequest(dteJson, "01", codigoGeneracion.ToString().ToUpperInvariant(), numeroControl, 2,
                            Factura.Receptor.Correo, factura.FacturaId));
                    await GuardarResultadoTransmisionAsync(factura, resultadoTransmision);
                    await NotificarResultadoAsync(factura, resultadoTransmision);
                    //****************************************************
                    //FIN CREACION DEL DTE
                    //****************************************************
                }
                else if (Factura.TipoDocumento is "02" or "03")
                {
                    // CRÉDITO FISCAL
                    //****************************************************
                    //CREACION DEL DTE
                    //****************************************************

                    string dteJson = "";
                    Guid codigoGeneracion = Guid.NewGuid();

                    // Generar número de control

                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("03", ambiente == 1 ? "01" : "00", codigoEstablecimiento, codigoPuntoVenta);
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "03" + "-" + codigoEstablecimiento + codigoPuntoVenta + "-" + numeroFormateado;

                    string fecEmi = DateTime.Now.ToString("yyyy-MM-dd");
                    string horEmi = DateTime.Now.ToString("HH:mm:ss");
                    try
                    {
                        registroDTE registroDTE = new registroDTE();
                        registroDTE.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                        registroDTE.NumControl = numeroControl;
                        registroDTE.Fecha = DateOnly.Parse(fecEmi);
                        registroDTE.Hora = TimeOnly.Parse(horEmi);
                        registroDTE.Tipo = "02";
                        _context.registroDTEs.Add(registroDTE);
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {

                    }
                    // ESQUEMA PARA UN DTE DE CRÉDITO FISCAL DTE 02

                    var identificacion = new
                    {
                        version = 3,
                        ambiente = ambiente == 1 ? "01" : "00",
                        tipoDte = "03",
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        tipoContingencia = (string)null,
                        motivoContin = (string)null,
                        fecEmi = fecEmi,
                        horEmi = horEmi,
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
                        tipoEstablecimiento = configuracionFiscal.Entidad.TipoEstablecimiento,
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            distrito = configuracionFiscal.Entidad.CodigoDistrito,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        codEstableMH = codigoEstablecimientoMh,
                        codEstable = codigoEstablecimiento,
                        codPuntoVentaMH = codigoPuntoVentaMh,
                        codPuntoVenta = codigoPuntoVenta,
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
                            distrito = distritoReceptor,
                            complemento = Factura.Receptor.Direccion.Complemento
                        },
                        telefono = Factura.Receptor.Telefono,
                        correo = Factura.Receptor.Correo
                    };

                    // Crear el cuerpo del documento
                    var cuerpoDocumento = Factura.Productos
                        .Select((producto, index) =>
                        {
                            // Para Crédito Fiscal, el validador del sistema exige múltiplos de 0.01.
                            // Redondeamos el subtotal por línea a 2 decimales para que el total sea consistente.
                            var subTotalProducto = Math.Round(producto.SubTotal, 2);

                            return new
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
                                ventaExenta = producto.EsExento ? subTotalProducto : 0.0m,
                                ventaGravada = !producto.EsExento ? subTotalProducto : 0.0m,
                                tributos = producto.EsExento ? null : new[] { "20" },
                                psv = producto.PrecioUnitario,
                                noGravado = 0.0
                            };
                        })
                        .ToArray();

                    decimal totalVenta = Factura.Productos
                        .Where(p => !p.EsExento)
                        .Sum(p => Math.Round(p.SubTotal, 2));
                    decimal totalVentaExenta = Factura.Productos
                        .Where(p => p.EsExento)
                        .Sum(p => Math.Round(p.SubTotal, 2));

                    decimal totalIva = Factura.Productos
                        .Where(p => !p.EsExento)
                        // IVA por línea: redondeamos a 2 decimales antes de sumar.
                        .Sum(p => Math.Round(Math.Round(p.SubTotal, 2) * 0.13m, 2));

                    decimal subTotalVentas = Math.Round(totalVenta + totalVentaExenta, 2);
                    decimal subTotal = subTotalVentas;
                    decimal montoTotalOperacion = Math.Round(subTotalVentas + totalIva, 2);
                    decimal totalPagar = montoTotalOperacion;
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
                                descripcion = "Impuesto al Valor Agregado 13%",
                                valor = Math.Round(totalIva, 2)
                            }
                        },
                        subTotal = subTotal,
                        ivaRete1 = 0.00,
                        ivaPerci1 = Math.Round(0.00, 2),
                        reteRenta = 0.0,
                        montoTotalOperacion = montoTotalOperacion,
                        totalNoGravado = 0.0,
                        totalPagar = totalPagar,
                        totalLetras = totalLetras,
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

                    if (!PrepararBorrador(ref dteJson)) return Page();
                    var factura = await GuardarFacturaPreparadaAsync(dteJson, codigoGeneracion, numeroControl,
                        identificacion.fecEmi, identificacion.horEmi, 3, "03", Factura.Receptor.Correo,
                        totalVenta, totalVentaExenta, totalIva, totalPagar);
                    if (factura is null) return OperacionYaProcesada();
                    var resultadoTransmision = await _dteTransmision.TransmitirAsync(
                        new DteTransmisionRequest(dteJson, "03", codigoGeneracion.ToString().ToUpperInvariant(), numeroControl, 4,
                            Factura.Receptor.Correo, factura.FacturaId));
                    await GuardarResultadoTransmisionAsync(factura, resultadoTransmision);
                    await NotificarResultadoAsync(factura, resultadoTransmision);
                    //****************************************************
                    //FIN CREACION DEL DTE
                    //****************************************************
                }

                else if (Factura.TipoDocumento == "14")
                {
                    //SUJETO EXCLUIDO
                    string dteJson = "";
                    Guid codigoGeneracion = Guid.NewGuid();

                    // Generar número de control

                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("14", ambiente == 1 ? "01" : "00", codigoEstablecimiento, codigoPuntoVenta);
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "14" + "-" + codigoEstablecimiento + codigoPuntoVenta + "-" + numeroFormateado;

                    string fecEmi = DateTime.Now.ToString("yyyy-MM-dd");
                    string horEmi = DateTime.Now.ToString("HH:mm:ss");
                    try
                    {
                        registroDTE registroDTE = new registroDTE();
                        registroDTE.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                        registroDTE.NumControl = numeroControl;
                        registroDTE.Fecha = DateOnly.Parse(fecEmi);
                        registroDTE.Hora = TimeOnly.Parse(horEmi);
                        registroDTE.Tipo = "14";
                        _context.registroDTEs.Add(registroDTE);
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {

                    }
                    // Crear el objeto para la identificación
                    var identificacion = new
                    {
                        version = 1,
                        ambiente = ambiente == 1 ? "01" : "00",
                        tipoDte = "14", // Tipo 14 para Sujeto Excluido
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        tipoContingencia = (string)null,
                        motivoContin = (string)null,
                        fecEmi = fecEmi,
                        horEmi = horEmi,
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
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            distrito = configuracionFiscal.Entidad.CodigoDistrito,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        codEstableMH = codigoEstablecimientoMh,
                        codEstable = codigoEstablecimiento, // Ejemplo: "F001"
                        codPuntoVentaMH = codigoPuntoVentaMh,
                        codPuntoVenta = codigoPuntoVenta, // Ejemplo: "C001"
                        correo = _emisor.EMAIL
                    };

                    // Crear el objeto para el sujeto excluido
                    var sujetoExcluido = new
                    {
                        tipoDocumento = DeterminarTipoDocumento(Factura.Receptor.Nit), // "13" para DUI
                        numDocumento = Factura.Receptor.Nit,
                        nombre = Factura.Receptor.Nombre,
                        codActividad = (string)null,
                        descActividad = (string)null,
                        direccion = new
                        {
                            departamento = Factura.Receptor.CodigoDepartamento,
                            municipio = Factura.Receptor.CodigoMunicipio,
                            distrito = distritoReceptor,
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
                            tipoItem = string.Equals(Factura.TipoOperacion, "Servicio", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
                            cantidad = producto.Cantidad,
                            codigo = producto.Codigo,
                            uniMedida = 59, // Unidad de medida estándar
                            descripcion = producto.Descripcion,
                            precioUni = producto.PrecioUnitario,
                            montoDescu = 0,
                            compra = Math.Round(producto.Cantidad * producto.PrecioUnitario, 2)
                        })
                        .ToArray();
                    bool retencion = Factura.Retencion;
                    // Calcular totales
                    decimal totalCompra = cuerpoDocumento.Sum(item => (decimal)item.compra);
                    decimal totalDescuento = cuerpoDocumento.Sum(item => (decimal)item.montoDescu);
                    decimal subTotal = totalCompra;
                    decimal ivaRete1 = 0; // Para sujeto excluido generalmente es 0
                    decimal reteRenta = retencion ? Math.Round(totalCompra * 0.10m, 2) : 0; // totalCompra*0.10; // Retención de renta si aplica
                    decimal totalPagar = subTotal - reteRenta;
                    string totalLetras = new Conversor().ConvertirNumeroALetras(totalPagar);

                    // Crear el objeto resumen
                    var resumen = new
                    {
                        totalCompra = Math.Round(totalCompra, 2),
                        descu = Math.Round(totalDescuento, 2),
                        totalDescu = Math.Round(totalDescuento, 2),
                        subTotal = Math.Round(subTotal, 2),
                        ivaRete1 = Math.Round(ivaRete1, 2),
                        reteRenta = Math.Round(reteRenta, 2),
                        totalPagar = Math.Round(totalPagar, 2),
                        totalLetras = totalLetras,
                        condicionOperacion = 1,
                        pagos = new[]
                         {
                            new
                            {
                                codigo = "01",
                                montoPago = Math.Round(totalPagar, 2),
                                referencia = "0000",
                                periodo = (string)null,
                                plazo = (string)null
                            }
                        },
                        observaciones = (string)null
                    };

                    // Crear el apéndice (opcional)
                    var apendice = (string)null;

                    // Serializar el objeto JSON completo para DTE Sujeto Excluido
                    dteJson = JsonSerializer.Serialize(new
                    {
                        identificacion,
                        emisor,
                        sujetoExcluido,
                        cuerpoDocumento,
                        resumen,
                        apendice
                    });

                    if (!PrepararBorrador(ref dteJson)) return Page();
                    var factura = await GuardarFacturaPreparadaAsync(dteJson, codigoGeneracion, numeroControl,
                        identificacion.fecEmi, identificacion.horEmi, 14, "14", Factura.Receptor.Correo,
                        0m, 0m, 0m, totalPagar);
                    if (factura is null) return OperacionYaProcesada();
                    var resultadoTransmision = await _dteTransmision.TransmitirAsync(
                        new DteTransmisionRequest(dteJson, "14", codigoGeneracion.ToString().ToUpperInvariant(), numeroControl, 2,
                            Factura.Receptor.Correo, factura.FacturaId));
                    await GuardarResultadoTransmisionAsync(factura, resultadoTransmision);
                    await NotificarResultadoAsync(factura, resultadoTransmision);
                }
                else if (Factura.TipoDocumento == "15")
                {
                    //COMPROBANTE DE DONACIÓN
                    string dteJson = "";
                    Guid codigoGeneracion = Guid.NewGuid();

                    // Generar número de control
                    int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("15", ambiente == 1 ? "01" : "00", codigoEstablecimiento, codigoPuntoVenta);
                    string numeroFormateado = numero.ToString("D15");
                    string numeroControl = "DTE-" + "15" + "-" + codigoEstablecimiento + codigoPuntoVenta + "-" + numeroFormateado;

                    string fecEmi = DateTime.Now.ToString("yyyy-MM-dd");
                    string horEmi = DateTime.Now.ToString("HH:mm:ss");
                    try
                    {
                        registroDTE registroDTE = new registroDTE();
                        registroDTE.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
                        registroDTE.NumControl = numeroControl;
                        registroDTE.Fecha = DateOnly.Parse(fecEmi);
                        registroDTE.Hora = TimeOnly.Parse(horEmi);
                        registroDTE.Tipo = "15";
                        _context.registroDTEs.Add(registroDTE);
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {

                    }

                    // Crear el objeto para la identificación
                    var identificacion = new
                    {
                        version = 1,
                        ambiente = ambiente == 1 ? "01" : "00",
                        tipoDte = "15", // Tipo 15 para Comprobante de Donación
                        numeroControl = numeroControl,
                        codigoGeneracion = codigoGeneracion.ToString().ToUpper(),
                        tipoModelo = 1,
                        tipoOperacion = 1,
                        fecEmi = fecEmi,
                        horEmi = horEmi,
                        tipoMoneda = "USD"
                    };

                    // Crear el objeto para el donatario (equivalente al emisor)
                    var donatario = new
                    {
                        tipoDocumento = "36", // Tipo fijo para donatarios
                        numDocumento = _emisor.NIT,
                        nrc = _emisor.NRC,
                        nombre = _emisor.NOMBRE,
                        codActividad = _emisor.CODACTIVIDAD,
                        descActividad = _emisor.GIRO,
                        nombreComercial = _emisor.NOMBRECOMERCIAL,
                        tipoEstablecimiento = configuracionFiscal.Entidad.TipoEstablecimiento,
                        direccion = new
                        {
                            departamento = _emisor.DEPARTAMENTO,
                            municipio = _emisor.DISTRITO,
                            distrito = configuracionFiscal.Entidad.CodigoDistrito,
                            complemento = _emisor.DIRECCION
                        },
                        telefono = _emisor.TELEFONO,
                        correo = _emisor.EMAIL,
                        codEstableMH = codigoEstablecimientoMh,
                        codEstable = codigoEstablecimiento,
                        codPuntoVentaMH = codigoPuntoVentaMh,
                        codPuntoVenta = codigoPuntoVenta
                    };

                    // Crear el objeto para el donante (equivalente al sujeto excluido)
                    var donante = new
                    {
                        tipoDocumento = DeterminarTipoDocumento(Factura.Receptor.Nit), // "13" para DUI, etc.
                        numDocumento = Factura.Receptor.Nit,
                        nrc = string.IsNullOrWhiteSpace(Factura.Receptor.Nrc) ? (string)null : Factura.Receptor.Nrc,
                        nombre = Factura.Receptor.Nombre,
                        codActividad = Factura.Receptor.CodActividad,
                        descActividad = Factura.Receptor.DescActividad,
                        direccion = new
                        {
                            departamento = Factura.Receptor.CodigoDepartamento,
                            municipio = Factura.Receptor.CodigoMunicipio,
                            distrito = distritoReceptor,
                            complemento = Factura.Receptor.Direccion.Complemento
                        },
                        telefono = Factura.Receptor.Telefono,
                        correo = Factura.Receptor.Correo,
                        codDomiciliado = Factura.Receptor.CodigoDomicilioFiscal,
                        codPais = Factura.CodigoPais
                    };

                    // Crear otros documentos (opcional)
                    var otrosDocumentos = new[]
     {
        new
        {
            codDocAsociado = (int?)null,
            descDocumento = (string)null,
            detalleDocumento = (string)null
        }
    };

                    // Crear el cuerpo del documento
                    var cuerpoDocumento = Factura.Productos
                        .Select((producto, index) => new
                        {
                            numItem = index + 1,
                            tipoDonacion = int.Parse(Factura.TipoDonacion), // Tipo de donación por defecto
                            cantidad = producto.Cantidad,
                            codigo = producto.Codigo,
                            uniMedida = 99, // Unidad de medida estándar
                            descripcion = producto.Descripcion,
                            depreciacion = 0,
                            valorUni = producto.PrecioUnitario,
                            valor = Math.Round(producto.Cantidad * producto.PrecioUnitario, 2)
                        })
                        .ToArray();

                    // Calcular totales
                    decimal valorTotal = cuerpoDocumento.Sum(item => (decimal)item.valor);
                    string totalLetras = new Conversor().ConvertirNumeroALetras(valorTotal);

                    // Crear el objeto resumen
                    var resumen = new
                    {
                        valorTotal = valorTotal,
                        totalLetras = totalLetras,
                        pagos = new[]
                        {
            new
            {
                codigo = "01", // Código del tipo de pago
                montoPago = valorTotal, // Monto total de la donación
                referencia = "0000"
            }
        }


                    };

                    // Crear el apéndice (opcional)
                    var apendice = (string)null;

                    // Serializar el objeto JSON completo para DTE Comprobante de Donación
                    dteJson = JsonSerializer.Serialize(new
                    {
                        identificacion,
                        donatario,
                        donante,
                        otrosDocumentos,
                        cuerpoDocumento,
                        resumen,
                        apendice
                    });

                    if (!PrepararBorrador(ref dteJson)) return Page();
                    var factura = await GuardarFacturaPreparadaAsync(dteJson, codigoGeneracion, numeroControl,
                        identificacion.fecEmi, identificacion.horEmi, 15, "15", Factura.Receptor.Correo,
                        0m, 0m, 0m, valorTotal);
                    if (factura is null) return OperacionYaProcesada();
                    var resultadoTransmision = await _dteTransmision.TransmitirAsync(
                        new DteTransmisionRequest(dteJson, "15", codigoGeneracion.ToString().ToUpperInvariant(), numeroControl, 2,
                            Factura.Receptor.Correo, factura.FacturaId));
                    await GuardarResultadoTransmisionAsync(factura, resultadoTransmision);
                    await NotificarResultadoAsync(factura, resultadoTransmision);
                }
                return RedirectToPage("../facturas/Index");
            }

            return Page();
        }

        private async Task<Factura?> GuardarFacturaPreparadaAsync(string json, Guid codigoGeneracion, string numeroControl,
            string fechaEmision, string horaEmision, int tipoDteLegacy, string tipoDte, string? correo,
            decimal totalGravado, decimal totalExento, decimal totalIva, decimal totalPagar)
        {
            var claveOperacion = $"EMISION-PV:{OperacionEmisionId:N}";
            if (await _context.Facturas.AsNoTracking().AnyAsync(x => x.ClaveIdempotencia == claveOperacion))
                return null;
            var factura = new Factura
            {
                CodigoGeneracion = codigoGeneracion.ToString().ToUpperInvariant(),
                NumeroControl = numeroControl,
                SelloRecepcion = string.Empty,
                JsonDte = json,
                Fecha = DateTime.ParseExact($"{fechaEmision} {horaEmision}", "yyyy-MM-dd HH:mm:ss", null),
                TipoDTE = tipoDteLegacy,
                TipoDteCodigo = tipoDte,
                AmbienteFiscal = ambienteFiscal,
                EstadoTransmision = DteEstados.Preparado,
                IntentosTransmision = 0,
                CorreoReceptor = correo,
                TotalGravado = totalGravado,
                TotalExento = totalExento,
                TotalIva = totalIva,
                TotalPagar = totalPagar,
                ClaveIdempotencia = claveOperacion
            };
            _context.Facturas.Add(factura);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                _context.Entry(factura).State = EntityState.Detached;
                return null;
            }
            return factura;
        }

        private IActionResult OperacionYaProcesada()
        {
            ModelState.AddModelError(string.Empty,
                "Esta operación ya fue guardada. Revise el listado de facturas; no se generó un segundo DTE.");
            return Page();
        }

        private async Task GuardarResultadoTransmisionAsync(Factura factura, DteTransmisionResult resultado)
        {
            factura.SelloRecepcion = resultado.SelloRecepcion ?? string.Empty;
            factura.EstadoTransmision = resultado.Estado;
            factura.IntentosTransmision = resultado.Intentos;
            factura.RespuestaHacienda = resultado.Respuesta;
            factura.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
            await _context.SaveChangesAsync();
        }

        private bool PrepararBorrador(ref string json)
        {
            try
            {
                json = DteDocumentoActualizador.ActualizarBorrador(json, ambienteFiscal, _jsonFactory);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or Newtonsoft.Json.JsonException)
            {
                ModelState.AddModelError(string.Empty, $"No se transmitió el borrador: {ex.Message}");
                return false;
            }
        }

        private async Task NotificarResultadoAsync(Factura factura, DteTransmisionResult resultado)
        {
            TempData.Remove("Success");
            TempData.Remove("Error");
            if (!resultado.Aceptado)
            {
                TempData["Error"] = $"Documento guardado con estado {resultado.Estado}; no está aceptado. Revise la factura antes de reintentar.";
                return;
            }

            TempData["Success"] = "Factura aceptada y guardada.";
            if (string.IsNullOrWhiteSpace(factura.CorreoReceptor)) return;
            try
            {
                await _emailQueue.EncolarDteAsync(factura, factura.TipoDteCodigo!, factura.JsonDte!,
                    factura.SelloRecepcion!, factura.CorreoReceptor);
            }
            catch (Exception)
            {
                TempData["Error"] = "Factura aceptada y guardada, pero no se pudo encolar el correo. No vuelva a emitirla; reintente solo el envío.";
            }
        }

        public static string CorregirCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return correo;

            correo = correo.Trim().ToLower();

            // Verificar y corregir @gmail
            if (correo.Contains("@gmail") && !correo.EndsWith("@gmail.com"))
            {
                // Si termina solo en @gmail, agregar .com
                if (correo.EndsWith("@gmail"))
                {
                    correo += ".com";
                }
                // Si tiene @gmail pero no .com al final, reemplazar la parte después de @gmail
                else if (correo.Contains("@gmail"))
                {
                    int gmailIndex = correo.IndexOf("@gmail");
                    correo = correo.Substring(0, gmailIndex) + "@gmail.com";
                }
            }

            // Verificar y corregir @umoar
            if (correo.Contains("@umoar") && !correo.EndsWith("@umoar.edu.sv"))
            {
                // Si termina solo en @umoar, agregar .edu.sv
                if (correo.EndsWith("@umoar"))
                {
                    correo += ".edu.sv";
                }
                // Si tiene @umoar pero no .edu.sv al final, reemplazar la parte después de @umoar
                else if (correo.Contains("@umoar"))
                {
                    int umoarIndex = correo.IndexOf("@umoar");
                    correo = correo.Substring(0, umoarIndex) + "@umoar.edu.sv";
                }
            }

            return correo;
        }

        private async Task CargarCatalogosAsync(CancellationToken cancellationToken)
        {
            try
            {
                CatalogoDepartamentos = await _catalogos.ObtenerAsync("CAT-012", "1.1", cancellationToken);
                CatalogoMunicipios = await _catalogos.ObtenerAsync("CAT-013", "1.1", cancellationToken);
                CatalogoDistritos = await _catalogos.ObtenerAsync("CAT-008", "1.1", cancellationToken);
                CatalogoPaises = await _catalogos.ObtenerAsync("CAT-020", "1.1", cancellationToken);
                CatalogoDonaciones = await _catalogos.ObtenerAsync("CAT-026", "1.1", cancellationToken);
            }
            catch
            {
                CatalogoDepartamentos = Array.Empty<CatalogoFeItem>();
                CatalogoMunicipios = Array.Empty<CatalogoFeItem>();
                CatalogoDistritos = Array.Empty<CatalogoFeItem>();
                // La emisión se bloquea sin catálogos fiscales; no se usa geografía legacy.
            }
        }

        private static Emisor CrearEmisorDesdeConfiguracion(Entidades.FacturacionElectronica.ConfiguracionFacturacionElectronica configuracion)
            => new()
            {
                Nit = configuracion.NitEmisor ?? string.Empty,
                Nrc = configuracion.NrcEmisor ?? string.Empty,
                Nombre = configuracion.NombreEmisor ?? string.Empty,
                CodActividad = configuracion.CodigoActividad ?? string.Empty,
                DescActividad = configuracion.DescripcionActividad ?? string.Empty,
                NombreComercial = configuracion.NombreComercial ?? string.Empty,
                TipoEstablecimiento = configuracion.TipoEstablecimiento ?? string.Empty,
                Direccion = new Direccion
                {
                    Departamento = configuracion.CodigoDepartamento ?? string.Empty,
                    Municipio = configuracion.CodigoMunicipio ?? string.Empty,
                    Complemento = configuracion.ComplementoDireccion ?? string.Empty
                },
                Telefono = configuracion.TelefonoEmisor ?? string.Empty,
                Correo = configuracion.CorreoEmisor ?? string.Empty
            };

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

