using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface ICatalogoFeService
{
    Task<int> ImportarAsync(Stream archivo, string version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogoFeItem>> ObtenerAsync(string? codigoCatalogo = null, string? version = null, CancellationToken cancellationToken = default);
}

public sealed class CatalogoFeService : ICatalogoFeService
{
    private readonly Contexto _contexto;

    public CatalogoFeService(Contexto contexto) => _contexto = contexto;

    public async Task<int> ImportarAsync(Stream archivo, string version, CancellationToken cancellationToken = default)
    {
        if (archivo is null || !archivo.CanRead)
            throw new ArgumentException("El archivo de catálogos no se puede leer.", nameof(archivo));

        version = string.IsNullOrWhiteSpace(version) ? "1.1" : version.Trim();
        var items = new List<CatalogoFeItem>();

        using var copia = new MemoryStream();
        await archivo.CopyToAsync(copia, cancellationToken);
        copia.Position = 0;

        using var workbook = new XLWorkbook(copia);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("El archivo no contiene hojas.");
        var rango = worksheet.RangeUsed()
            ?? throw new InvalidOperationException("La hoja de catálogos está vacía.");

        string? catalogoActual = null;
        var orden = 0;
        foreach (var fila in rango.RowsUsed())
        {
            var codigo = fila.Cell(1).GetString().Trim();
            var descripcion = fila.Cell(2).GetString().Trim();
            var auxiliar = fila.Cell(3).GetString().Trim();

            if (codigo.StartsWith("CAT-", StringComparison.OrdinalIgnoreCase))
            {
                var separador = codigo.IndexOf(' ');
                catalogoActual = (separador > 0 ? codigo[..separador] : codigo).ToUpperInvariant();
                orden = 0;
                continue;
            }

            if (catalogoActual is null || string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(descripcion))
                continue;
            if (codigo.Equals("Código", StringComparison.OrdinalIgnoreCase) || codigo.Equals("CÓDIGO", StringComparison.OrdinalIgnoreCase))
                continue;
            if (codigo.Equals("Código", StringComparison.OrdinalIgnoreCase) || codigo.Equals("Codigo", StringComparison.OrdinalIgnoreCase))
                continue;

            if (catalogoActual is "CAT-008" or "CAT-012" or "CAT-013")
                codigo = TerritorioFiscalService.Normalizar(codigo);

            items.Add(new CatalogoFeItem
            {
                CodigoCatalogo = catalogoActual,
                Version = version,
                Codigo = codigo,
                Descripcion = descripcion,
                AmbitoDepartamento = string.Empty,
                AmbitoMunicipio = string.Empty,
                ValorAuxiliar = string.IsNullOrWhiteSpace(auxiliar) ? null : auxiliar,
                Activo = true,
                Orden = orden++
            });
        }

        TerritorioFiscalService.Enriquecer(items);
        var unicos = items
            .GroupBy(x => $"{x.CodigoCatalogo}|{x.Version}|{x.Codigo}|{x.AmbitoDepartamento}|{x.AmbitoMunicipio}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        if (unicos.Count == 0)
            throw new InvalidOperationException("No se encontraron filas de catálogos válidas.");

        await using var transaction = await _contexto.Database.BeginTransactionAsync(cancellationToken);
        var catalogos = unicos.Select(x => x.CodigoCatalogo).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await _contexto.CatalogosFe
            .Where(x => x.Version == version && catalogos.Contains(x.CodigoCatalogo))
            .ExecuteDeleteAsync(cancellationToken);
        _contexto.CatalogosFe.AddRange(unicos);
        await _contexto.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return unicos.Count;
    }

    public async Task<IReadOnlyList<CatalogoFeItem>> ObtenerAsync(string? codigoCatalogo = null, string? version = null, CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.CatalogosFe.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(codigoCatalogo)) consulta = consulta.Where(x => x.CodigoCatalogo == codigoCatalogo.Trim());
        if (!string.IsNullOrWhiteSpace(version)) consulta = consulta.Where(x => x.Version == version.Trim());
        return await consulta.OrderBy(x => x.CodigoCatalogo).ThenBy(x => x.Orden).ToListAsync(cancellationToken);
    }
}
