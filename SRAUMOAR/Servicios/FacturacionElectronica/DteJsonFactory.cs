using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteJsonFactory
{
    string ObtenerTipoCanonico(string tipoEntrada);
    void ValidarDocumento(string json, string ambienteFiscal, string? tipoDte = null);
    string CrearNota(string jsonDte, string tipoNota, string numeroControl, string codigoGeneracion, decimal monto, string? motivo, ConfiguracionFacturacionElectronica? configuracion = null);
    string CrearContingencia(string jsonDte, int tipoContingencia, string motivo, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null);
    string CrearInvalidacion(string jsonDte, string selloRecepcion, string motivoAnulacion, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null);
}

public sealed class DteJsonFactory : IDteJsonFactory
{
    private static readonly Lazy<JsonSchema> EsquemaEret = new(CargarEsquemaEret, LazyThreadSafetyMode.ExecutionAndPublication);

    public string ObtenerTipoCanonico(string tipoEntrada)
        => (tipoEntrada ?? string.Empty).Trim() switch
        {
            "01" => DteTipos.Factura,
            "02" or "03" => DteTipos.CreditoFiscal,
            "05" => DteTipos.NotaCredito,
            "06" => DteTipos.NotaDebito,
            "11" => "11",
            "14" => DteTipos.SujetoExcluido,
            "15" => DteTipos.Donacion,
            "18" => DteTipos.EventoRetorno,
            _ => throw new ArgumentException($"Tipo DTE no soportado: {tipoEntrada}", nameof(tipoEntrada))
        };

    public void ValidarDocumento(string json, string ambienteFiscal, string? tipoDte = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("El JSON DTE no puede estar vacío.", nameof(json));

        var root = JObject.Parse(json);
        var identificacion = root["identificacion"] as JObject
            ?? throw new InvalidOperationException("El DTE no contiene la sección identificacion.");
        var ambiente = identificacion.Value<string>("ambiente");
        if (!string.Equals(ambiente, ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal), StringComparison.Ordinal))
            throw new InvalidOperationException("El ambiente del JSON no coincide con el ambiente fiscal activo.");

        var tipo = identificacion.Value<string>("tipoDte") ?? identificacion.Value<string>("tipoEvento");
        if (string.IsNullOrWhiteSpace(tipo)) throw new InvalidOperationException("El documento no contiene tipoDte/tipoEvento.");
        if (tipoDte is not null && ObtenerTipoCanonico(tipoDte) != ObtenerTipoCanonico(tipo))
            throw new InvalidOperationException("El tipo del JSON no coincide con el tipo solicitado.");
        if (string.IsNullOrWhiteSpace(identificacion.Value<string>("codigoGeneracion")))
            throw new InvalidOperationException("El DTE no contiene codigoGeneracion.");
        if (root["emisor"] is null && root["donatario"] is null)
            throw new InvalidOperationException("El DTE no contiene emisor/donatario.");
        DteSchemaValidator.Validar(json, tipo, identificacion.Value<int>("version"));
        if (tipo == DteTipos.EventoRetorno)
        {
            ValidarEretContraEsquema(json);
            if (identificacion.Value<int?>("version") != 1 || root["documentoRelacionado"] is not JArray relacionados || relacionados.Count == 0)
                throw new InvalidOperationException("El ERET no cumple la identificación o relación documental requerida.");
            if (root["cuerpoDocumento"] is not JArray cuerpo || cuerpo.Count == 0 || root["resumen"] is null)
                throw new InvalidOperationException("El ERET no contiene documento, detalle o resumen.");
            if (cuerpo.Count > 2000 || relacionados.Count > 50)
                throw new InvalidOperationException("El ERET excede el máximo de líneas o documentos relacionados.");
            ExigirPropiedades(root, "identificacion", "documentoRelacionado", "emisor", "documento", "ventaTercero", "compraTercero", "cuerpoDocumento", "resumen", "apendice");
            ExigirPropiedades(identificacion, "version", "ambiente", "tipoModelo", "tipoOperacion", "tipoEvento", "tipoContingencia", "motivoContin", "codigoGeneracion", "fecEmi", "horEmi", "fusion", "tipoMoneda");
            ExigirPropiedades((JObject)root["emisor"]!, "nit", "nombre", "codEstableMH", "codEstable", "codPuntoVentaMH", "codPuntoVenta", "recintoFiscal", "tipoRegimen", "regimen", "tipoItemExpor");
            if (root["documento"] is JObject documento)
                ExigirPropiedades(documento, "tipoDocumento", "numDocumento", "nombre", "codPais", "nombrePais", "telefono", "correo");
            foreach (var relacionado in relacionados.OfType<JObject>())
                ExigirPropiedades(relacionado, "tipoDocumento", "codigoGeneracion", "fechaEmision");
            foreach (var item in cuerpo.OfType<JObject>())
                ExigirPropiedades(item, "numItem", "tipoItem", "codigoGeneracion", "cantidad", "precioUni", "descripcion", "codigo", "uniMedida", "montoDescu", "codTributo", "ventaNoSuj", "ventaExenta", "ventaGravada", "compra", "tributos", "psv", "ivaItem", "noGravado", "seguro", "flete", "ivaRete", "reteRenta");
            ExigirPropiedades((JObject)root["resumen"]!, "totalNoSuj", "totalExenta", "totalGravada", "totalCompraExcluidos", "subTotalVentas", "tributos", "totalSeguro", "totalFlete", "montoTotalOperacion", "ivaRete", "reteRenta", "totalNoGravado", "totalPagar", "totalLetras", "totalNoOnerosas", "totalIva", "saldoFavor");
        }
    }

    private static void ValidarEretContraEsquema(string json)
    {
        var instancia = JsonNode.Parse(json) ?? throw new InvalidOperationException("El JSON ERET está vacío.");
        var resultado = EsquemaEret.Value.Evaluate(instancia, new EvaluationOptions
        {
            EvaluateAs = SpecVersion.Draft7,
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        });
        if (!resultado.IsValid)
            throw new InvalidOperationException("El ERET no cumple el esquema oficial fe-eret-v1.json.");
    }

    private static JsonSchema CargarEsquemaEret()
    {
        const string recurso = "SRAUMOAR.Schemas.fe-eret-v1.json";
        using var stream = typeof(DteJsonFactory).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso fiscal {recurso}.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    public string CrearNota(string jsonDte, string tipoNota, string numeroControl, string codigoGeneracion, decimal monto, string? motivo, ConfiguracionFacturacionElectronica? configuracion = null)
    {
        tipoNota = tipoNota?.Trim() ?? string.Empty;
        if (tipoNota is not (DteTipos.NotaCredito or DteTipos.NotaDebito))
            throw new ArgumentException("La nota debe ser tipo 05 o 06.", nameof(tipoNota));
        if (string.IsNullOrWhiteSpace(numeroControl) || string.IsNullOrWhiteSpace(codigoGeneracion))
            throw new ArgumentException("La nota requiere número de control y código de generación.");
        if (monto <= 0m)
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto de la nota debe ser mayor que cero.");

        var root = JObject.Parse(jsonDte);
        var identificacionOriginal = (JObject?)root["identificacion"]
            ?? throw new InvalidOperationException("El DTE original no contiene identificación.");
        if (!string.Equals(ObtenerTipoCanonico(identificacionOriginal.Value<string>("tipoDte") ?? string.Empty), DteTipos.CreditoFiscal, StringComparison.Ordinal))
            throw new InvalidOperationException("Las notas de crédito y débito se relacionan actualmente con un DTE 03.");

        var emisorOriginal = (JObject?)root["emisor"]
            ?? throw new InvalidOperationException("El DTE original no contiene emisor.");
        var receptorOriginal = (JObject?)root["receptor"]
            ?? throw new InvalidOperationException("El DTE original no contiene receptor.");
        var cuerpoOriginal = (JArray?)root["cuerpoDocumento"]
            ?? throw new InvalidOperationException("El DTE original no contiene cuerpoDocumento.");
        var resumenOriginal = (JObject?)root["resumen"]
            ?? throw new InvalidOperationException("El DTE original no contiene resumen.");

        var codigoOriginal = Obligatorio(identificacionOriginal.Value<string>("codigoGeneracion"), "identificacion.codigoGeneracion");
        var fechaOriginal = identificacionOriginal.Value<string>("fecEmi") ?? ObtenerFechaElSalvador().ToString("yyyy-MM-dd");
        var totalOriginal = Numero(resumenOriginal, "montoTotalOperacion", "totalPagar")
            ?? throw new InvalidOperationException("El DTE original no contiene un total válido.");
        if (tipoNota == DteTipos.NotaCredito && monto > totalOriginal + 0.01m)
            throw new InvalidOperationException("La nota de crédito no puede superar el total del DTE original.");

        var factor = totalOriginal <= 0m ? 1m : monto / totalOriginal;
        var now = ObtenerFechaElSalvador();
        var identificacion = new JObject
        {
            ["version"] = 4,
            ["ambiente"] = Obligatorio(identificacionOriginal.Value<string>("ambiente"), "identificacion.ambiente"),
            ["tipoDte"] = tipoNota,
            ["numeroControl"] = numeroControl,
            ["codigoGeneracion"] = codigoGeneracion.ToUpperInvariant(),
            ["tipoModelo"] = 1,
            ["tipoOperacion"] = 1,
            ["tipoContingencia"] = null,
            ["motivoContin"] = null,
            ["fecEmi"] = now.ToString("yyyy-MM-dd"),
            ["horEmi"] = now.ToString("HH:mm:ss"),
            ["tipoMoneda"] = "USD",
            ["fusion"] = null
        };

        var dte = new JObject
        {
            ["identificacion"] = identificacion,
            ["documentoRelacionado"] = new JArray
            {
                new JObject
                {
                    ["tipoDocumento"] = "03",
                    ["tipoGeneracion"] = 2,
                    ["numeroDocumento"] = codigoOriginal,
                    ["fechaEmision"] = fechaOriginal
                }
            },
            ["emisor"] = ConstruirEmisor(emisorOriginal, configuracion),
            ["receptor"] = ConstruirReceptor(receptorOriginal),
            ["ventaTercero"] = null,
            ["cuerpoDocumento"] = ConstruirCuerpo(cuerpoOriginal, resumenOriginal, codigoOriginal, factor),
            ["resumen"] = ConstruirResumen(resumenOriginal, factor, monto, motivo),
            ["apendice"] = null
        };

        if (tipoNota == DteTipos.NotaDebito) dte["resumen"]!["numPagoElectronico"] = null;
        var json = dte.ToString(Formatting.None);
        ValidarDocumento(json, identificacion.Value<string>("ambiente")!, tipoNota);
        return json;
    }

    // Esquema vigente de FacturaME: contingencia v4.
    public string CrearContingencia(string jsonDte, int tipoContingencia, string motivo, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null)
    {
        if (tipoContingencia is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(tipoContingencia), "El tipo de contingencia debe estar entre 1 y 5.");

        var root = JObject.Parse(jsonDte);
        var identificacionOriginal = (JObject?)root["identificacion"]
            ?? throw new InvalidOperationException("El DTE no contiene identificación.");
        var emisorOriginal = (JObject?)(root["emisor"] ?? root["donatario"])
            ?? throw new InvalidOperationException("El DTE no contiene emisor/donatario.");
        var fechaActual = ObtenerFechaElSalvador();
        var fechaInicio = identificacionOriginal.Value<string>("fecEmi") ?? fechaActual.ToString("yyyy-MM-dd");
        var horaInicio = identificacionOriginal.Value<string>("horEmi") ?? fechaActual.ToString("HH:mm:ss");

        var evento = new JObject
        {
            ["identificacion"] = new JObject
            {
                ["version"] = 4,
                ["ambiente"] = identificacionOriginal.Value<string>("ambiente"),
                ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                ["fTransmision"] = fechaActual.ToString("yyyy-MM-dd"),
                ["hTransmision"] = fechaActual.ToString("HH:mm:ss")
            },
            ["emisor"] = new JObject
            {
                ["nit"] = Obligatorio(ValorConfigurado(configuracion?.NitEmisor, emisorOriginal, "nit", "numDocumento"), "emisor.nit"),
                ["nombre"] = Obligatorio(ValorConfigurado(configuracion?.NombreEmisor, emisorOriginal, "nombre", "nombreComercial"), "emisor.nombre"),
                ["nombreResponsable"] = Obligatorio(nombreResponsable, "nombreResponsable"),
                ["tipoDocResponsable"] = "37",
                ["numeroDocResponsable"] = Obligatorio(nitResponsable, "numeroDocResponsable"),
                ["tipoEstablecimiento"] = Obligatorio(ValorConfigurado(configuracion?.TipoEstablecimiento, emisorOriginal, "tipoEstablecimiento"), "emisor.tipoEstablecimiento"),
                ["codEstableMH"] = ValorConfigurado(configuracion?.CodigoEstablecimientoMh, emisorOriginal, "codEstableMH"),
                ["codPuntoVentaMH"] = ValorConfigurado(configuracion?.CodigoPuntoVentaMh, emisorOriginal, "codPuntoVentaMH"),
                ["telefono"] = Obligatorio(ValorConfigurado(configuracion?.TelefonoEmisor, emisorOriginal, "telefono"), "emisor.telefono"),
                ["correo"] = Obligatorio(ValorConfigurado(configuracion?.CorreoEmisor, emisorOriginal, "correo"), "emisor.correo")
            },
            ["detalleDTE"] = new JArray
            {
                new JObject
                {
                    ["noItem"] = 1,
                    ["codigoGeneracion"] = identificacionOriginal.Value<string>("codigoGeneracion"),
                    ["tipoDoc"] = identificacionOriginal.Value<string>("tipoDte")
                }
            },
            ["motivo"] = new JObject
            {
                ["fInicio"] = fechaInicio,
                ["fFin"] = fechaInicio,
                ["hInicio"] = horaInicio,
                ["hFin"] = horaInicio,
                ["tipoContingencia"] = tipoContingencia,
                ["motivoContingencia"] = motivo
            }
        };

        return evento.ToString(Formatting.None);
    }

    // Esquema vigente de FacturaME: invalidación v3.
    public string CrearInvalidacion(string jsonDte, string selloRecepcion, string motivoAnulacion, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null)
    {
        if (string.IsNullOrWhiteSpace(selloRecepcion))
            throw new ArgumentException("El sello de recepción es requerido.", nameof(selloRecepcion));

        var root = JObject.Parse(jsonDte);
        var original = (JObject?)root["identificacion"]
            ?? throw new InvalidOperationException("El DTE no contiene identificación.");
        var emisorOriginal = (JObject?)(root["emisor"] ?? root["donatario"])
            ?? throw new InvalidOperationException("El DTE no contiene emisor/donatario.");
        var receptor = root["receptor"] as JObject ?? root["sujetoExcluido"] as JObject
            ?? root["donante"] as JObject ?? root["documento"] as JObject ?? new JObject();
        var fecha = ObtenerFechaElSalvador();
        var tipoDte = ObtenerTipoCanonico(original.Value<string>("tipoDte") ?? original.Value<string>("tipoEvento") ?? "01");
        var numeroDocumento = Valor(receptor, "numDocumento", "nit");
        var tipoDocumento = Valor(receptor, "tipoDocumento");
        if (string.IsNullOrWhiteSpace(tipoDocumento) && !string.IsNullOrWhiteSpace(numeroDocumento))
            tipoDocumento = numeroDocumento.Length == 14 ? "36" : numeroDocumento.Length == 9 ? "13" : "37";
        var numeroSolicita = numeroDocumento ?? Obligatorio(nitResponsable, "numDocSolicita");
        var tipoSolicita = tipoDocumento ?? (numeroSolicita.Length == 14 ? "36" : numeroSolicita.Length == 9 ? "13" : "37");
        var numeroControl = Valor(original, "numeroControl");
        if (string.IsNullOrWhiteSpace(numeroControl)) numeroControl = null;

        var evento = new JObject
        {
            ["identificacion"] = new JObject
            {
                ["version"] = 3,
                ["ambiente"] = original.Value<string>("ambiente"),
                ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                ["fecEmi"] = fecha.ToString("yyyy-MM-dd"),
                ["horEmi"] = fecha.ToString("HH:mm:ss"),
                ["fusion"] = null
            },
            ["emisor"] = new JObject
            {
                ["nit"] = Obligatorio(ValorConfigurado(configuracion?.NitEmisor, emisorOriginal, "nit", "numDocumento"), "emisor.nit"),
                ["nombre"] = Obligatorio(ValorConfigurado(configuracion?.NombreEmisor, emisorOriginal, "nombre", "nombreComercial"), "emisor.nombre"),
                ["codEstableMH"] = Obligatorio(ValorConfigurado(configuracion?.CodigoEstablecimientoMh, emisorOriginal, "codEstableMH"), "emisor.codEstableMH"),
                ["codEstable"] = ValorConfigurado(configuracion?.CodigoEstablecimiento, emisorOriginal, "codEstable"),
                ["codPuntoVentaMH"] = Obligatorio(ValorConfigurado(configuracion?.CodigoPuntoVentaMh, emisorOriginal, "codPuntoVentaMH"), "emisor.codPuntoVentaMH"),
                ["codPuntoVenta"] = ValorConfigurado(configuracion?.CodigoPuntoVenta, emisorOriginal, "codPuntoVenta"),
                ["telefono"] = Obligatorio(ValorConfigurado(configuracion?.TelefonoEmisor, emisorOriginal, "telefono"), "emisor.telefono"),
                ["correo"] = Obligatorio(ValorConfigurado(configuracion?.CorreoEmisor, emisorOriginal, "correo"), "emisor.correo")
            },
            ["documento"] = new JObject
            {
                ["tipoDte"] = tipoDte,
                ["codigoGeneracion"] = original.Value<string>("codigoGeneracion"),
                ["selloRecibido"] = selloRecepcion,
                ["numeroControl"] = numeroControl,
                ["fecEmi"] = original.Value<string>("fecEmi") ?? fecha.ToString("yyyy-MM-dd"),
                ["codigoGeneracionR"] = null,
                ["tipoDocumento"] = tipoDocumento,
                ["numDocumento"] = numeroDocumento,
                ["nombre"] = Valor(receptor, "nombre"),
                ["telefono"] = Valor(receptor, "telefono"),
                ["correo"] = Valor(receptor, "correo")
            },
            ["motivo"] = new JObject
            {
                ["tipoAnulacion"] = 2,
                ["motivoAnulacion"] = string.IsNullOrWhiteSpace(motivoAnulacion) ? "Anulación solicitada por el cliente" : motivoAnulacion,
                ["nombreResponsable"] = Obligatorio(nombreResponsable, "nombreResponsable"),
                ["tipDocResponsable"] = "37",
                ["numDocResponsable"] = Obligatorio(nitResponsable, "numDocResponsable"),
                ["nombreSolicita"] = Valor(receptor, "nombre") ?? nombreResponsable,
                ["tipDocSolicita"] = tipoSolicita,
                ["numDocSolicita"] = numeroSolicita
            }
        };

        return evento.ToString(Formatting.None);
    }

    private static string? Valor(JObject? objeto, params string[] nombres)
    {
        if (objeto is null) return null;
        foreach (var nombre in nombres)
        {
            var valor = objeto[nombre];
            if (valor is not null && valor.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(valor.ToString()))
                return valor.ToString();
        }
        return null;
    }

    private static void ExigirPropiedades(JObject objeto, params string[] nombres)
    {
        foreach (var nombre in nombres)
            if (!objeto.ContainsKey(nombre)) throw new InvalidOperationException($"El ERET no contiene la propiedad requerida {nombre}.");
    }

    private static string? ValorConfigurado(string? configurado, JObject original, params string[] nombres)
        => !string.IsNullOrWhiteSpace(configurado) ? configurado : Valor(original, nombres);

    private static JObject ConstruirEmisor(JObject original, ConfiguracionFacturacionElectronica? configuracion = null)
    {
        var direccion = (JObject?)original["direccion"] ?? new JObject();
        return new JObject
        {
            ["nit"] = Obligatorio(ValorConfigurado(configuracion?.NitEmisor, original, "nit"), "emisor.nit"),
            ["nrc"] = ValorConfigurado(configuracion?.NrcEmisor, original, "nrc"),
            ["nombre"] = Obligatorio(ValorConfigurado(configuracion?.NombreEmisor, original, "nombre"), "emisor.nombre"),
            ["codActividad"] = Obligatorio(ValorConfigurado(configuracion?.CodigoActividad, original, "codActividad"), "emisor.codActividad"),
            ["descActividad"] = Obligatorio(ValorConfigurado(configuracion?.DescripcionActividad, original, "descActividad"), "emisor.descActividad"),
            ["nombreComercial"] = ValorConfigurado(configuracion?.NombreComercial, original, "nombreComercial"),
            ["direccion"] = new JObject
            {
                ["departamento"] = Obligatorio(ValorConfigurado(configuracion?.CodigoDepartamento, direccion, "departamento"), "emisor.direccion.departamento"),
                ["municipio"] = Obligatorio(ValorConfigurado(configuracion?.CodigoMunicipio, direccion, "municipio"), "emisor.direccion.municipio"),
                ["distrito"] = Obligatorio(ValorConfigurado(configuracion?.CodigoDistrito, direccion, "distrito"), "emisor.direccion.distrito"),
                ["complemento"] = Obligatorio(ValorConfigurado(configuracion?.ComplementoDireccion, direccion, "complemento"), "emisor.direccion.complemento")
            },
            ["telefono"] = Obligatorio(ValorConfigurado(configuracion?.TelefonoEmisor, original, "telefono"), "emisor.telefono"),
            ["correo"] = Obligatorio(ValorConfigurado(configuracion?.CorreoEmisor, original, "correo"), "emisor.correo")
        };
    }

    private static JObject ConstruirReceptor(JObject original)
    {
        var direccion = (JObject?)original["direccion"] ?? new JObject();
        var documento = Valor(original, "numDocumento", "nit");
        documento = Obligatorio(documento, "receptor.numDocumento");
        var tipoDocumento = Valor(original, "tipoDocumento")
            ?? (documento.Length == 14 ? "36" : documento.Length == 9 ? "13" : "37");
        return new JObject
        {
            ["tipoDocumento"] = tipoDocumento,
            ["numDocumento"] = documento,
            ["nrc"] = Valor(original, "nrc"),
            ["nombre"] = Obligatorio(Valor(original, "nombre"), "receptor.nombre"),
            ["codActividad"] = Obligatorio(Valor(original, "codActividad"), "receptor.codActividad"),
            ["descActividad"] = Obligatorio(Valor(original, "descActividad"), "receptor.descActividad"),
            ["nombreComercial"] = Valor(original, "nombreComercial"),
            ["direccion"] = new JObject
            {
                ["departamento"] = Obligatorio(Valor(direccion, "departamento"), "receptor.direccion.departamento"),
                ["municipio"] = Obligatorio(Valor(direccion, "municipio"), "receptor.direccion.municipio"),
                ["distrito"] = Obligatorio(Valor(direccion, "distrito"), "receptor.direccion.distrito"),
                ["complemento"] = Obligatorio(Valor(direccion, "complemento"), "receptor.direccion.complemento")
            },
            ["telefono"] = Valor(original, "telefono"),
            ["correo"] = Valor(original, "correo")
        };
    }

    private static JArray ConstruirCuerpo(JArray original, JObject resumenOriginal, string codigoOriginal, decimal factor)
    {
        var cuerpo = new JArray();
        var baseGravada = original.OfType<JObject>().Sum(x => Numero(x, "ventaGravada") ?? 0m);
        var ivaOriginal = Numero(resumenOriginal, "totalIva") ?? SumarTributos(resumenOriginal["tributos"] as JArray ?? new JArray());
        foreach (var token in original.OfType<JObject>())
        {
            var pesoGravado = baseGravada > 0m ? (Numero(token, "ventaGravada") ?? 0m) / baseGravada : 0m;
            var linea = new JObject
            {
                ["numItem"] = token.Value<int?>("numItem") ?? cuerpo.Count + 1,
                ["tipoItem"] = token.Value<int?>("tipoItem") ?? 1,
                ["numeroDocumento"] = codigoOriginal,
                ["cantidad"] = Escalar(Numero(token, "cantidad") ?? 1m, factor, 8),
                ["codigo"] = Valor(token, "codigo"),
                ["codTributo"] = Valor(token, "codTributo"),
                ["uniMedida"] = token.Value<int?>("uniMedida") ?? 59,
                ["descripcion"] = Obligatorio(Valor(token, "descripcion"), "cuerpoDocumento.descripcion"),
                ["precioUni"] = Numero(token, "precioUni") ?? 0m,
                ["montoDescu"] = Escalar(Numero(token, "montoDescu") ?? 0m, factor, 8),
                ["ventaNoSuj"] = Escalar(Numero(token, "ventaNoSuj") ?? 0m, factor, 8),
                ["ventaExenta"] = Escalar(Numero(token, "ventaExenta") ?? 0m, factor, 8),
                ["ventaGravada"] = Escalar(Numero(token, "ventaGravada") ?? 0m, factor, 8),
                ["tributos"] = token["tributos"]?.DeepClone(),
                ["noGravado"] = Escalar(Numero(token, "noGravado") ?? 0m, factor, 8),
                ["ivaPerci"] = Escalar(Numero(token, "ivaPerci") ?? (Numero(resumenOriginal, "ivaPerci", "ivaPerci1") ?? 0m) * pesoGravado, factor, 8),
                ["totalIva"] = Escalar(Numero(token, "totalIva") ?? ivaOriginal * pesoGravado, factor, 8),
                ["ivaRete"] = Escalar(Numero(token, "ivaRete") ?? (Numero(resumenOriginal, "ivaRete", "ivaRete1") ?? 0m) * pesoGravado, factor, 8)
            };
            if (linea["tributos"] is null && (Numero(linea, "ventaGravada") ?? 0m) > 0m)
                linea["tributos"] = new JArray("20");
            cuerpo.Add(linea);
        }
        if (cuerpo.Count == 0) throw new InvalidOperationException("El DTE original no contiene líneas.");
        return cuerpo;
    }

    private static JObject ConstruirResumen(JObject original, decimal factor, decimal monto, string? motivo)
    {
        var tributos = new JArray();
        if (original["tributos"] is JArray originalTributos)
        {
            foreach (var token in originalTributos.OfType<JObject>())
            {
                var tributo = (JObject)token.DeepClone();
                if (Numero(tributo, "valor") is decimal valor)
                    tributo["valor"] = Escalar(valor, factor, 2);
                tributos.Add(tributo);
            }
        }
        if (tributos.Count == 0 && (Numero(original, "totalIva") ?? 0m) > 0m)
            tributos.Add(new JObject { ["codigo"] = "20", ["descripcion"] = "Impuesto al Valor Agregado 13%", ["valor"] = Escalar(Numero(original, "totalIva") ?? 0m, factor, 2) });

        var resumen = new JObject
        {
            ["totalNoSuj"] = Escalar(Numero(original, "totalNoSuj") ?? 0m, factor, 2),
            ["totalExenta"] = Escalar(Numero(original, "totalExenta") ?? 0m, factor, 2),
            ["totalGravada"] = Escalar(Numero(original, "totalGravada") ?? 0m, factor, 2),
            ["subTotalVentas"] = Escalar(Numero(original, "subTotalVentas") ?? 0m, factor, 2),
            ["totalDescu"] = Escalar(Numero(original, "totalDescu") ?? 0m, factor, 2),
            ["tributos"] = tributos.Count == 0 ? null : tributos,
            ["montoTotalOperacion"] = Math.Round(monto, 2, MidpointRounding.AwayFromZero),
            ["ivaPerci"] = Escalar(Numero(original, "ivaPerci", "ivaPerci1") ?? 0m, factor, 2),
            ["totalIva"] = Numero(original, "totalIva") is decimal iva ? Escalar(iva, factor, 2) : SumarTributos(tributos),
            ["ivaRete"] = Escalar(Numero(original, "ivaRete", "ivaRete1") ?? 0m, factor, 2),
            ["totalNoGravado"] = Escalar(Numero(original, "totalNoGravado") ?? 0m, factor, 2),
            ["totalPagar"] = Escalar(Numero(original, "totalPagar") ?? monto / factor, factor, 2),
            ["totalLetras"] = new Conversor().ConvertirNumeroALetras(Escalar(Numero(original, "totalPagar") ?? monto / factor, factor, 2)),
            ["condicionOperacion"] = original.Value<int?>("condicionOperacion") ?? 1,
            ["observaciones"] = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            ["codigoRetencionMH"] = Valor(original, "codigoRetencionMH")
        };
        return resumen;
    }

    private static decimal SumarTributos(JArray tributos)
        => tributos.OfType<JObject>().Where(x => x.Value<string>("codigo") == "20").Sum(x => Numero(x, "valor") ?? 0m);

    private static decimal? Numero(JObject objeto, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            var token = objeto[nombre];
            if (token is null || token.Type is JTokenType.Null) continue;
            if (token.Type is JTokenType.Integer or JTokenType.Float) return token.Value<decimal>();
            if (decimal.TryParse(token.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                return valor;
        }
        return null;
    }

    private static decimal Escalar(decimal valor, decimal factor, int decimales)
        => Math.Round(valor * factor, decimales, MidpointRounding.AwayFromZero);

    private static string Obligatorio(string? valor, string nombre)
        => string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException($"Falta el dato fiscal requerido: {nombre}.")
            : valor.Trim();

    private static DateTime ObtenerFechaElSalvador()
    {
        try
        {
            var zona = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zona);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.UtcNow.AddHours(-6);
        }
    }
}
