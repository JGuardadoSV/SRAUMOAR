using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class FacturarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfig _emisor;
        private readonly ICorrelativoService _correlativoService;

        public FacturarModel(SRAUMOAR.Modelos.Contexto context, IOptions<EmisorConfig> emisorOptions, ICorrelativoService correlativoService                                                                                )
        {
            _context = context;
            _emisor = emisorOptions.Value;
            _correlativoService = correlativoService;
        }
        public List<Arancel> Aranceles { get; set; }
        //public async Task<IActionResult> OnGet(int alumnoId, int arancelId)
        public async Task<IActionResult> OnGetAsync(string arancelIds,int idalumno)
        {

            var  cicloId = await _context.Ciclos.Where(x => x.Activo==true).FirstAsync();
            var arancelIdsList = arancelIds.Split(',').Select(int.Parse).ToList();
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            var aranceles = await _context.Aranceles.Include(a => a.Ciclo).Where(a=>arancelIdsList.Contains(a.ArancelId)).ToListAsync();
            var ciclo = await _context.Ciclos.FirstOrDefaultAsync(c => c.Id == cicloId.Id);

            ViewData["Alumno"] = alumno;
            ViewData["AlumnoNombre"] = alumno.Nombres + " " + alumno.Apellidos;
            ViewData["AlumnoId"] = alumno.AlumnoId;
            Aranceles = aranceles;
            ViewData["Ciclo"] = ciclo;


            return Page();
        }

        [BindProperty]
        public CobroArancel CobroArancel { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync(List<int> selectedAranceles, List<decimal> arancelescostos)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            //****************************************************
            //CREACION DEL DTE
            //****************************************************



            string dteJson = "";
            Guid codigoGeneracion = Guid.NewGuid();

            Random random = new Random();
            int i = random.Next(1, 100001); // Genera un número aleatorio entre 1 y 100000
            string correlativo = i.ToString().PadLeft(15, '0'); // Rellena con ceros a la izquierda para que tenga 15 caracteres
                                                                // Generar número de control

            int numero =(int) await _correlativoService.ObtenerSiguienteCorrelativo("01", "00"); ;
            string numeroFormateado = numero.ToString("D15");
            string numeroControl = "DTE-" + "01" + "-" + "ABCD1234" + "-" + numeroFormateado;

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
                nombre = "JOSUE GUARDADO",
                codActividad = (string)null,
                descActividad = (string)null,
                direccion = new
                {
                    departamento = "04",
                    municipio = "34",
                    complemento = "Chalatenango"
                },
                telefono = (string)null,
                correo = "jguardadosv@gmail.com"
            };

            var arancelesAPagar = await _context.Aranceles
      .Include(a => a.Ciclo)
      .Where(a => selectedAranceles.Contains(a.ArancelId))
      .ToListAsync();

            // Crear el cuerpo del documento usando los datos de los aranceles obtenidos
            var cuerpoDocumento = arancelesAPagar
                .Select((arancel, index) => new
                {
                    numItem = index + 1,
                    tipoItem = 1,
                    numeroDocumento = (string)null,
                    cantidad = 1,
                    codigo = "0000" + index.ToString(),
                    codTributo = (string)null,
                    uniMedida = 59,
                    descripcion = arancel.Nombre,
                    precioUni = arancel.Costo,
                    montoDescu = 0.0,
                    ventaNoSuj = 0.0,
                    ventaExenta = arancel.Exento  ? arancel.Costo : 0.0m,
                    ventaGravada = !arancel.Exento ? arancel.Costo : 0.0m,
                    tributos = (string)null,
                    psv = arancel.Costo,
                    noGravado = 0.0,
                    ivaItem = !arancel.Exento ? Math.Round(arancel.Costo - (arancel.Costo / 1.13m), 2) : 0.0m
                })
                .ToArray();

            decimal totalVenta = arancelesAPagar.Where(a => a.Exento == false).Sum(a => a.Costo);
            decimal totalVentaExenta = arancelesAPagar.Where(a => a.Exento == true).Sum(a => a.Costo);

            //ivaItem = Math.Round(arancelescostos[index] - (arancelescostos[index] / 1.13m), 6)
            // Crear el resumen
            // Calcular las variables primero
           // decimal totalVentaExenta = arancelescostos.Sum();
         //   decimal totalVenta = 0;
            decimal subTotalVentas = totalVenta+totalVentaExenta;
            decimal subTotal = totalVenta+totalVentaExenta;
            decimal montoTotalOperacion = totalVenta+ totalVentaExenta;
            decimal totalPagar = totalVenta+totalVentaExenta;
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
                CorreoCliente ="jguardadosv@gmail.com"
            };
            var selloRecibido="";
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

            List<DetallesCobroArancel> aranceles = new List<DetallesCobroArancel>();

           for (int y = 0; y < selectedAranceles.Count; y++)
            {
                DetallesCobroArancel arancel = new DetallesCobroArancel();
                arancel.ArancelId = selectedAranceles[y];
                arancel.costo = arancelescostos[y];
                aranceles.Add(arancel);
                
            }
            CobroArancel.DetallesCobroArancel = aranceles;
            CobroArancel.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
            CobroArancel.Fecha = DateTime.Now;
            _context.CobrosArancel.Add(CobroArancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Facturas");
        }
    }
}
