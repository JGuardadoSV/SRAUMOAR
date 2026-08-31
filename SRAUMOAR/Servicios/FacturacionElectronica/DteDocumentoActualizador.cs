using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

/// <summary>
/// Adaptador puro de borradores NUEVOS de las páginas de facturación. No usar para
/// migrar documentos persistidos, firmados, rechazados o pendientes de reintento.
/// La validación final pertenece a DteJsonFactory y sus esquemas incrustados.
/// </summary>
public static class DteDocumentoActualizador
{
    public static int ObtenerVersion(string tipoDte) => tipoDte switch
    {
        "01" or "14" or "15" => 2,
        "03" => 4,
        _ => throw new InvalidOperationException("Solo se actualizan borradores ordinarios 01, 03, 14 y 15.")
    };

    public static string ActualizarBorrador(string borrador, string ambienteFiscal, IDteJsonFactory validador)
    {
        ArgumentNullException.ThrowIfNull(validador);
        var documento = JObject.Parse(borrador);
        if (documento.Properties().Any(p => p.Name is "firma" or "firmaElectronica" or "selloRecibido" or "selloRecepcion"))
            throw new InvalidOperationException("No se puede actualizar un documento firmado o transmitido.");

        var identificacion = Objeto(documento, "identificacion");
        var tipo = identificacion.Value<string>("tipoDte") ?? string.Empty;
        var version = ObtenerVersion(tipo);
        var anterior = tipo == "03" ? 3 : 1;
        if (identificacion.Value<int?>("version") is not int actual || (actual != anterior && actual != version))
            throw new InvalidOperationException("Versión de borrador no soportada.");
        if (identificacion.Value<int?>("tipoOperacion") != 1 || identificacion.Value<int?>("tipoModelo") != 1)
            throw new InvalidOperationException("Este adaptador solo admite borradores de transmisión ordinaria.");

        identificacion["version"] = version;
        if (tipo == "14") Renombrar(documento, "sujetoExcluido", "receptor");
        if (tipo == "15")
        {
            Renombrar(documento, "donatario", "emisor");
            Renombrar(documento, "donante", "receptor");
            foreach (var item in (JArray?)documento["cuerpoDocumento"] ?? throw new InvalidOperationException("Falta cuerpoDocumento."))
                Renombrar((JObject)item, "depreciacion", "tipoDepreciacion");
        }

        var emisor = Objeto(documento, "emisor");
        emisor.Remove("tipoEstablecimiento");
        emisor.Remove("codEstableMH");
        emisor.Remove("codPuntoVentaMH");
        ExigirDireccion(emisor);
        NormalizarOpcionales(emisor, "nombreComercial", "nrc", "codEstable", "codPuntoVenta");

        // Las páginas requieren dirección explícita, aun donde el esquema permite null.
        var receptor = Objeto(documento, "receptor");
        ExigirDireccion(receptor);
        NormalizarOpcionales(receptor, "nrc", "nombreComercial", "codActividad", "descActividad", "telefono", "correo");
        var resumen = Objeto(documento, "resumen");
        if (tipo is "01" or "03")
        {
            Renombrar(resumen, "ivaRete1", "ivaRete");
            if (tipo == "03") Renombrar(resumen, "ivaPerci1", "ivaPerci");
            QuitarImporteCero(resumen, "reteRenta");
            var observaciones = (documento["extension"] as JObject)?["observaciones"];
            if (!resumen.ContainsKey("observaciones")) resumen["observaciones"] = observaciones?.DeepClone();
            documento.Remove("extension");
        }
        if (tipo == "14") QuitarImporteCero(resumen, "ivaRete1");

        var json = documento.ToString(Formatting.None);
        validador.ValidarDocumento(json, ambienteFiscal, tipo);
        return json;
    }

    /// <summary>
    /// DistritoId identifica una fila CAT-008 importada, NO la entidad Distrito legacy.
    /// Los tres valores se seleccionan explícitamente; no se infieren de códigos legacy.
    /// </summary>
    public static string ResolverDistritoReceptor(string? departamento, string? municipio, long? distritoId,
        IEnumerable<CatalogoFeItem> departamentos, IEnumerable<CatalogoFeItem> municipios,
        IEnumerable<CatalogoFeItem> distritos)
    {
        var dep = departamentos.Where(x => x.Activo && x.CodigoCatalogo == "CAT-012" && x.Codigo == departamento).ToArray();
        var mun = municipios.Where(x => x.Activo && x.CodigoCatalogo == "CAT-013" && x.Codigo == municipio).ToArray();
        var dis = distritos.Where(x => x.Activo && x.CodigoCatalogo == "CAT-008" && x.Id == distritoId).ToArray();
        if (string.IsNullOrWhiteSpace(departamento) || string.IsNullOrWhiteSpace(municipio) || distritoId is null or <= 0
            || dep.Length != 1 || mun.Length != 1 || dis.Length != 1 || string.IsNullOrWhiteSpace(dis[0].Codigo)
            || dep[0].Version != mun[0].Version || dep[0].Version != dis[0].Version)
            throw new InvalidOperationException("Seleccione departamento, municipio y distrito del receptor en los catálogos fiscales importados. No hay una geografía fiscal fiable para emitir.");
        return dis[0].Codigo;
    }

    private static JObject Objeto(JObject padre, string nombre) => padre[nombre] as JObject
        ?? throw new InvalidOperationException($"Falta la sección fiscal {nombre}.");

    private static void ExigirDireccion(JObject persona)
    {
        var direccion = Objeto(persona, "direccion");
        foreach (var campo in new[] { "departamento", "municipio", "distrito", "complemento" })
            if (string.IsNullOrWhiteSpace(direccion.Value<string>(campo)))
                throw new InvalidOperationException($"Falta dirección fiscal fiable: {campo}.");
    }

    private static void NormalizarOpcionales(JObject objeto, params string[] campos)
    {
        foreach (var campo in campos)
            if (objeto[campo]?.Type == JTokenType.String && string.IsNullOrWhiteSpace(objeto.Value<string>(campo)))
                objeto[campo] = null;
    }

    private static void Renombrar(JObject objeto, string anterior, string nuevo)
    {
        if (!objeto.TryGetValue(anterior, out var valor)) return;
        if (objeto.TryGetValue(nuevo, out var existente) && !JToken.DeepEquals(valor, existente))
            throw new InvalidOperationException($"El borrador contiene valores contradictorios para {nuevo}.");
        objeto[nuevo] = valor.DeepClone();
        objeto.Remove(anterior);
    }

    private static void QuitarImporteCero(JObject resumen, string nombre)
    {
        if (!resumen.TryGetValue(nombre, out var valor)) return;
        if (valor.Type is not (JTokenType.Integer or JTokenType.Float) || valor.Value<decimal>() != 0m)
            throw new InvalidOperationException($"No se puede eliminar {nombre} con importe del borrador.");
        resumen.Remove(nombre);
    }
}
