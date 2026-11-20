using System.Globalization;
using Newtonsoft.Json.Linq;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDtePdfService
{
    byte[] Generar(string tipoDte, string json, string? sello);
    string ObtenerNombre(string tipoDte, string codigoGeneracion, bool anulacion = false);
}

/// <summary>
/// Renderizador único de documentos fiscales. La información se obtiene de los nombres
/// oficiales actuales y de sus equivalentes históricos para que los DTE existentes sigan
/// siendo imprimibles aunque hayan sido emitidos con una versión anterior del esquema.
/// </summary>
public sealed class DtePdfService : IDtePdfService
{
    private const string Azul = "#0F2747";
    private const string AzulMedio = "#1769AA";
    private const string AzulClaro = "#EAF3FA";
    private const string Texto = "#263746";
    private const string Muted = "#657786";
    private const string Borde = "#D9E2EA";
    private const string Fondo = "#F6F8FB";

    public byte[] Generar(string tipoDte, string json, string? sello)
    {
        var root = JObject.Parse(json);
        var identificacion = root["identificacion"] as JObject ?? new JObject();
        var tipoEntrada = identificacion.Value<string>("tipoDte")
            ?? identificacion.Value<string>("tipoEvento")
            ?? tipoDte;
        var tipo = string.Equals(tipoDte, "INVALIDACION", StringComparison.OrdinalIgnoreCase)
            ? "INVALIDACION"
            : (tipoEntrada ?? string.Empty).PadLeft(2, '0');

        if (tipo == "18") return GenerarEventoRetorno(root, identificacion, sello);

        var emisor = root["emisor"] as JObject ?? root["donatario"] as JObject ?? new JObject();
        var receptor = root["receptor"] as JObject ?? root["sujetoExcluido"] as JObject
            ?? root["donante"] as JObject ?? new JObject();
        var codigo = Valor(identificacion, "codigoGeneracion");
        var titulo = tipo == "INVALIDACION" ? "DOCUMENTO DE INVALIDACIÓN" : TipoDocumento(tipo);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(8).FontColor(Texto));
            page.Header().Element(header => AgregarEncabezado(header, root, identificacion, emisor, titulo, codigo));
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(8);
                AgregarIdentificacion(column, identificacion, tipo, codigo);
                AgregarPartes(column, emisor, receptor, tipo);

                if (tipo is "05" or "06") AgregarNota(column, root, tipo);
                if (tipo == "INVALIDACION") AgregarInvalidacion(column, root);
                else
                {
                    AgregarDetalle(column, root["cuerpoDocumento"] as JArray);
                    AgregarResumen(column, root["resumen"] as JObject);
                }

                AgregarQrYValidacion(column, tipo == "INVALIDACION"
                    ? root["documento"] as JObject ?? identificacion : identificacion,
                    identificacion.Value<string>("ambiente"), sello);
            });
            page.Footer().PaddingTop(8).BorderTop(1).BorderColor(Borde).Row(row =>
            {
                row.RelativeItem().Text("SRAUMOAR  ·  Facturación Electrónica").FontSize(7).FontColor(Muted);
                row.ConstantItem(80).AlignRight().Text(text =>
                {
                    text.Span("Página ").FontSize(7).FontColor(Muted);
                    text.CurrentPageNumber().FontSize(7).FontColor(Muted);
                    text.Span(" de ").FontSize(7).FontColor(Muted);
                    text.TotalPages().FontSize(7).FontColor(Muted);
                });
            });
        })).GeneratePdf();
    }

    public string ObtenerNombre(string tipoDte, string codigoGeneracion, bool anulacion = false)
        => $"{(anulacion ? "INVALIDACION" : "DTE-" + tipoDte.PadLeft(2, '0'))}-{codigoGeneracion}.pdf";

    private static void AgregarEncabezado(IContainer container, JObject root, JObject identificacion,
        JObject emisor, string titulo, string codigo)
    {
        container.Background(Azul).Padding(14).Row(row =>
        {
            row.RelativeItem(1.25f).Column(left =>
            {
                var logo = ObtenerLogo();
                if (logo is not null) left.Item().Height(38).Image(logo).FitArea();
                left.Item().PaddingTop(4).Text(Valor(emisor, "nombreComercial", "nombre", "razonSocial"))
                    .Bold().FontSize(12).FontColor(Colors.White);
                left.Item().Text("Documento tributario electrónico").FontSize(7.5f).FontColor("#C9D9E8");
            });
            row.ConstantItem(205).Background(Colors.White).Padding(9).Column(right =>
            {
                right.Item().Text(titulo).Bold().FontSize(10).FontColor(Azul);
                right.Item().PaddingTop(3).Text($"Tipo DTE  {Valor(identificacion, "tipoDte", "tipoEvento", "-")}")
                    .FontSize(7.5f).FontColor(Texto);
                right.Item().Text($"Código  {codigo}").FontSize(7.5f).FontColor(Texto);
                right.Item().Text($"Ambiente  {Ambiente(Valor(identificacion, "ambiente"))}")
                    .FontSize(7.5f).FontColor(Texto);
            });
        });
    }

    private static void AgregarIdentificacion(ColumnDescriptor column, JObject identificacion, string tipo, string codigo)
    {
        Seccion(column, "Identificación fiscal", content => content.Row(row =>
        {
            row.RelativeItem().Element(c => Campo(c, "Número de control", Valor(identificacion, "numeroControl", "numeroControlDte")));
            row.RelativeItem().Element(c => Campo(c, "Código de generación", codigo));
            row.RelativeItem().Element(c => Campo(c, "Fecha y hora", FechaHora(identificacion)));
        }));
    }

    private static void AgregarPartes(ColumnDescriptor column, JObject emisor, JObject receptor, string tipo)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Element(c => TarjetaParte(c, "Emisor", emisor, true));
            row.ConstantItem(8);
            if (tipo == "INVALIDACION")
                row.RelativeItem().Element(c => TarjetaParte(c, "Entidad receptora", receptor, false));
            else
                row.RelativeItem().Element(c => TarjetaParte(c, "Receptor", receptor, false));
        });
    }

    private static void TarjetaParte(IContainer container, string titulo, JObject persona, bool emisor)
    {
        container.Border(1).BorderColor(Borde).Background(Fondo).Padding(9).Column(column =>
        {
            column.Item().Text(titulo.ToUpperInvariant()).Bold().FontSize(8).FontColor(AzulMedio);
            column.Item().PaddingTop(4).Text(Valor(persona, "nombre", "nombreComercial", "razonSocial"))
                .Bold().FontSize(9).FontColor(Texto);
            column.Item().PaddingTop(2).Text($"{(emisor ? "NIT" : "Documento")}: {Valor(persona, "nit", "numDocumento", "numeroDocumento")}").FontSize(7.5f);
            if (emisor) column.Item().Text($"NRC: {Valor(persona, "nrc")}").FontSize(7.5f);
            var actividad = Valor(persona, "descActividad", "actividadEconomica");
            if (actividad != "-") column.Item().Text(actividad).FontSize(7.5f).FontColor(Muted);
            column.Item().Text(Direccion(persona)).FontSize(7.5f).FontColor(Muted);
            var contacto = string.Join("  ·  ", new[] { Valor(persona, "telefono"), Valor(persona, "correo", "email") }.Where(x => x != "-"));
            if (!string.IsNullOrWhiteSpace(contacto)) column.Item().Text(contacto).FontSize(7.5f).FontColor(Muted);
        });
    }

    private static void AgregarNota(ColumnDescriptor column, JObject root, string tipo)
    {
        var relacionado = (root["documentoRelacionado"] as JArray)?.OfType<JObject>().FirstOrDefault();
        var resumen = root["resumen"] as JObject;
        Seccion(column, tipo == "05" ? "Nota de crédito" : "Nota de débito", content => content.Row(row =>
        {
            Campo(row.RelativeItem(), "Documento original", Valor(relacionado, "numeroDocumento", "codigoGeneracion"));
            Campo(row.RelativeItem(), "Fecha original", Valor(relacionado, "fechaEmision"));
            Campo(row.RelativeItem(1.5f), "Motivo", Valor(resumen, "observaciones", "descripcion"));
        }));
    }

    private static void AgregarInvalidacion(ColumnDescriptor column, JObject root)
    {
        var documento = root["documento"] as JObject ?? new JObject();
        var motivo = root["motivo"] as JObject ?? new JObject();
        Seccion(column, "Documento invalidado", content => content.Column(inner =>
        {
            inner.Item().Element(c => Campo(c, "Código de generación original", Valor(documento, "codigoGeneracion")));
            inner.Item().Element(c => Campo(c, "Sello original", Valor(documento, "selloRecibido", "sello")));
            inner.Item().Element(c => Campo(c, "Motivo", Valor(motivo, "motivoAnulacion", "descripcionMotivo")));
        }));
    }

    private static void AgregarDetalle(ColumnDescriptor column, JArray? items)
    {
        if (items is null || items.Count == 0) return;
        Seccion(column, "Detalle de bienes y servicios", content => content.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22); columns.RelativeColumn(3.8f); columns.ConstantColumn(42);
                columns.ConstantColumn(58); columns.ConstantColumn(54); columns.ConstantColumn(66);
            });
            table.Header(header =>
            {
                foreach (var title in new[] { "#", "Descripción / código", "Cant.", "P. unitario", "Descuento", "Valor" })
                    header.Cell().Background(Azul).Padding(5).Text(title).Bold().FontSize(7).FontColor(Colors.White);
            });
            foreach (var item in items.OfType<JObject>())
            {
                Celda(table, Valor(item, "numItem"));
                table.Cell().BorderBottom(1).BorderColor(Borde).Padding(5).Column(detail =>
                {
                    detail.Item().Text(Valor(item, "descripcion", "detalle")).FontSize(7.5f);
                    var code = Valor(item, "codigo", "codigoItem");
                    if (code != "-") detail.Item().Text(code).FontSize(6.5f).FontColor(Muted);
                });
                Celda(table, Valor(item, "cantidad"), true);
                Celda(table, Moneda(item["precioUni"]), true);
                Celda(table, Moneda(item["montoDescu"]), true);
                Celda(table, Moneda(ObtenerMontoItem(item)), true, true);
            }
        }));
    }

    private static void AgregarResumen(ColumnDescriptor column, JObject? resumen)
    {
        if (resumen is null) return;
        Seccion(column, "Resumen de la operación", content => content.Row(row =>
        {
            row.RelativeItem().Background(Fondo).Border(1).BorderColor(Borde).Padding(8).Column(left =>
            {
                left.Item().Text("TOTAL EN LETRAS").Bold().FontSize(7).FontColor(AzulMedio);
                left.Item().PaddingTop(4).Text(Valor(resumen, "totalLetras")).FontSize(8);
                var tributos = resumen["tributos"] as JArray;
                if (tributos is not null)
                    foreach (var tributo in tributos.OfType<JObject>())
                        left.Item().PaddingTop(3).Text($"{Valor(tributo, "descripcion", "codigo")}: {Moneda(tributo["valor"])}").FontSize(7);
            });
            row.ConstantItem(8);
            row.RelativeItem().Border(1).BorderColor(Borde).Padding(8).Column(right =>
            {
                LineaTotal(right, "No sujeto", resumen, "totalNoSuj");
                LineaTotal(right, "Exento", resumen, "totalExenta");
                LineaTotal(right, "Gravado", resumen, "totalGravada");
                LineaTotal(right, "Descuento", resumen, "totalDescu");
                LineaTotal(right, "IVA", resumen, "totalIva", "totalIvaRetenido");
                LineaTotal(right, "Retención IVA", resumen, "ivaRete", "ivaRete1");
                LineaTotal(right, "Percepción IVA", resumen, "ivaPerci", "ivaPerci1");
                LineaTotal(right, "Total a pagar", resumen, "totalPagar", "valorTotal", true);
            });
        }));
    }

    private static void AgregarQrYValidacion(ColumnDescriptor column, JObject identificacion, string? ambiente, string? sello)
    {
        var url = UrlConsulta(identificacion, ambiente);
        column.Item().PaddingTop(2).Row(row =>
        {
            if (url is not null && !string.IsNullOrWhiteSpace(sello))
            {
                using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
                using var qr = new PngByteQRCode(data);
                row.ConstantItem(70).Height(70).Image(qr.GetGraphic(4)).FitArea();
                row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(info =>
                {
                    info.Item().Text("Consulta y valida este documento").Bold().FontSize(8).FontColor(Azul);
                    info.Item().PaddingTop(3).Text(url).FontSize(6.5f).FontColor(AzulMedio);
                    info.Item().PaddingTop(5).Text($"Sello de recepción: {sello}").FontSize(7).FontColor(Muted);
                });
            }
            else row.RelativeItem().Text($"Sello de recepción: {sello ?? "Pendiente de transmisión"}").Bold().FontSize(8).FontColor(Muted);
        });
        column.Item().PaddingTop(9).Text("El JSON electrónico correspondiente se conserva como respaldo del documento.")
            .Italic().FontSize(7).FontColor(Muted);
    }

    private static void Seccion(ColumnDescriptor column, string titulo, Action<IContainer> contenido)
    {
        column.Item().Text(titulo).Bold().FontSize(9).FontColor(Azul);
        column.Item().Element(contenido);
    }

    private static void Campo(IContainer container, string etiqueta, string valor)
    {
        container.PaddingRight(8).Column(column =>
        {
            column.Item().Text(etiqueta.ToUpperInvariant()).FontSize(6.5f).FontColor(Muted);
            column.Item().PaddingTop(2).Text(valor).FontSize(8).FontColor(Texto);
        });
    }

    private static void Celda(TableDescriptor table, string texto, bool derecha = false, bool negrita = false)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor(Borde).Padding(5);
        var line = (derecha ? cell.AlignRight() : cell).Text(texto).FontSize(7.5f);
        if (negrita) line.Bold();
    }

    private static void LineaTotal(ColumnDescriptor column, string etiqueta, JObject resumen, string nombre, string? alterno = null, bool destacada = false)
    {
        var valor = Valor(resumen, nombre, alterno ?? string.Empty);
        if (valor == "-") return;
        column.Item().Row(row =>
        {
            var etiquetaTexto = row.RelativeItem().Text(etiqueta).FontSize(destacada ? 8.5f : 7.5f);
            var valorTexto = row.ConstantItem(74).AlignRight().Text(Moneda(valor)).FontSize(destacada ? 8.5f : 7.5f);
            if (destacada)
            {
                etiquetaTexto.Bold();
                valorTexto.Bold();
            }
        });
    }


    private static string TipoDocumento(string tipo) => tipo switch
    {
        "01" => "FACTURA ELECTRÓNICA", "03" => "COMPROBANTE DE CRÉDITO FISCAL",
        "05" => "NOTA DE CRÉDITO", "06" => "NOTA DE DÉBITO", "07" => "COMPROBANTE DE RETENCIÓN",
        "08" => "COMPROBANTE DE LIQUIDACIÓN", "09" => "DOCUMENTO CONTABLE DE LIQUIDACIÓN",
        "11" => "FACTURA DE EXPORTACIÓN", "14" => "FACTURA DE SUJETO EXCLUIDO",
        "15" => "COMPROBANTE DE DONACIÓN", _ => $"DOCUMENTO TRIBUTARIO ELECTRÓNICO {tipo}"
    };

    private static string Ambiente(string value) => value switch { "00" => "Pruebas (00)", "01" => "Producción (01)", _ => value == "-" ? "-" : value };

    private static string FechaHora(JObject value)
    {
        var fecha = Valor(value, "fecEmi", "fecAnula");
        var hora = Valor(value, "horEmi", "horAnula");
        return hora == "-" ? fecha : $"{fecha} {hora}";
    }

    private static string Direccion(JObject persona)
        => persona["direccion"] is JObject direccion ? CatalogoTerritorialFiscal.Formatear(direccion) : Valor(persona, "direccion");

    internal static string? UrlConsulta(JObject identificacion, string? ambiente)
    {
        var codigo = Valor(identificacion, "codigoGeneracion");
        var fecha = Valor(identificacion, "fecEmi", "fecAnula");
        return ambiente is ("00" or "01") && codigo != "-" && fecha != "-"
            ? $"https://admin.factura.gob.sv/consultaPublica?ambiente={ambiente}&codGen={Uri.EscapeDataString(codigo)}&fechaEmi={Uri.EscapeDataString(fecha)}" : null;
    }

    private static string Valor(JObject? objeto, params string[] nombres)
    {
        if (objeto is null) return "-";
        foreach (var nombre in nombres.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var valor = objeto[nombre];
            if (valor is not null && valor.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(valor.ToString())) return valor.ToString();
        }
        return "-";
    }

    private static string Moneda(JToken? valor)
        => decimal.TryParse(valor?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numero) ? $"${numero:N2}" : "-";

    private static string ObtenerMontoItem(JObject item)
    {
        static decimal Numero(JToken? valor)
        {
            if (valor?.Type is JTokenType.Integer or JTokenType.Float)
                return valor.Value<decimal>();
            if (decimal.TryParse(valor?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                return n;
            return decimal.TryParse(valor?.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out n) ? n : 0m;
        }
        var nombres = new[] { "ventaGravada", "ventaExenta", "ventaNoSuj", "compra" };
        var presentes = nombres.Where(n => item[n] is not null && item[n]!.Type != JTokenType.Null).ToArray();
        var total = presentes.Length == 0 ? Numero(item["valor"]) : presentes.Sum(n => Numero(item[n]));
        return total.ToString(CultureInfo.InvariantCulture);
    }

    private static bool EsCero(string valor) => decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && n == 0m;

    private static byte[]? ObtenerLogo()
    {
        var candidatos = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "logoUmoar.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoUmoar.jpg"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot", "images", "logoUmoar.jpg"))
        };
        var ruta = candidatos.FirstOrDefault(File.Exists);
        if (ruta is not null) return File.ReadAllBytes(ruta);

        // El recurso embebido evita que el logo desaparezca en pruebas, servicios o
        // publicaciones donde wwwroot no queda junto al ensamblado.
        using var recurso = typeof(DtePdfService).Assembly
            .GetManifestResourceStream("SRAUMOAR.logoUmoar.jpg");
        if (recurso is null) return null;
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static byte[] GenerarEventoRetorno(JObject root, JObject identificacion, string? sello)
    {
        // ERET mantiene sus campos propios, pero usa la misma jerarquía visual del resto de DTE.
        var emisor = root["emisor"] as JObject ?? new JObject();
        var documento = root["documento"] as JObject ?? new JObject();
        var resumen = root["resumen"] as JObject ?? new JObject();
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter); page.Margin(30);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(8).FontColor(Texto));
            page.Header().Element(header => AgregarEncabezado(header, root, identificacion, emisor, "EVENTO DE RETORNO · TIPO 18", Valor(identificacion, "codigoGeneracion")));
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(8);
                AgregarIdentificacion(column, identificacion, "18", Valor(identificacion, "codigoGeneracion"));
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => TarjetaParte(c, "Emisor", emisor, true));
                    row.ConstantItem(8);
                    row.RelativeItem().Element(c => TarjetaParte(c, "Documento / receptor", documento, false));
                });
                var relacionados = root["documentoRelacionado"] as JArray;
                if (relacionados is not null && relacionados.Count > 0)
                    Seccion(column, "Documentos relacionados", c => c.Column(inner =>
                    {
                        foreach (var d in relacionados.OfType<JObject>())
                            inner.Item().Text($"Tipo: {Valor(d, "tipoDocumento")}   Código: {Valor(d, "codigoGeneracion")}   Fecha: {Valor(d, "fechaEmision")}");
                    }));
                AgregarDetalle(column, root["cuerpoDocumento"] as JArray);
                AgregarResumen(column, resumen);
                AgregarQrYValidacion(column, identificacion, identificacion.Value<string>("ambiente"), sello);
            });
            page.Footer().AlignCenter().Text("SRAUMOAR  ·  Evento de Retorno · Página ").FontSize(7).FontColor(Muted);
        })).GeneratePdf();
    }
}
