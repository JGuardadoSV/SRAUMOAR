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
using SRAUMOAR.Entidades;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.aranceles
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class FacturarModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly EmisorConfig _emisor;
        private readonly ICorrelativoService _correlativoService;
        private int ambiente = 1;
        public FacturarModel(SRAUMOAR.Modelos.Contexto context, IOptions<EmisorConfig> emisorOptions, ICorrelativoService correlativoService)
        {
            _context = context;
            _emisor = emisorOptions.Value;
            _correlativoService = correlativoService;
        }
        public List<Arancel> Aranceles { get; set; }
        
        // Método para cargar datos de la vista (reutilizable)
        private async Task CargarDatosParaVista(int idalumno)
        {
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            if (alumno != null)
            {
                ViewData["Alumno"] = alumno;
                ViewData["AlumnoNombre"] = alumno.Nombres + " " + alumno.Apellidos;
                ViewData["AlumnoId"] = alumno.AlumnoId;
                ViewData["ExentoMora"] = alumno.ExentoMora;
            }
        }
        
        //public async Task<IActionResult> OnGet(int alumnoId, int arancelId)
        public async Task<IActionResult> OnGetAsync(string arancelIds, int idalumno)
        {
            var arancelIdsList = arancelIds.Split(',').Select(int.Parse).ToList();
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            
            // Obtener aranceles seleccionados
            var aranceles = await _context.Aranceles
                .Where(a => arancelIdsList.Contains(a.ArancelId))
                .ToListAsync();

            // Verificar si el alumno tiene beca parcial
            var becaAlumno = await _context.Becados
                .Where(b => b.AlumnoId == idalumno && b.Estado && b.TipoBeca == 2)
                .FirstOrDefaultAsync();

            if (becaAlumno != null)
            {
                // Obtener aranceles personalizados activos para este becado
                var arancelesPersonalizados = await _context.ArancelesBecados
                    .Where(ab => ab.BecadosId == becaAlumno.BecadosId && ab.Activo)
                    .ToListAsync();

                // Reemplazar el costo de los aranceles seleccionados por el personalizado si existe
                foreach (var arancel in aranceles)
                {
                    var personalizado = arancelesPersonalizados.FirstOrDefault(ab => ab.ArancelId == arancel.ArancelId);
                    if (personalizado != null)
                    {
                        arancel.Costo = personalizado.PrecioPersonalizado;
                    }
                }
            }

            // Cargar los ciclos por separado para evitar problemas con Include
            var ciclosIds = aranceles.Where(a => a.CicloId.HasValue).Select(a => a.CicloId.Value).Distinct().ToList();
            var ciclos = await _context.Ciclos.Where(c => ciclosIds.Contains(c.Id)).ToListAsync();
            
            // Asignar los ciclos a los aranceles
            foreach (var arancel in aranceles)
            {
                if (arancel.CicloId.HasValue)
                {
                    arancel.Ciclo = ciclos.FirstOrDefault(c => c.Id == arancel.CicloId.Value);
                }
            }

            // Obtener el ciclo solo si alguno de los aranceles tiene ciclo
            Ciclo? ciclo = null;
            if (aranceles.Any(a => a.Ciclo != null))
            {
                ciclo = aranceles.First(a => a.Ciclo != null).Ciclo;
            }

            await CargarDatosParaVista(idalumno);
            Aranceles = aranceles;
            ViewData["Ciclo"] = ciclo;

            return Page();
        }

        [BindProperty]
        public CobroArancel CobroArancel { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync(List<int> selectedAranceles, List<decimal> arancelescostos, int idalumno)
        {
            // Validar que los parámetros no sean null
            if (selectedAranceles == null || arancelescostos == null)
            {
                ModelState.AddModelError("", "Los datos de aranceles son requeridos");
                return Page();
            }

            // Obtener el alumno
            var alumno = await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            if (alumno == null)
            {
                ModelState.AddModelError("", "Alumno no encontrado");
                return Page();
            }

            // Verificar si el alumno tiene beca parcial y obtener precios personalizados
            var becaAlumno = await _context.Becados
                .Where(b => b.AlumnoId == idalumno && b.Estado && b.TipoBeca == 2)
                .FirstOrDefaultAsync();

            Dictionary<int, decimal> preciosPersonalizados = new Dictionary<int, decimal>();
            if (becaAlumno != null)
            {
                var arancelesPersonalizados = await _context.ArancelesBecados
                    .Where(ab => ab.BecadosId == becaAlumno.BecadosId && ab.Activo)
                    .ToListAsync();

                foreach (var personalizado in arancelesPersonalizados)
                {
                    preciosPersonalizados[personalizado.ArancelId] = personalizado.PrecioPersonalizado;
                }
            }

            // Validar que el efectivo recibido sea suficiente
            if (CobroArancel.EfectivoRecibido <= 0)
            {
                ModelState.AddModelError("CobroArancel.EfectivoRecibido", "Debe ingresar el monto de efectivo recibido");
                await CargarDatosParaVista(idalumno);
                return Page();
            }

            // Calcular el total a cobrar usando los precios correctos
            decimal totalACobrar = 0;
            for (int x = 0; x < selectedAranceles.Count; x++)
            {
                var arancelId = selectedAranceles[x];
                var precioOriginal = arancelescostos[x];
                
                // Si el alumno tiene beca parcial y hay precio personalizado, usar ese
                if (preciosPersonalizados.ContainsKey(arancelId))
                {
                    totalACobrar += preciosPersonalizados[arancelId];
                }
                else
                {
                    totalACobrar += precioOriginal;
                }
            }

            if (CobroArancel.EfectivoRecibido < totalACobrar)
            {
                ModelState.AddModelError("CobroArancel.EfectivoRecibido", 
                    $"El efectivo recibido (${CobroArancel.EfectivoRecibido:F2}) es insuficiente. Se requiere al menos ${totalACobrar:F2}");
                await CargarDatosParaVista(idalumno);
                return Page();
            }

            if (!ModelState.IsValid)
            {
                await CargarDatosParaVista(idalumno);
                return Page();
            }

            //****************************************************
            //CREACION DEL DTE
            //****************************************************
             alumno = await _context.Alumno.Include(c=>c.Carrera).FirstOrDefaultAsync(m => m.AlumnoId == idalumno);
            string carnet = alumno.Email.Split('@')[0];

            string dteJson = "";
            Guid codigoGeneracion = Guid.NewGuid();

            Random random = new Random();
            int i = random.Next(1, 100001); // Genera un número aleatorio entre 1 y 100000
            string correlativo = i.ToString().PadLeft(15, '0'); // Rellena con ceros a la izquierda para que tenga 15 caracteres
                                                                // Generar número de control

            int numero = (int)await _correlativoService.ObtenerSiguienteCorrelativo("01", ambiente == 1 ? "01" : "00");
            string numeroFormateado = numero.ToString("D15");
            string numeroControl = "DTE-" + "01" + "-" + "M001P001" + "-" + numeroFormateado;

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


            // ESQUEMA PARA UN DTE DE CONSUMIDOR FINAL DTE 01

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
                tipoEstablecimiento = "02",
                direccion = new
                {
                    departamento = _emisor.DEPARTAMENTO,
                    municipio = _emisor.DISTRITO,
                    complemento = _emisor.DIRECCION
                },
                telefono = _emisor.TELEFONO,
                codEstableMH = "M001",
                codEstable = "M001",
                codPuntoVentaMH = "P001",
                codPuntoVenta = "P001",
                correo = _emisor.EMAIL
            };

            // Crear el objeto para el receptor
            var receptor = new
            {
                tipoDocumento = "37",
                numDocumento = (string)null,
                nrc = (string)null,
                nombre = alumno.Nombres + " " + alumno.Apellidos + " " + alumno.Carnet,
                codActividad = (string)null,
                descActividad = (string)null,
                direccion = new
                {
                    departamento = "04",
                    municipio = "34",
                    complemento = alumno.DireccionDeResidencia
                },
                telefono = (string)null,
                correo = alumno.Email
            };

            // Usar una consulta más segura que maneje aranceles sin ciclo
            var arancelesAPagar = await _context.Aranceles
                .Where(a => selectedAranceles.Contains(a.ArancelId))
                .ToListAsync();

            // Cargar los ciclos por separado para evitar problemas con Include
            var ciclosIds = arancelesAPagar.Where(a => a.CicloId.HasValue).Select(a => a.CicloId.Value).Distinct().ToList();
            var ciclos = await _context.Ciclos.Where(c => ciclosIds.Contains(c.Id)).ToListAsync();
            
            // Asignar los ciclos a los aranceles
            foreach (var arancel in arancelesAPagar)
            {
                if (arancel.CicloId.HasValue)
                {
                    arancel.Ciclo = ciclos.FirstOrDefault(c => c.Id == arancel.CicloId.Value);
                }
            }

            // Crear el cuerpo del documento usando los datos enviados desde la vista
            var cuerpoDocumento = selectedAranceles
     .Select((arancelId, index) => {
         var arancel = arancelesAPagar.First(a => a.ArancelId == arancelId);
         var cobroConMora = arancel.EstaVencido && !alumno.ExentoMora;
         return new
         {
             numItem = index + 1,
             tipoItem = 1,
             numeroDocumento = (string)null,
             cantidad = 1,
             codigo = "0000" + index.ToString(),
             codTributo = (string)null,
             uniMedida = 59,
             descripcion = arancel.Nombre + (cobroConMora ? " (con mora incluida)" : ""),
             precioUni = arancelescostos[index],
             montoDescu = 0.0,
             ventaNoSuj = 0.0,
             ventaExenta = arancel.Exento ? arancelescostos[index] : 0.0m,
             ventaGravada = !arancel.Exento ? arancelescostos[index] : 0.0m,
             tributos = (string)null,
             psv = arancelescostos[index],
             noGravado = 0.0,
             ivaItem = !arancel.Exento ? Math.Round(arancelescostos[index] - (arancelescostos[index] / 1.13m), 2) : 0.0m
         };
     })
     .ToArray();

            decimal totalVenta = 0;
            decimal totalVentaExenta = 0;
            for (int y = 0; y < selectedAranceles.Count; y++)
            {
                var arancel = arancelesAPagar.First(a => a.ArancelId == selectedAranceles[y]);
                if (arancel.Exento)
                    totalVentaExenta += arancelescostos[y];
                else
                    totalVenta += arancelescostos[y];
            }
            decimal subTotalVentas = totalVenta + totalVentaExenta;
            decimal subTotal = subTotalVentas;
            decimal montoTotalOperacion = subTotalVentas;
            decimal totalPagar = subTotalVentas;
            string totalLetras = new Conversor().ConvertirNumeroALetras(totalPagar);
            decimal totalIva = cuerpoDocumento.Sum(item => item.ivaItem);

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
                Password = ambiente == 1 ? _emisor.CLAVEPRODAPI : _emisor.CLAVETESTAPI,
                Ambiente = ambiente == 1 ? "01" : "00",
                DteJson = dteJson,
                Nit = _emisor.NIT,
                PasswordPrivado = ambiente == 1 ? _emisor.CLAVEPRODCERTIFICADO : _emisor.CLAVETESTCERTIFICADO,
                TipoDte = "01",
                CodigoGeneracion = codigoGeneracion,
                NumControl = numeroControl,
                VersionDte = 1,
                CorreoCliente = alumno.Email,
                Carrera = string.IsNullOrEmpty(alumno.Carrera?.NombreCarrera) ? "-" : alumno.Carrera.NombreCarrera,
                Observaciones = string.IsNullOrEmpty(CobroArancel.nota) ? "-" : CobroArancel.nota
            };
            var selloRecibido = "";
            //string selloRecibido = null;
            int intentos = 0;
            int maxIntentos = 3;

            while (intentos < maxIntentos && string.IsNullOrEmpty(selloRecibido))
            {
                intentos++;
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        var response = client.PostAsJsonAsync(
                                 ambiente == 1 ? "http://207.58.153.147:7122/api/procesar-dte" : "http://207.58.153.147:7122/api/procesar-dte",
                                  requestUnificado).Result;
                        var responseData = response.Content.ReadAsStringAsync().Result;
                        if (!response.IsSuccessStatusCode)
                            throw new Exception($"Error al procesar DTE: {responseData}");
                        var resultado = JsonDocument.Parse(responseData).RootElement;
                        var selloRecepcion = resultado.TryGetProperty("selloRecibido", out var sello)
                            ? sello.GetString()
                            : null;
                        selloRecibido = selloRecepcion;
                    }
                }
                catch (Exception)
                {

                }

                if (string.IsNullOrEmpty(selloRecibido) && intentos < maxIntentos)
                    Thread.Sleep(8000); // Esperar 8 segundos solo si va a reintentar
            }

           // if (ambiente == 1)
          //  {
                //guardar factura

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
        //    }
            //****************************************************
            //FIN CREACION DEL DTE
            //****************************************************

            List<DetallesCobroArancel> aranceles = new List<DetallesCobroArancel>();
            bool algunConMora = false;
            for (int y = 0; y < selectedAranceles.Count; y++)
            {
                var arancelDb = await _context.Aranceles.FindAsync(selectedAranceles[y]);
                var vencido = arancelDb.EstaVencido;
                var mostrarMora = vencido && !alumno.ExentoMora;
                
                // Determinar el costo base (precio personalizado si existe, sino el original)
                decimal costoBase;
                if (preciosPersonalizados.ContainsKey(selectedAranceles[y]))
                {
                    costoBase = preciosPersonalizados[selectedAranceles[y]];
                }
                else
                {
                    costoBase = arancelDb.Costo;
                }
                
                // Aplicar mora si es necesario
                var costoFinal = mostrarMora ? costoBase + arancelDb.ValorMora : costoBase;
                
                if (mostrarMora)
                {
                    algunConMora = true;
                }
                
                DetallesCobroArancel arancel = new DetallesCobroArancel();
                arancel.ArancelId = selectedAranceles[y];
                arancel.costo = costoFinal;
                aranceles.Add(arancel);
            }
            CobroArancel.DetallesCobroArancel = aranceles;
            // Agregar nota si algún arancel fue cobrado con mora
            if (algunConMora)
            {
                if (string.IsNullOrWhiteSpace(CobroArancel.nota))
                    CobroArancel.nota = "con mora incluida";
                else if (!CobroArancel.nota.Contains("con mora incluida"))
                    CobroArancel.nota += " (con mora incluida)";
            }
            // Asignar el total real cobrado (incluyendo mora si aplica)
            CobroArancel.Total = aranceles.Sum(a => a.costo);
            CobroArancel.CodigoGeneracion = codigoGeneracion.ToString().ToUpper();
            CobroArancel.Fecha = DateTime.Now;
            
            // Asignar el CicloId si existe un ciclo en los aranceles
            var cicloDeAranceles = arancelesAPagar.FirstOrDefault(a => a.Ciclo != null)?.Ciclo;
            if (cicloDeAranceles != null)
            {
                CobroArancel.CicloId = cicloDeAranceles.Id;
            }
            _context.CobrosArancel.Add(CobroArancel);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Facturas");
        }
    }
}

