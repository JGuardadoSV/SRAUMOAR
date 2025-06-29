using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades;

namespace SRAUMOAR.Pages.puntoventa
{
    public class puntoVentaModel : PageModel
    {
      
            [BindProperty]
            public FacturaViewModel Factura { get; set; } = new FacturaViewModel();

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

                    // Limpiar el formulario para nuevos valores
                    Factura.CodigoProducto = string.Empty;
                    Factura.DescripcionProducto = string.Empty;
                    Factura.CantidadProducto = 0;
                    Factura.PrecioProducto = 0;
                    Factura.ProductoExento = false;

                    // Limpiar el ModelState para que los valores no persistan en la vista
                    ModelState.Clear();
                }

                return Page();
            }

            public IActionResult OnPostEliminarProducto(int id)
            {
                var producto = Factura.Productos.FirstOrDefault(p => p.Id == id);
                if (producto != null)
                {
                    Factura.Productos.Remove(producto);
                }

                return Page();
            }

            public IActionResult OnPostProcesarFactura()
            {
                if (ModelState.IsValid && Factura.Productos.Any())
                {
                    // Aquí procesarías la factura
                    // Por ejemplo, guardar en base de datos, generar PDF, etc.

                    TempData["Success"] = "Factura procesada exitosamente";
                    return RedirectToPage();
                }

                return Page();
            }

            private void CargarDatosEmisor()
            {
                // Ejemplo de carga de datos del emisor
                Factura.Emisor = new Emisor
                {
                    Nit = "04352208241018",
                    Nrc = "3477200",
                    Nombre = "Keyjo",
                    CodActividad = "46510",
                    DescActividad = "VENTA AL POR MAYOR DE COMPUTADORAS, EQUIPO PERIFERICO Y PROGRAMAS INFORMATICOS",
                    NombreComercial = "Keyjo",
                    TipoEstablecimiento = "02",
                    Direccion = new Direccion
                    {
                        Departamento = "04",
                        Municipio = "35",
                        Complemento = "Calle 123, Ciudad, País"
                    },
                    Telefono = "76230990",
                    Correo = "demo@gmail.com"
                };
            }
        }
    }

