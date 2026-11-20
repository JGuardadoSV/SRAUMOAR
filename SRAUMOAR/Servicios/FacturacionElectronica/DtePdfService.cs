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
/// Renderizador institucional y oficial de Documentos Tributarios Electrónicos (DTE).
/// Diseñado con estética académica de alto nivel, encabezado unificado con sello de recepción,
/// y soporte integral adaptado según las normativas del Ministerio de Hacienda para:
/// - Factura Electrónica (01)
/// - Comprobante de Crédito Fiscal (03)
/// - Nota de Crédito / Débito (05/06)
/// - Comprobante de Retención (07)
/// - Factura de Exportación (11)
/// - Factura de Sujeto Excluido (14) -> Sin IVA, con Retención de Renta
/// - Comprobante de Donación (15) -> Sin IVA, con tipo de donación y documentos asociados
/// - Evento de Retorno (18)
/// - Documento de Invalidación
/// </summary>
public sealed class DtePdfService : IDtePdfService
{
    private const string VerdeInstitucional = "#0B4D1A";
    private const string VerdeMedio = "#1B5E20";
    private const string VerdeClaro = "#E8F5E9";
    private const string TextoOscuro = "#1E293B";
    private const string TextoMuted = "#64748B";
    private const string BordeSuave = "#E2E8F0";
    private const string FondoGris = "#F8FAFC";

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
        var numControl = Valor(identificacion, "numeroControl", "numeroControlDte");
        var titulo = tipo == "INVALIDACION" ? "DOCUMENTO DE INVALIDACIÓN" : TipoDocumento(tipo);
        var badgeTipo = tipo == "INVALIDACION" ? "ANULADO" : $"DTE {tipo}";
        var fechaHora = FechaHora(identificacion);
        var ambiente = Ambiente(Valor(identificacion, "ambiente"));

        var urlConsulta = UrlConsulta(tipo == "INVALIDACION"
            ? root["documento"] as JObject ?? identificacion : identificacion,
            identificacion.Value<string>("ambiente"));
        var qrBytes = GenerarQr(urlConsulta);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(8).FontColor(TextoOscuro));

            // ENCABEZADO INSTITUCIONAL ELEGANTE
            page.Header().Column(header =>
            {
                // Franja superior institucional
                header.Item().Height(4).Background(VerdeInstitucional);
                header.Item().PaddingTop(10).Row(row =>
                {
                    // Lado Izquierdo: Marca Institucional y Emisor
                    row.RelativeItem(1.3f).Row(left =>
                    {
                        var logo = ObtenerLogo();
                        if (logo != null)
                        {
                            left.ConstantItem(48).Height(56).Image(logo).FitArea();
                            left.ConstantItem(10);
                        }
                        left.RelativeItem().Column(col =>
                        {
                            col.Item().Text(Valor(emisor, "nombre", "razonSocial", "nombreComercial"))
                                .Bold().FontSize(10.5f).FontColor(VerdeInstitucional);
                            col.Item().PaddingTop(1).Text("EDUCACIÓN SUPERIOR UNIVERSITARIA")
                                .Bold().FontSize(7).FontColor(VerdeMedio);
                            col.Item().PaddingTop(3).Text($"NIT: {Valor(emisor, "nit")}   ·   NRC: {Valor(emisor, "nrc")}")
                                .Bold().FontSize(7.5f).FontColor(TextoOscuro);
                            col.Item().PaddingTop(1).Text(Direccion(emisor))
                                .FontSize(7).FontColor(TextoMuted);
                            var contacto = string.Join("   ·   ", new[] { Valor(emisor, "telefono"), Valor(emisor, "correo", "email") }.Where(x => x != "-"));
                            if (!string.IsNullOrWhiteSpace(contacto))
                                col.Item().Text(contacto).FontSize(7).FontColor(TextoMuted);
                        });
                    });

                    // Lado Derecho: Tarjeta Fiscal con Sello de Recepción
                    row.ConstantItem(225).Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(right =>
                    {
                        right.Item().Row(r =>
                        {
                            r.RelativeItem().Text(titulo)
                                .Bold().FontSize(10.5f).FontColor(VerdeInstitucional);
                            r.ConstantItem(46).Background(VerdeClaro).PaddingVertical(2).PaddingHorizontal(3)
                                .AlignCenter().Text(badgeTipo).Bold().FontSize(6.5f).FontColor(VerdeInstitucional);
                        });

                        right.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("N° Control: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(numControl).Bold().FontSize(7.2f).FontColor(TextoOscuro);
                        });
                        right.Item().PaddingTop(1.5f).Text(t =>
                        {
                            t.Span("Cód. Gen: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(codigo).FontSize(6.5f).FontColor(TextoOscuro);
                        });
                        right.Item().PaddingTop(1.5f).Text(t =>
                        {
                            t.Span("Sello Recep: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(string.IsNullOrWhiteSpace(sello) ? "Pendiente" : sello)
                                .FontSize(6.3f).FontColor(VerdeMedio).Bold();
                        });
                        right.Item().PaddingTop(1.5f).Row(meta =>
                        {
                            meta.RelativeItem().Text(t =>
                            {
                                t.Span("Fecha: ").FontSize(6.8f).FontColor(TextoMuted);
                                t.Span(fechaHora).FontSize(6.8f).FontColor(TextoOscuro);
                            });
                            meta.ConstantItem(68).AlignRight().Text(ambiente)
                                .FontSize(6.8f).FontColor(VerdeMedio).Bold();
                        });
                    });
                });

                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(BordeSuave);
            });

            // CONTENIDO
            page.Content().PaddingTop(10).Column(column =>
            {
                column.Spacing(9);

                // Sección Receptor
                AgregarSeccionReceptor(column, receptor, tipo);

                // Documentos vinculados o autorizaciones específicas
                if (tipo is "05" or "06") AgregarNota(column, root, tipo);
                if (tipo == "15") AgregarOtrosDocumentosDonacion(column, root);
                if (tipo == "INVALIDACION") AgregarInvalidacion(column, root);
                else
                {
                    AgregarDetalleSegunTipo(column, root["cuerpoDocumento"] as JArray, tipo);
                    AgregarResumenSegunTipo(column, root["resumen"] as JObject, tipo);
                }

                AgregarQrYValidacion(column, qrBytes, urlConsulta);
            });

            // PIE DE PÁGINA
            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(1).LineColor(BordeSuave);
                f.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Universidad Monseñor Oscar Arnulfo Romero  ·  SRAUMOAR Facturación Electrónica")
                        .FontSize(6.8f).FontColor(TextoMuted);
                    row.ConstantItem(80).AlignRight().Text(text =>
                    {
                        text.Span("Página ").FontSize(6.8f).FontColor(TextoMuted);
                        text.CurrentPageNumber().FontSize(6.8f).FontColor(TextoMuted);
                        text.Span(" de ").FontSize(6.8f).FontColor(TextoMuted);
                        text.TotalPages().FontSize(6.8f).FontColor(TextoMuted);
                    });
                });
            });
        })).GeneratePdf();
    }

    public string ObtenerNombre(string tipoDte, string codigoGeneracion, bool anulacion = false)
        => $"{(anulacion ? "INVALIDACION" : "DTE-" + tipoDte.PadLeft(2, '0'))}-{codigoGeneracion}.pdf";

    private static void AgregarSeccionReceptor(ColumnDescriptor column, JObject receptor, string tipo)
    {
        var tituloSeccion = tipo switch
        {
            "03" => "DATOS DEL RECEPTOR / CONTRIBUYENTE (CRÉDITO FISCAL)",
            "14" => "DATOS DEL SUJETO EXCLUIDO (PROVEEDOR / VENDEDOR)",
            "15" => "DATOS DEL DONANTE",
            "INVALIDACION" => "DATOS DEL RECEPTOR DE LA INVALIDACIÓN",
            _ => "DATOS DEL RECEPTOR / CLIENTE"
        };

        var subtitulo = tipo switch
        {
            "03" => "CONTRIBUYENTE IVA",
            "14" => "SUJETO EXCLUIDO DE IVA",
            "15" => "DONANTE",
            _ => "CONSUMIDOR FINAL"
        };

        column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Text(tituloSeccion).Bold().FontSize(7.5f).FontColor(VerdeInstitucional);
                r.ConstantItem(150).AlignRight().Text(subtitulo).FontSize(6.5f).FontColor(TextoMuted);
            });

            c.Item().PaddingTop(4).Row(r =>
            {
                // Columna 1
                r.RelativeItem(1.8f).Column(left =>
                {
                    var nombre = Valor(receptor, "nombre", "razonSocial", "nombreComercial");
                    if (nombre == "-") nombre = tipo == "01" ? "Cliente General / Consumidor Final" : "-";
                    left.Item().Text(t =>
                    {
                        t.Span(tipo == "14" ? "Proveedor / Sujeto: " : (tipo == "15" ? "Donante: " : "Nombre o Razón Social: "))
                            .FontSize(7).FontColor(TextoMuted);
                        t.Span(nombre).Bold().FontSize(7.5f).FontColor(TextoOscuro);
                    });

                    var actividad = Valor(receptor, "descActividad", "actividadEconomica");
                    if (actividad != "-")
                    {
                        left.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Giro / Actividad: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(actividad).FontSize(7f).FontColor(TextoMuted);
                        });
                    }

                    var dir = Direccion(receptor);
                    if (dir != "-")
                    {
                        left.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Dirección: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(dir).FontSize(7f).FontColor(TextoMuted);
                        });
                    }
                });

                // Columna 2
                r.RelativeItem(1.4f).Column(right =>
                {
                    var nit = Valor(receptor, "nit");
                    var nrc = Valor(receptor, "nrc");
                    var doc = Valor(receptor, "numDocumento", "numeroDocumento");

                    if (tipo == "03")
                    {
                        right.Item().Text(t =>
                        {
                            t.Span("NIT: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(nit != "-" ? nit : doc).Bold().FontSize(7.5f);
                            t.Span("   NRC: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(nrc).Bold().FontSize(7.5f);
                        });
                    }
                    else
                    {
                        right.Item().Text(t =>
                        {
                            t.Span("Documento (DUI/NIT): ").FontSize(7).FontColor(TextoMuted);
                            t.Span(doc != "-" ? doc : (nit != "-" ? nit : "-")).Bold().FontSize(7.5f);
                        });
                    }

                    var correo = Valor(receptor, "correo", "email");
                    if (correo != "-")
                    {
                        right.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Correo: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(correo).Bold().FontSize(7.2f).FontColor(TextoOscuro);
                        });
                    }

                    var tel = Valor(receptor, "telefono");
                    if (tel != "-")
                    {
                        right.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Teléfono: ").FontSize(7).FontColor(TextoMuted);
                            t.Span(tel).FontSize(7f);
                        });
                    }
                });
            });
        });
    }

    private static void AgregarOtrosDocumentosDonacion(ColumnDescriptor column, JObject root)
    {
        var otros = root["otrosDocumentos"] as JArray;
        if (otros is null || otros.Count == 0) return;

        column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(7).Column(c =>
        {
            c.Item().Text("DOCUMENTO ASOCIADO / AUTORIZACIÓN DE DONACIÓN").Bold().FontSize(7.2f).FontColor(VerdeInstitucional);
            foreach (var doc in otros.OfType<JObject>())
            {
                c.Item().PaddingTop(2).Text(t =>
                {
                    t.Span(Valor(doc, "descDocumento")).Bold().FontSize(7f);
                    t.Span("  ·  Detalle: ").FontSize(6.8f).FontColor(TextoMuted);
                    t.Span(Valor(doc, "detalleDocumento")).FontSize(7f);
                });
            }
        });
    }

    private static void AgregarNota(ColumnDescriptor column, JObject root, string tipo)
    {
        var relacionado = (root["documentoRelacionado"] as JArray)?.OfType<JObject>().FirstOrDefault();
        var resumen = root["resumen"] as JObject;
        var titulo = tipo == "05" ? "DOCUMENTO ORIGINAL MODIFICADO POR NOTA DE CRÉDITO" : "DOCUMENTO ORIGINAL MODIFICADO POR NOTA DE DÉBITO";

        column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(c =>
        {
            c.Item().Text(titulo).Bold().FontSize(7.5f).FontColor(VerdeInstitucional);
            c.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Column(col =>
                {
                    col.Item().Text("DOCUMENTO VINCULADO").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(relacionado, "numeroDocumento", "codigoGeneracion")).Bold().FontSize(7.5f);
                });
                r.RelativeItem().Column(col =>
                {
                    col.Item().Text("FECHA ORIGINAL").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(relacionado, "fechaEmision")).FontSize(7.5f);
                });
                r.RelativeItem(1.5f).Column(col =>
                {
                    col.Item().Text("MOTIVO DE LA OPERACIÓN").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(resumen, "observaciones", "descripcion")).FontSize(7.5f);
                });
            });
        });
    }

    private static void AgregarInvalidacion(ColumnDescriptor column, JObject root)
    {
        var documento = root["documento"] as JObject ?? new JObject();
        var motivo = root["motivo"] as JObject ?? new JObject();

        column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(c =>
        {
            c.Item().Text("DETALLE DEL DOCUMENTO INVALIDADO").Bold().FontSize(7.5f).FontColor(VerdeInstitucional);
            c.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem(1.4f).Column(col =>
                {
                    col.Item().Text("CÓDIGO DE GENERACIÓN ORIGINAL").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(documento, "codigoGeneracion")).Bold().FontSize(7.5f);
                    col.Item().PaddingTop(3).Text("SELLO ORIGINAL").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(documento, "selloRecibido", "sello")).FontSize(7f).FontColor(VerdeMedio).Bold();
                });
                r.RelativeItem(1.6f).Column(col =>
                {
                    col.Item().Text("MOTIVO DE ANULACIÓN").FontSize(6.5f).FontColor(TextoMuted);
                    col.Item().Text(Valor(motivo, "motivoAnulacion", "descripcionMotivo")).FontSize(7.5f).Bold();
                });
            });
        });
    }

    private static void AgregarDetalleSegunTipo(ColumnDescriptor column, JArray? items, string tipo)
    {
        if (items is null || items.Count == 0) return;

        column.Item().Table(table =>
        {
            if (tipo == "14")
            {
                // DTE 14: SUJETO EXCLUIDO (COMPRA)
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(24);
                    cols.RelativeColumn(4.2f);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(60);
                    cols.ConstantColumn(55);
                    cols.ConstantColumn(65);
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "#", "Descripción del bien o servicio", "Cant.", "Precio Unit.", "Descuento", "Compra Total" })
                        header.Cell().Background(VerdeInstitucional).PaddingVertical(5).PaddingHorizontal(4)
                            .Text(title).Bold().FontSize(7).FontColor(Colors.White);
                });

                int n = 1;
                foreach (var item in items.OfType<JObject>())
                {
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignCenter().Text(n++.ToString()).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).Column(detail =>
                    {
                        detail.Item().Text(Valor(item, "descripcion", "detalle")).Bold().FontSize(7.5f);
                        var code = Valor(item, "codigo", "codigoItem");
                        if (code != "-") detail.Item().Text($"Código: {code}").FontSize(6.5f).FontColor(TextoMuted);
                    });
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Valor(item, "cantidad")).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["precioUni"])).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["montoDescu"])).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["compra"] ?? item["precioUni"])).Bold().FontSize(7.5f);
                }
            }
            else if (tipo == "15")
            {
                // DTE 15: COMPROBANTE DE DONACIÓN
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(24);
                    cols.RelativeColumn(4.5f);
                    cols.ConstantColumn(45);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "#", "Descripción de la donación o bien", "Cant.", "Valor Unitario", "Valor Donado" })
                        header.Cell().Background(VerdeInstitucional).PaddingVertical(5).PaddingHorizontal(4)
                            .Text(title).Bold().FontSize(7).FontColor(Colors.White);
                });

                int n = 1;
                foreach (var item in items.OfType<JObject>())
                {
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignCenter().Text(n++.ToString()).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).Column(detail =>
                    {
                        detail.Item().Text(Valor(item, "descripcion", "detalle")).Bold().FontSize(7.5f);
                        var code = Valor(item, "codigo");
                        var tipoDon = Valor(item, "tipoDonacion");
                        var descTipo = tipoDon == "1" ? "Efectivo / Dinero" : (tipoDon == "2" ? "Bienes Muebles" : (tipoDon == "3" ? "Inmuebles" : tipoDon));
                        if (tipoDon != "-") detail.Item().Text($"Tipo: {descTipo}   ·   Código: {code}").FontSize(6.5f).FontColor(TextoMuted);
                    });
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Valor(item, "cantidad")).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["valorUni"] ?? item["valor"])).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["valor"] ?? item["valorUni"])).Bold().FontSize(7.5f);
                }
            }
            else
            {
                // DTE 01, 03, 05, 06, 07, etc.
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(24);
                    cols.RelativeColumn(4f);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(58);
                    cols.ConstantColumn(55);
                    cols.ConstantColumn(65);
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "#", "Descripción del producto o servicio", "Cant.", "Precio Unit.", "Descuento", "Venta Total" })
                        header.Cell().Background(VerdeInstitucional).PaddingVertical(5).PaddingHorizontal(4)
                            .Text(title).Bold().FontSize(7).FontColor(Colors.White);
                });

                int n = 1;
                foreach (var item in items.OfType<JObject>())
                {
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignCenter().Text(n++.ToString()).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).Column(detail =>
                    {
                        detail.Item().Text(Valor(item, "descripcion", "detalle")).Bold().FontSize(7.5f);
                        var code = Valor(item, "codigo", "codigoItem");
                        if (code != "-") detail.Item().Text($"Código: {code}").FontSize(6.5f).FontColor(TextoMuted);
                    });
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Valor(item, "cantidad")).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["precioUni"])).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["montoDescu"])).FontSize(7.5f);
                    table.Cell().BorderBottom(1).BorderColor(BordeSuave).Padding(5).AlignRight().Text(Moneda(item["ventaGravada"] ?? item["precioUni"])).Bold().FontSize(7.5f);
                }
            }
        });
    }

    private static void AgregarResumenSegunTipo(ColumnDescriptor column, JObject? resumen, string tipo)
    {
        if (resumen is null) return;

        column.Item().Row(row =>
        {
            // Izquierda: Total en letras y condiciones
            row.RelativeItem(1.3f).Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(left =>
            {
                left.Item().Text("TOTAL EN LETRAS").Bold().FontSize(7).FontColor(VerdeInstitucional);
                left.Item().PaddingTop(3).Text(Valor(resumen, "totalLetras").ToUpperInvariant())
                    .Bold().FontSize(8).FontColor(TextoOscuro);

                left.Item().PaddingTop(8).Text(tipo == "15" ? "FORMA DE DONACIÓN" : "CONDICIONES DE LA OPERACIÓN")
                    .Bold().FontSize(7).FontColor(VerdeInstitucional);

                var condicion = Valor(resumen, "condicionOperacion") == "2" ? "Crédito" : "Contado / Efectivo";
                if (tipo == "15") condicion = "Donación Directa / Transferencia";
                left.Item().PaddingTop(2).Text($"Forma de entrega: {condicion}   ·   Moneda: USD ($)").FontSize(7).FontColor(TextoMuted);

                var obs = Valor(resumen, "observaciones");
                if (obs != "-")
                {
                    left.Item().PaddingTop(4).Text("Observaciones:").FontSize(6.5f).FontColor(TextoMuted);
                    left.Item().Text(obs).FontSize(7f);
                }
            });

            row.ConstantItem(12);

            // Derecha: Desglose financiero formal adaptado según el tipo de DTE
            row.RelativeItem(1f).Border(1).BorderColor(BordeSuave).Padding(8).Column(right =>
            {
                if (tipo == "14")
                {
                    // DTE 14: SUJETO EXCLUIDO (NO IVA, RETENCIÓN DE RENTA)
                    Linea(right, "Total Compra:", Moneda(resumen["totalCompra"]));
                    var descu = resumen["totalDescu"] ?? resumen["descu"];
                    if (TieneMonto(descu))
                        Linea(right, "Descuento:", Moneda(descu));

                    var subTotal = resumen["subTotal"];
                    if (TieneMonto(subTotal))
                        Linea(right, "Sub-Total:", Moneda(subTotal));

                    var reteRenta = resumen["reteRenta"];
                    if (TieneMonto(reteRenta))
                        Linea(right, "(-) Retención Renta (10%):", Moneda(reteRenta), true);

                    right.Item().PaddingTop(3).Height(1).Background(BordeSuave);
                    right.Item().PaddingTop(3).Row(total =>
                    {
                        total.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(9).FontColor(VerdeInstitucional);
                        total.ConstantItem(75).AlignRight().Text(Moneda(resumen["totalPagar"])).Bold().FontSize(10).FontColor(VerdeInstitucional);
                    });
                }
                else if (tipo == "15")
                {
                    // DTE 15: COMPROBANTE DE DONACIÓN (SOLO VALOR TOTAL DONADO)
                    right.Item().PaddingTop(6).Text("VALOR TOTAL DONACIÓN").Bold().FontSize(8).FontColor(VerdeInstitucional);
                    right.Item().PaddingTop(4).Row(total =>
                    {
                        total.RelativeItem().Text("TOTAL DONADO:").Bold().FontSize(9.5f).FontColor(TextoOscuro);
                        total.ConstantItem(75).AlignRight().Text(Moneda(resumen["valorTotal"])).Bold().FontSize(11).FontColor(VerdeInstitucional);
                    });
                    right.Item().PaddingTop(8).Text("Documento exento de IVA por donación institucional.")
                        .Italic().FontSize(6.5f).FontColor(TextoMuted);
                }
                else
                {
                    // DTE 01, 03, 05, etc.
                    Linea(right, "Ventas no sujetas:", Moneda(resumen["totalNoSuj"]));
                    Linea(right, "Ventas exentas:", Moneda(resumen["totalExenta"]));
                    Linea(right, "Ventas gravadas:", Moneda(resumen["totalGravada"]));
                    Linea(right, "Descuentos:", Moneda(resumen["totalDescu"]));

                    if (tipo == "03")
                        Linea(right, "13% Débito Fiscal:", Moneda(resumen["totalIva"]));
                    else
                        Linea(right, "IVA (13% incluido):", Moneda(resumen["totalIva"]));

                    // Mostrar siempre la retención para que el comprobante deje
                    // explícito cuando no aplica (0.00) o cuando fue calculada.
                    Linea(right, "(-) Retención IVA (gran contribuyente):", Moneda(resumen["ivaRete"]), true);
                    if (TieneMonto(resumen["ivaPerci"]))
                        Linea(right, "(+) Percepción IVA:", Moneda(resumen["ivaPerci"]));

                    right.Item().PaddingTop(3).Height(1).Background(BordeSuave);
                    right.Item().PaddingTop(3).Row(total =>
                    {
                        total.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(9).FontColor(VerdeInstitucional);
                        var valTotal = resumen["totalPagar"] ?? resumen["montoTotalOperacion"];
                        total.ConstantItem(75).AlignRight().Text(Moneda(valTotal)).Bold().FontSize(10).FontColor(VerdeInstitucional);
                    });
                }
            });
        });
    }

    private static bool TieneMonto(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null) return false;
        return decimal.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && n > 0m;
    }

    private static void Linea(ColumnDescriptor column, string etiqueta, string valor, bool destacar = false)
    {
        if (valor == "$0.00" && !destacar && !etiqueta.Contains("Gravadas") && !etiqueta.Contains("Compra")) return;
        column.Item().Row(row =>
        {
            var lbl = row.RelativeItem().Text(etiqueta).FontSize(7.5f).FontColor(TextoMuted);
            var val = row.ConstantItem(70).AlignRight().Text(valor).FontSize(7.5f);
            if (destacar)
            {
                lbl.Bold().FontColor(TextoOscuro);
                val.Bold().FontColor(VerdeMedio);
            }
        });
    }

    private static void AgregarQrYValidacion(ColumnDescriptor column, byte[]? qrBytes, string? urlConsulta)
    {
        column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Row(r =>
        {
            if (qrBytes != null)
            {
                r.ConstantItem(60).Height(60).Image(qrBytes).FitArea();
                r.ConstantItem(10);
            }
            r.RelativeItem().Column(val =>
            {
                val.Item().Text("CONSULTA Y VALIDACIÓN OFICIAL DE HACIENDA").Bold().FontSize(7.5f).FontColor(VerdeInstitucional);
                val.Item().PaddingTop(2).Text("Representación gráfica oficial de Documento Tributario Electrónico (DTE) transmitido y validado ante el Ministerio de Hacienda de El Salvador.")
                    .FontSize(6.8f).FontColor(TextoMuted);
                if (!string.IsNullOrWhiteSpace(urlConsulta))
                    val.Item().PaddingTop(3).Text(urlConsulta).FontSize(6.3f).FontColor(VerdeInstitucional);
                val.Item().PaddingTop(2).Text("El archivo electrónico .JSON firmado digitalmente constituye el respaldo tributario con valor legal.")
                    .Italic().FontSize(6.3f).FontColor(TextoMuted);
            });
        });
    }

    private static string TipoDocumento(string tipo) => tipo switch
    {
        "01" => "FACTURA ELECTRÓNICA",
        "03" => "COMPROBANTE DE CRÉDITO FISCAL",
        "05" => "NOTA DE CRÉDITO",
        "06" => "NOTA DE DÉBITO",
        "07" => "COMPROBANTE DE RETENCIÓN",
        "08" => "COMPROBANTE DE LIQUIDACIÓN",
        "09" => "DOCUMENTO CONTABLE DE LIQUIDACIÓN",
        "11" => "FACTURA DE EXPORTACIÓN",
        "14" => "FACTURA DE SUJETO EXCLUIDO",
        "15" => "COMPROBANTE DE DONACIÓN",
        _ => $"DOCUMENTO TRIBUTARIO ELECTRÓNICO {tipo}"
    };

    private static string Ambiente(string value) => value switch
    {
        "00" => "Pruebas (00)",
        "01" => "Producción (01)",
        _ => value == "-" ? "-" : value
    };

    private static string FechaHora(JObject value)
    {
        var fecha = Valor(value, "fecEmi", "fecAnula");
        var hora = Valor(value, "horEmi", "horAnula");
        return hora == "-" ? fecha : $"{fecha} {hora}";
    }

    private static string Direccion(JObject persona)
    {
        if (persona["direccion"] is JObject direccion)
        {
            try
            {
                var formateada = CatalogoTerritorialFiscal.Formatear(direccion);
                if (!string.IsNullOrWhiteSpace(formateada)) return formateada;
            }
            catch
            {
                // Fallback si no está en catálogo o es formato previo
            }
            var comp = Valor(direccion, "complemento");
            if (comp != "-") return comp;
        }
        return Valor(persona, "direccion");
    }

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
            if (valor is not null && valor.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(valor.ToString()))
                return valor.ToString();
        }
        return "-";
    }

    private static string Moneda(object? valor)
    {
        if (valor == null) return "$0.00";
        return decimal.TryParse(valor.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numero) ? $"${numero:N2}" : "$0.00";
    }

    private static byte[]? GenerarQr(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(4);
    }

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

        using var recurso = typeof(DtePdfService).Assembly
            .GetManifestResourceStream("SRAUMOAR.logoUmoar.jpg");
        if (recurso is null) return null;
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static byte[] GenerarEventoRetorno(JObject root, JObject identificacion, string? sello)
    {
        var emisor = root["emisor"] as JObject ?? new JObject();
        var documento = root["documento"] as JObject ?? new JObject();
        var resumen = root["resumen"] as JObject ?? new JObject();
        var codigo = Valor(identificacion, "codigoGeneracion");
        var numControl = Valor(identificacion, "numeroControl", "numeroControlDte");
        var fechaHora = FechaHora(identificacion);
        var ambiente = Ambiente(Valor(identificacion, "ambiente"));
        var urlConsulta = UrlConsulta(identificacion, identificacion.Value<string>("ambiente"));
        var qrBytes = GenerarQr(urlConsulta);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(8).FontColor(TextoOscuro));

            page.Header().Column(header =>
            {
                header.Item().Height(4).Background(VerdeInstitucional);
                header.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem(1.3f).Row(left =>
                    {
                        var logo = ObtenerLogo();
                        if (logo != null)
                        {
                            left.ConstantItem(48).Height(56).Image(logo).FitArea();
                            left.ConstantItem(10);
                        }
                        left.RelativeItem().Column(col =>
                        {
                            col.Item().Text(Valor(emisor, "nombre", "razonSocial", "nombreComercial"))
                                .Bold().FontSize(10.5f).FontColor(VerdeInstitucional);
                            col.Item().PaddingTop(1).Text("EDUCACIÓN SUPERIOR UNIVERSITARIA")
                                .Bold().FontSize(7).FontColor(VerdeMedio);
                            col.Item().PaddingTop(3).Text($"NIT: {Valor(emisor, "nit")}   ·   NRC: {Valor(emisor, "nrc")}")
                                .Bold().FontSize(7.5f).FontColor(TextoOscuro);
                            col.Item().PaddingTop(1).Text(Direccion(emisor)).FontSize(7).FontColor(TextoMuted);
                        });
                    });

                    row.ConstantItem(225).Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(right =>
                    {
                        right.Item().Row(r =>
                        {
                            r.RelativeItem().Text("EVENTO DE RETORNO")
                                .Bold().FontSize(10.5f).FontColor(VerdeInstitucional);
                            r.ConstantItem(46).Background(VerdeClaro).PaddingVertical(2).PaddingHorizontal(3)
                                .AlignCenter().Text("DTE 18").Bold().FontSize(6.5f).FontColor(VerdeInstitucional);
                        });

                        right.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("N° Control: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(numControl).Bold().FontSize(7.2f).FontColor(TextoOscuro);
                        });
                        right.Item().PaddingTop(1.5f).Text(t =>
                        {
                            t.Span("Cód. Gen: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(codigo).FontSize(6.5f).FontColor(TextoOscuro);
                        });
                        right.Item().PaddingTop(1.5f).Text(t =>
                        {
                            t.Span("Sello Recep: ").FontSize(6.8f).FontColor(TextoMuted);
                            t.Span(string.IsNullOrWhiteSpace(sello) ? "Pendiente" : sello)
                                .FontSize(6.3f).FontColor(VerdeMedio).Bold();
                        });
                        right.Item().PaddingTop(1.5f).Row(meta =>
                        {
                            meta.RelativeItem().Text(t =>
                            {
                                t.Span("Fecha: ").FontSize(6.8f).FontColor(TextoMuted);
                                t.Span(fechaHora).FontSize(6.8f).FontColor(TextoOscuro);
                            });
                            meta.ConstantItem(68).AlignRight().Text(ambiente)
                                .FontSize(6.8f).FontColor(VerdeMedio).Bold();
                        });
                    });
                });

                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(BordeSuave);
            });

            page.Content().PaddingTop(10).Column(column =>
            {
                column.Spacing(9);
                AgregarSeccionReceptor(column, documento, "18");

                var relacionados = root["documentoRelacionado"] as JArray;
                if (relacionados is not null && relacionados.Count > 0)
                {
                    column.Item().Border(1).BorderColor(BordeSuave).Background(FondoGris).Padding(8).Column(c =>
                    {
                        c.Item().Text("DOCUMENTOS RELACIONADOS").Bold().FontSize(7.5f).FontColor(VerdeInstitucional);
                        foreach (var d in relacionados.OfType<JObject>())
                            c.Item().PaddingTop(2).Text($"Tipo: {Valor(d, "tipoDocumento")}   ·   Código: {Valor(d, "codigoGeneracion")}   ·   Fecha: {Valor(d, "fechaEmision")}").FontSize(7f);
                    });
                }

                AgregarDetalleSegunTipo(column, root["cuerpoDocumento"] as JArray, "18");
                AgregarResumenSegunTipo(column, resumen, "18");
                AgregarQrYValidacion(column, qrBytes, urlConsulta);
            });

            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(1).LineColor(BordeSuave);
                f.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("SRAUMOAR  ·  Evento de Retorno (ERET)").FontSize(6.8f).FontColor(TextoMuted);
                    row.ConstantItem(80).AlignRight().Text(text =>
                    {
                        text.Span("Página ").FontSize(6.8f).FontColor(TextoMuted);
                        text.CurrentPageNumber().FontSize(6.8f).FontColor(TextoMuted);
                        text.Span(" de ").FontSize(6.8f).FontColor(TextoMuted);
                        text.TotalPages().FontSize(6.8f).FontColor(TextoMuted);
                    });
                });
            });
        })).GeneratePdf();
    }
}
