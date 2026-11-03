using QRCoder;
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
        var tipoIdentificacion = identificacion.Value<string>("tipoDte")
            ?? identificacion.Value<string>("tipoEvento");
        var tipo = string.Equals(tipoDte, "INVALIDACION", StringComparison.OrdinalIgnoreCase)
            ? "INVALIDACION"
            : (string.IsNullOrWhiteSpace(tipoIdentificacion) ? tipoDte : tipoIdentificacion).PadLeft(2, '0');

        if (tipo == "18")
            return GenerarEventoRetorno(root, identificacion, sello);

        var emisor = root["emisor"] as JObject ?? root["donatario"] as JObject ?? new JObject();
        var receptor = root["receptor"] as JObject ?? root["sujetoExcluido"] as JObject ?? root["donante"] as JObject ?? new JObject();
        var codigo = identificacion.Value<string>("codigoGeneracion") ?? string.Empty;
        var numeroControl = identificacion.Value<string>("numeroControl") ?? string.Empty;
        var titulo = tipo == "INVALIDACION" ? "DOCUMENTO DE INVALIDACIÓN" : $"DOCUMENTO TRIBUTARIO ELECTRÓNICO {tipo}";

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
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
                    ("NIT", emisor.Value<string>("nit") ?? emisor.Value<string>("numDocumento") ?? "-"),
                    ("NRC", emisor.Value<string>("nrc") ?? "-"),
                    ("Actividad", ObtenerValor(emisor, "descActividad", "codActividad")),
                    ("Dirección", Direccion(emisor)),
                    ("Teléfono", emisor.Value<string>("telefono") ?? "-"),
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
                        ("NRC", receptor.Value<string>("nrc") ?? "-"),
                        ("Actividad", ObtenerValor(receptor, "descActividad", "codActividad")),
                        ("Dirección", Direccion(receptor)),
                        ("Teléfono", receptor.Value<string>("telefono") ?? "-"),
                        ("Nombre", receptor.Value<string>("nombre") ?? "-"),
                        ("Correo", receptor.Value<string>("correo") ?? "-")
                    });
                    AgregarDetalle(column, root["cuerpoDocumento"] as JArray);
                    var resumen = root["resumen"] as JObject;
                    if (resumen is not null)
                    {
                        AgregarBloque(column, "Resumen", new[]
                        {
                            ("No sujeto", ObtenerValor(resumen, "totalNoSuj")),
                            ("Exento", ObtenerValor(resumen, "totalExenta")),
                            ("Gravado", ObtenerValor(resumen, "totalGravada")),
                            ("Compra", ObtenerValor(resumen, "totalCompra", "totalCompraExcluidos")),
                            ("Descuento", ObtenerValor(resumen, "totalDescu")),
                            ("Retención IVA", ObtenerValor(resumen, "ivaRete", "ivaRete1")),
                            ("Percepción IVA", ObtenerValor(resumen, "ivaPerci", "ivaPerci1")),
                            ("Retención renta", ObtenerValor(resumen, "reteRenta")),
                            ("Total", ObtenerValor(resumen, "totalPagar", "valorTotal", "totalCompra", "totalExenta")),
                            ("IVA", ObtenerValor(resumen, "totalIva", "totalIvaRetenido")),
                            ("Total en letras", resumen.Value<string>("totalLetras") ?? "-")
                        });
                        if (resumen["tributos"] is JArray tributos)
                            foreach (var tributo in tributos.OfType<JObject>())
                                column.Item().Text($"Tributo {ObtenerValor(tributo, "codigo")}: {ObtenerValor(tributo, "descripcion")} — {ObtenerValor(tributo, "valor")}").FontSize(8);
                    }
                }
                AgregarQr(column, tipo == "INVALIDACION" ? root["documento"] as JObject ?? identificacion : identificacion,
                    identificacion.Value<string>("ambiente"), sello);
                column.Item().PaddingTop(10).Text($"Sello de recepción: {sello ?? "Pendiente"}").FontSize(8);
                column.Item().PaddingTop(16).Text("Documento generado por SRAUMOAR. El JSON electrónico correspondiente se conserva como respaldo.").Italic().FontSize(8);
            });
            page.Footer().AlignCenter().Text(text => text.Span("SRAUMOAR - Facturación Electrónica").FontSize(8));
        })).GeneratePdf();
    }

    private static byte[] GenerarEventoRetorno(JObject root, JObject identificacion, string? sello)
    {
        var emisor = root["emisor"] as JObject ?? new JObject();
        var documento = root["documento"] as JObject ?? new JObject();
        var relacionados = root["documentoRelacionado"] as JArray;
        var cuerpo = root["cuerpoDocumento"] as JArray;
        var resumen = root["resumen"] as JObject ?? new JObject();
        var codigo = identificacion.Value<string>("codigoGeneracion") ?? "-";
        var ambiente = identificacion.Value<string>("ambiente") ?? "-";
        var ambienteTexto = ambiente == "00" ? "Pruebas (00)" : ambiente == "01" ? "Producción (01)" : ambiente;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(25);
            page.DefaultTextStyle(x => x.FontSize(8.5f));
            page.Header().Column(column =>
            {
                column.Item().Text("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(2).Text("EVENTO DE RETORNO")
                    .Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                column.Item().Text("Evento de Retorno de Mercancías (ERET) - Tipo 18").FontSize(9);
            });
            page.Content().PaddingTop(10).Column(column =>
            {
                column.Spacing(6);
                AgregarQr(column, identificacion, ambiente, sello);
                column.Item().Background(Colors.Blue.Lighten5).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(7).Column(info =>
                {
                    info.Item().Text($"Código de generación: {codigo}").Bold().FontSize(8);
                    info.Item().Text($"Sello de recepción: {sello ?? "Pendiente"}").FontSize(8);
                    info.Item().Text($"Emisión: {UnirFechaHora(identificacion)}   Ambiente: {ambienteTexto}").FontSize(8);
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(container => AgregarBloqueContenedor(container, "Emisor", new[]
                    {
                        ("NIT", emisor.Value<string>("nit") ?? "-"),
                        ("Nombre", emisor.Value<string>("nombre") ?? "-"),
                        ("Establecimiento", ObtenerValor(emisor, "codEstableMH", "codEstable")),
                        ("Punto de venta", ObtenerValor(emisor, "codPuntoVentaMH", "codPuntoVenta")),
                        ("Recinto fiscal", ObtenerValor(emisor, "recintoFiscal")),
                        ("Régimen", ObtenerValor(emisor, "regimen"))
                    }));
                    row.RelativeItem().PaddingLeft(7).Element(container => AgregarBloqueContenedor(container, "Documento / receptor", new[]
                    {
                        ("Documento", documento.Value<string>("numDocumento") ?? "-"),
                        ("Nombre", documento.Value<string>("nombre") ?? "-"),
                        ("País", documento.Value<string>("nombrePais") ?? documento.Value<string>("codPais") ?? "-"),
                        ("Teléfono", documento.Value<string>("telefono") ?? "-"),
                        ("Correo", documento.Value<string>("correo") ?? "-")
                    }));
                });

                AgregarDocumentosRelacionados(column, relacionados);
                AgregarDetalleRetorno(column, cuerpo);
                AgregarResumenRetorno(column, resumen);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("SRAUMOAR - Evento de Retorno - ").FontSize(7.5f);
                text.CurrentPageNumber().FontSize(7.5f);
                text.Span(" / ").FontSize(7.5f);
                text.TotalPages().FontSize(7.5f);
            });
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

    private static void AgregarBloqueContenedor(IContainer container, string titulo, IEnumerable<(string Etiqueta, string Valor)> datos)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(column =>
        {
            column.Item().Text(titulo).Bold().FontColor(Colors.Blue.Darken2).FontSize(9);
            foreach (var dato in datos)
                column.Item().PaddingTop(1).Text($"{dato.Etiqueta}: {dato.Valor}").FontSize(7.5f);
        });
    }

    private static void AgregarDocumentosRelacionados(ColumnDescriptor column, JArray? documentos)
    {
        if (documentos is null || documentos.Count == 0) return;

        column.Item().Text("Documento relacionado").Bold().FontColor(Colors.Blue.Darken2).FontSize(9);
        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(inner =>
        {
            foreach (var documento in documentos.OfType<JObject>())
            {
                inner.Item().Text(
                    $"Tipo {documento.Value<string>("tipoDocumento") ?? "-"} | " +
                    $"Código: {documento.Value<string>("codigoGeneracion") ?? "-"} | " +
                    $"Fecha: {documento.Value<string>("fechaEmision") ?? "-"}")
                    .FontSize(7.5f);
            }
        });
    }

    private static void AgregarDetalleRetorno(ColumnDescriptor column, JArray? items)
    {
        if (items is null || items.Count == 0) return;

        column.Item().Text("Detalle de mercancías retornadas").Bold().FontColor(Colors.Blue.Darken2).FontSize(9);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(26);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(3.5f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.25f);
                columns.RelativeColumn(1.35f);
            });
            table.Header(header =>
            {
                foreach (var titulo in new[] { "#", "Código", "Descripción", "Cantidad", "P. unitario", "Total" })
                    header.Cell().Background(Colors.Blue.Darken2).Padding(3).Text(titulo).FontColor(Colors.White).Bold().FontSize(7);
            });

            foreach (var item in items.OfType<JObject>())
            {
                var total = ObtenerMontoItem(item);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(ObtenerValor(item, "numItem")).FontSize(7);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(ObtenerValor(item, "codigo")).FontSize(7);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Column(description =>
                {
                    description.Item().Text(ObtenerValor(item, "descripcion")).FontSize(7);
                    var codigoRelacionado = item.Value<string>("codigoGeneracion");
                    if (!string.IsNullOrWhiteSpace(codigoRelacionado))
                        description.Item().Text($"DTE: {codigoRelacionado}").Italic().FontSize(6).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text(ObtenerValor(item, "cantidad")).FontSize(7);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text(FormatearMoneda(item["precioUni"])).FontSize(7);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text(FormatearMoneda(total)).FontSize(7).Bold();
            }
        });
    }

    private static void AgregarResumenRetorno(ColumnDescriptor column, JObject resumen)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(left =>
            {
                left.Item().Text("Total en letras").Bold().FontColor(Colors.Blue.Darken2).FontSize(8);
                left.Item().Text(resumen.Value<string>("totalLetras") ?? "-").FontSize(7.5f);
            });
            row.RelativeItem().PaddingLeft(7).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(right =>
            {
                right.Item().Text("Resumen").Bold().FontColor(Colors.Blue.Darken2).FontSize(8);
                AgregarLineaTotal(right, "No sujeto", resumen["totalNoSuj"]);
                AgregarLineaTotal(right, "Exento", resumen["totalExenta"]);
                AgregarLineaTotal(right, "Gravado", resumen["totalGravada"]);
                AgregarLineaTotal(right, "Compras excluidos", resumen["totalCompraExcluidos"]);
                AgregarLineaTotal(right, "IVA", resumen["totalIva"]);
                AgregarLineaTotal(right, "Seguro", resumen["totalSeguro"]);
                AgregarLineaTotal(right, "Flete", resumen["totalFlete"]);
                AgregarLineaTotal(right, "Monto operación", resumen["montoTotalOperacion"]);
                AgregarLineaTotal(right, "Total a pagar", resumen["totalPagar"], true);
            });
        });
    }

    private static void AgregarLineaTotal(ColumnDescriptor column, string etiqueta, JToken? valor, bool destacado = false)
    {
        if (valor is null || valor.Type == JTokenType.Null) return;
        if (!destacado && decimal.TryParse(valor.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numero) && numero == 0m) return;

        column.Item().Row(row =>
        {
            var etiquetaContenedor = row.RelativeItem();
            var valorContenedor = row.RelativeItem().AlignRight();
            if (destacado)
            {
                etiquetaContenedor.Text(etiqueta).Bold().FontSize(8);
                valorContenedor.Text(FormatearMoneda(valor)).Bold().FontSize(8);
            }
            else
            {
                etiquetaContenedor.Text(etiqueta).FontSize(7);
                valorContenedor.Text(FormatearMoneda(valor)).FontSize(7);
            }
        });
    }

    private static string UnirFechaHora(JObject identificacion)
    {
        var fecha = identificacion.Value<string>("fecEmi") ?? "-";
        var hora = identificacion.Value<string>("horEmi");
        return string.IsNullOrWhiteSpace(hora) ? fecha : $"{fecha} {hora}";
    }

    private static string FormatearMoneda(JToken? valor)
        => decimal.TryParse(valor?.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var numero)
            ? $"${numero:N2}"
            : "-";

    private static string FormatearMoneda(string valor)
        => decimal.TryParse(valor, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var numero)
            ? $"${numero:N2}"
            : "-";

    private static string ObtenerMontoItem(JObject item)
    {
        static decimal Numero(JToken? valor)
        {
            if (valor?.Type is JTokenType.Integer or JTokenType.Float)
                return valor.Value<decimal>();
            return decimal.TryParse(valor?.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numero) ? numero : 0m;
        }

        // Las clasificaciones pueden coexistir y una gravada presente con valor
        // cero no debe ocultar una venta exenta/no sujeta ni una compra excluida.
        var nombres = new[] { "ventaGravada", "ventaExenta", "ventaNoSuj", "compra" };
        decimal total = 0m;
        var tieneClasificacion = false;
        foreach (var nombre in nombres)
        {
            var valor = item[nombre];
            if (valor is null || valor.Type == JTokenType.Null) continue;
            tieneClasificacion = true;
            total += Numero(valor);
        }

        if (!tieneClasificacion) total = Numero(item["valor"]);
        return total.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AgregarDetalle(ColumnDescriptor column, JArray? items)
    {
        if (items is null || items.Count == 0) return;
        column.Item().PaddingTop(8).Text("Detalle").Bold().FontColor(Colors.Blue.Darken2);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(20);
                columns.RelativeColumn(4);
                for (var i = 0; i < 9; i++) columns.RelativeColumn();
            });
            table.Header(header =>
            {
                foreach (var titulo in new[] { "#", "Descripción / código", "Cant.", "Precio", "Desc.", "No suj.", "Exento", "Gravado", "Compra", "IVA", "Valor" })
                    header.Cell().Background(Colors.Blue.Darken2).Padding(2).Text(titulo).FontColor(Colors.White).FontSize(6);
            });
            foreach (var item in items.OfType<JObject>())
            {
                var valores = new[] { ObtenerValor(item, "numItem"), $"{ObtenerValor(item, "descripcion")} / {ObtenerValor(item, "codigo")}",
                    ObtenerValor(item, "cantidad"), ObtenerValor(item, "precioUni"), ObtenerValor(item, "montoDescu"),
                    ObtenerValor(item, "ventaNoSuj"), ObtenerValor(item, "ventaExenta"), ObtenerValor(item, "ventaGravada"),
                    ObtenerValor(item, "compra"), ObtenerValor(item, "totalIva", "ivaItem"), ObtenerMontoItem(item) };
                foreach (var valor in valores) table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(valor).FontSize(6);
            }
        });
    }

    internal static string Direccion(JObject persona)
    {
        if (persona["direccion"] is not JObject direccion) return "-";
        return CatalogoTerritorialFiscal.Formatear(direccion);
    }

    internal static string? UrlConsulta(JObject identificacion, string? ambiente)
    {
        var codigo = identificacion.Value<string>("codigoGeneracion");
        var fecha = identificacion.Value<string>("fecEmi");
        if (ambiente is not ("00" or "01") || string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(fecha)) return null;
        return $"https://admin.factura.gob.sv/consultaPublica?ambiente={ambiente}&codGen={Uri.EscapeDataString(codigo)}&fechaEmi={Uri.EscapeDataString(fecha)}";
    }

    private static void AgregarQr(ColumnDescriptor column, JObject identificacion, string? ambiente, string? sello)
    {
        var url = UrlConsulta(identificacion, ambiente);
        if (url is null || string.IsNullOrWhiteSpace(sello))
        {
            column.Item().Text("Sin confirmación de recepción fiscal").Bold().FontColor(Colors.Orange.Darken3);
            return;
        }
        using var datos = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(datos);
        column.Item().PaddingTop(8).Row(row =>
        {
            row.ConstantItem(72).Image(qr.GetGraphic(4));
            row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(info =>
            {
                info.Item().Text("Consulte este documento en Hacienda").Bold().FontSize(8);
                info.Item().Hyperlink(url).Text(url).FontSize(7).FontColor(Colors.Blue.Darken2);
            });
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
