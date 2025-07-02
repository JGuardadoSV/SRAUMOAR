    // Models/PuntoVentaModels.cs
    using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades
{
    public class registroDTE
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CodigoGeneracion { get; set; }

        [Required]
        public string NumControl { get; set; }

        [Required]
        public string Tipo { get; set; } = string.Empty;

        [Required]
        public DateOnly Fecha { get; set; }

        [Required]
        public TimeOnly Hora { get; set; }

        // Constructor para inicializar el GUID automáticamente
        
    }
        public class Direccion
        {
            public string Departamento { get; set; } = string.Empty;
            public string Municipio { get; set; } = string.Empty;
            public string Complemento { get; set; } = string.Empty;
        }

        public class Emisor
        {
            public string Nit { get; set; } = string.Empty;
            public string Nrc { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string CodActividad { get; set; } = string.Empty;
            public string DescActividad { get; set; } = string.Empty;
            public string NombreComercial { get; set; } = string.Empty;
            public string TipoEstablecimiento { get; set; } = string.Empty;
            public Direccion Direccion { get; set; } = new Direccion();
            public string Telefono { get; set; } = string.Empty;
            public string? CodEstableMH { get; set; }
            public string? CodEstable { get; set; }
            public string? CodPuntoVentaMH { get; set; }
            public string? CodPuntoVenta { get; set; }
            public string Correo { get; set; } = string.Empty;
        }

        public class Receptor
        {
            public string Nit { get; set; } = string.Empty;
            public string? Nrc { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string? CodActividad { get; set; } = string.Empty;
            public string? DescActividad { get; set; } = string.Empty;
            public string? NombreComercial { get; set; } = string.Empty;
            public Direccion Direccion { get; set; } = new Direccion();
            public string Telefono { get; set; } = string.Empty;
            public string Correo { get; set; } = string.Empty;
            
            // Campos para departamento y municipio
            public string CodigoDepartamento { get; set; } = string.Empty;
            public string CodigoMunicipio { get; set; } = string.Empty;
        }

        public class ProductoVenta
        {
            public int Id { get; set; }
            public string Codigo { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;

            [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
            public decimal Cantidad { get; set; }

            [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
            public decimal PrecioUnitario { get; set; }

            public bool EsExento { get; set; }

            public decimal SubTotal => Cantidad * PrecioUnitario;
        }

        public class FacturaViewModel
        {
            public Emisor Emisor { get; set; } = new Emisor();
            public Receptor Receptor { get; set; } = new Receptor();

            [Required(ErrorMessage = "Debe seleccionar un tipo de documento")]
            public string TipoDocumento { get; set; } = string.Empty;

            // Campo para código de país (solo para donaciones)
            public string CodigoPais { get; set; } = string.Empty;

            public List<ProductoVenta> Productos { get; set; } = new List<ProductoVenta>();

            // Propiedades para el formulario de productos
            public string CodigoProducto { get; set; } = string.Empty;
            public string DescripcionProducto { get; set; } = string.Empty;
            public decimal CantidadProducto { get; set; }
            public decimal PrecioProducto { get; set; }
            public bool ProductoExento { get; set; }

            // Totales calculados
            public decimal TotalExento => Productos.Where(p => p.EsExento).Sum(p => p.SubTotal);
            public decimal TotalGravado => Productos.Where(p => !p.EsExento).Sum(p => p.SubTotal);
            
            // IVA calculado según tipo de documento y productos exentos
            public decimal IVA 
            { 
                get 
                {
                    // Solo calcular IVA si el documento es Crédito Fiscal (02)
                    if (TipoDocumento != "02")
                    {
                        return 0;
                    }
                    
                    // Solo calcular IVA sobre productos gravados (no exentos)
                    return TotalGravado * 0.13m; // 13% IVA
                }
            }
            
            public decimal TotalGeneral => TotalExento + TotalGravado + IVA;
        }

        public static class TiposDocumento
        {
            public static readonly Dictionary<string, string> Tipos = new Dictionary<string, string>
        {
            { "01", "Consumidor Final" },
            { "02", "Crédito Fiscal" },
            { "11", "Sujeto Excluido" },
            { "15", "Donación" }
        };
        }

        public static class PaisesFiltrados
        {
            public static readonly Dictionary<string, string> Paises = new Dictionary<string, string>
            {
                // Centro América
                { "BZ", "Belice" },
                { "CR", "Costa Rica" },
                { "SV", "El Salvador" },
                { "GT", "Guatemala" },
                { "HN", "Honduras" },
                { "NI", "Nicaragua" },
                { "PA", "Panamá" },
                
                // Norte América
                { "CA", "Canadá" },
                { "US", "Estados Unidos" },
                { "MX", "México" },
                
                // Europa
                { "ES", "España" }
            };
        }

        public static class Departamentos
        {
            public static readonly Dictionary<string, string> Lista = new Dictionary<string, string>
            {
                { "00", "Otro (Para extranjeros)" },
                { "01", "Ahuachapán" },
                { "02", "Santa Ana" },
                { "03", "Sonsonate" },
                { "04", "Chalatenango" },
                { "05", "La Libertad" },
                { "06", "San Salvador" },
                { "07", "Cuscatlán" },
                { "08", "La Paz" },
                { "09", "Cabañas" },
                { "10", "San Vicente" },
                { "11", "Usulután" },
                { "12", "San Miguel" },
                { "13", "Morazán" },
                { "14", "La Unión" }
            };
        }

        public static class Municipios
        {
            public static readonly Dictionary<string, string> Lista = new Dictionary<string, string>
            {
                // Extranjero
                { "00", "Extranjero" },
                
                // Ahuachapán
                { "13", "AHUACHAPAN NORTE" },
                { "14", "AHUACHAPAN CENTRO" },
                { "15", "AHUACHAPAN SUR" },
                
                // Santa Ana
                { "14", "SANTA ANA NORTE" },
                { "15", "SANTA ANA CENTRO" },
                { "16", "SANTA ANA ESTE" },
                { "17", "SANTA ANA OESTE" },
                
                // Sonsonate
                { "17", "SONSONATE NORTE" },
                { "18", "SONSONATE CENTRO" },
                { "19", "SONSONATE ESTE" },
                { "20", "SONSONATE OESTE" },
                
                // Chalatenango
                { "34", "CHALATENANGO NORTE" },
                { "35", "CHALATENANGO CENTRO" },
                { "36", "CHALATENANGO SUR" },
                
                // La Libertad
                { "23", "LA LIBERTAD NORTE" },
                { "24", "LA LIBERTAD CENTRO" },
                { "25", "LA LIBERTAD OESTE" },
                { "26", "LA LIBERTAD ESTE" },
                { "27", "LA LIBERTAD COSTA" },
                { "28", "LA LIBERTAD SUR" },
                
                // San Salvador
                { "20", "SAN SALVADOR NORTE" },
                { "21", "SAN SALVADOR OESTE" },
                { "22", "SAN SALVADOR ESTE" },
                { "23", "SAN SALVADOR CENTRO" },
                { "24", "SAN SALVADOR SUR" },
                
                // Cuscatlán
                { "17", "CUSCATLAN NORTE" },
                { "18", "CUSCATLAN SUR" },
                
                // La Paz
                { "23", "LA PAZ OESTE" },
                { "24", "LA PAZ CENTRO" },
                { "25", "LA PAZ ESTE" },
                
                // Cabañas
                { "10", "CABAÑAS OESTE" },
                { "11", "CABAÑAS ESTE" },
                
                // San Vicente
                { "14", "SAN VICENTE NORTE" },
                { "15", "SAN VICENTE SUR" },
                
                // Usulután
                { "24", "USULUTAN NORTE" },
                { "25", "USULUTAN ESTE" },
                { "26", "USULUTAN OESTE" },
                
                // San Miguel
                { "21", "SAN MIGUEL NORTE" },
                { "22", "SAN MIGUEL CENTRO" },
                { "23", "SAN MIGUEL OESTE" },
                
                // Morazán
                { "27", "MORAZAN NORTE" },
                { "28", "MORAZAN SUR" },
                
                // La Unión
                { "19", "LA UNION NORTE" },
                { "20", "LA UNION SUR" }
            };
        }
    }

