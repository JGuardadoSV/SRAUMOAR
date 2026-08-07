using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDtePdfService
{
    byte[] Generar(string tipoDte, string json, string? sello);
    string ObtenerNombre(string tipoDte, string codigoGeneracion, bool anulacion = false);
}

public sealed class DtePdfService : IDtePdfService
{
    public byte[] Generar(string tipoDte, string json, string? sello)
    {
        var root = JObject.Parse(json);
        var identificacion = root["identificacion"] as JObject ?? new JObject();
        var emisor = root["emisor"] as JObject ?? root["donatario"] as JObject ?? new JObject();
        var receptor = root["receptor"] as JObject ?? root["sujetoExcluido"] as JObject ?? root["donante"] as JObject ?? new JObject();
        var codigo = identificacion.Value<string>("codigoGeneracion") ?? string.Empty;
        var numeroControl = identificacion.Value<string>("numeroControl") ?? string.Empty;
        var tipo = string.Equals(tipoDte, "INVALIDACION", StringComparison.OrdinalIgnoreCase) ? "INVALIDACION" : tipoDte.PadLeft(2, '0');
        var titulo = tipo == "INVALIDACION" ? "DOCUMENTO DE INVALIDACIÓN" : $"DOCUMENTO TRIBUTARIO ELECTRÓNICO {tipo}";

        return Document.Create(document => document.Page(page =>
        {
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO").Bold().FontSize(15).FontColor(Colors.Blue.Darken2);
                column.Item().Text(titulo).Bold().FontSize(12);
                column.Item().Text($"Ambiente: {identificacion.Value<string>("ambiente") ?? "-"}   Código: {codigo}");
            });
            page.Content().PaddingTop(12).Column(column =>
            {
                AgregarBloque(column, "Identificación", new[]
                {
                    ("Tipo DTE", identificacion.Value<string>("tipoDte") ?? tipo),
                    ("Número de control", numeroControl),
                    ("Fecha", identificacion.Value<string>("fecEmi") ?? identificacion.Value<string>("fecAnula") ?? "-"),
                    ("Hora", identificacion.Value<string>("horEmi") ?? identificacion.Value<string>("horAnula") ?? "-")
                });
                AgregarBloque(column, "Emisor", new[]
                {
                    ("NIT", emisor.Value<string>("nit") ?? "-"),
                    ("Nombre", emisor.Value<string>("nombre") ?? "-"),
                    ("Correo", emisor.Value<string>("correo") ?? "-")
                });
                if (tipo is "05" or "06")
                {
                    var relacionado = (root["documentoRelacionado"] as JArray)?.OfType<JObject>().FirstOrDefault();
                    var motivoNota = (root["resumen"] as JObject)?.Value<string>("observaciones");
                    AgregarBloque(column, tipo == "05" ? "Nota de credito" : "Nota de debito", new[]
                    {
                        ("Documento original", relacionado?.Value<string>("numeroDocumento") ?? "-"),
                        ("Fecha original", relacionado?.Value<string>("fechaEmision") ?? "-"),
                        ("Motivo", string.IsNullOrWhiteSpace(motivoNota) ? "-" : motivoNota)
                    });
                }
                if (tipo == "INVALIDACION")
                {
                    var documento = root["documento"] as JObject ?? new JObject();
                    var motivo = root["motivo"] as JObject ?? new JObject();
                    AgregarBloque(column, "Documento invalidado", new[]
                    {
                        ("Código original", documento.Value<string>("codigoGeneracion") ?? "-"),
                        ("Sello original", documento.Value<string>("selloRecibido") ?? "-"),
                        ("Motivo", motivo.Value<string>("motivoAnulacion") ?? "-")
                    });
                }
                else
                {
                    AgregarBloque(column, "Receptor", new[]
                    {
                        ("Documento", receptor.Value<string>("numDocumento") ?? receptor.Value<string>("nit") ?? "-"),
                        ("Nombre", receptor.Value<string>("nombre") ?? "-"),
                        ("Correo", receptor.Value<string>("correo") ?? "-")
                    });
                    AgregarDetalle(column, root["cuerpoDocumento"] as JArray);
                    var resumen = root["resumen"] as JObject;
                    if (resumen is not null)
                    {
                        AgregarBloque(column, "Resumen", new[]
                        {
                            ("Total", ObtenerValor(resumen, "totalPagar", "valorTotal", "totalCompra", "totalExenta")),
                            ("IVA", ObtenerValor(resumen, "totalIva", "totalIvaRetenido")),
                            ("Total en letras", resumen.Value<string>("totalLetras") ?? "-")
                        });
                    }
                }
                column.Item().PaddingTop(10).Text($"Sello de recepción: {sello ?? "Pendiente"}").FontSize(8);
                column.Item().PaddingTop(16).Text("Documento generado por SRAUMOAR. El JSON electrónico correspondiente se conserva como respaldo.").Italic().FontSize(8);
            });
            page.Footer().AlignCenter().Text(text => text.Span("SRAUMOAR - Facturación Electrónica").FontSize(8));
        })).GeneratePdf();
    }

    public string ObtenerNombre(string tipoDte, string codigoGeneracion, bool anulacion = false)
        => $"{(anulacion ? "INVALIDACION" : "DTE-" + tipoDte.PadLeft(2, '0'))}-{codigoGeneracion}.pdf";

    private static void AgregarBloque(ColumnDescriptor column, string titulo, IEnumerable<(string Etiqueta, string Valor)> datos)
    {
        column.Item().PaddingTop(8).Text(titulo).Bold().FontColor(Colors.Blue.Darken2);
        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(inner =>
        {
            foreach (var dato in datos)
                inner.Item().Text($"{dato.Etiqueta}: {dato.Valor ?? "-"}").FontSize(8);
        });
    }

    private static void AgregarDetalle(ColumnDescriptor column, JArray? items)
    {
        if (items is null || items.Count == 0) return;
        column.Item().PaddingTop(8).Text("Detalle").Bold().FontColor(Colors.Blue.Darken2);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.RelativeColumn(2);
                columns.RelativeColumn(4);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });
            table.Header(header =>
            {
                foreach (var titulo in new[] { "#", "Código", "Descripción", "Cantidad", "Valor" })
                    header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text(titulo).FontColor(Colors.White).FontSize(8);
            });
            foreach (var item in items.OfType<JObject>())
            {
                var cantidad = ObtenerValor(item, "cantidad");
                var valor = ObtenerValor(item, "ventaGravada", "ventaExenta", "valor", "compra");
                table.Cell().Padding(4).Text(item.Value<string>("numItem") ?? "-");
                table.Cell().Padding(4).Text(item.Value<string>("codigo") ?? "-");
                table.Cell().Padding(4).Text(item.Value<string>("descripcion") ?? "-");
                table.Cell().Padding(4).Text(cantidad);
                table.Cell().Padding(4).Text(valor);
            }
        });
    }

    private static string ObtenerValor(JObject objeto, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            var valor = objeto[nombre];
            if (valor is not null && valor.Type != JTokenType.Null) return valor.ToString();
        }
        return "-";
    }
}
