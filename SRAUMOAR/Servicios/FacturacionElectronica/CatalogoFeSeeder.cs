using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

/// <summary>
/// Mantiene en la base de datos los catálogos territoriales incorporados al sistema.
/// Son la misma fuente oficial utilizada por los formularios de alumnos.
/// </summary>
public sealed class CatalogoFeSeeder
{
    private const string Version = "1.1";
    private readonly Contexto _contexto;

    public CatalogoFeSeeder(Contexto contexto) => _contexto = contexto;

    public async Task<int> SembrarTerritorioAsync(CancellationToken cancellationToken = default)
    {
        var entradas = CatalogoTerritorialFiscal.Entradas
            .Where(x => x.Catalogo is "008" or "012" or "013")
            .Select((x, indice) => new CatalogoFeItem
            {
                CodigoCatalogo = "CAT-" + x.Catalogo,
                Version = Version,
                Codigo = x.Codigo,
                Descripcion = x.Valor,
                AmbitoDepartamento = x.Catalogo is "012" ? string.Empty : x.Ambito,
                AmbitoMunicipio = x.Catalogo == "013" ? x.Codigo : x.MunicipioCodigo ?? string.Empty,
                Activo = true,
                Orden = indice
            })
            .ToList();

        if (entradas.Count == 0)
            throw new InvalidOperationException("El catálogo territorial incorporado no contiene registros.");

        var existentes = await _contexto.CatalogosFe
            .Where(x => x.Version == Version
                && (x.CodigoCatalogo == "CAT-008" || x.CodigoCatalogo == "CAT-012" || x.CodigoCatalogo == "CAT-013"))
            .ToListAsync(cancellationToken);

        var cambios = 0;
        foreach (var entrada in entradas)
        {
            var existente = existentes.SingleOrDefault(x => x.CodigoCatalogo == entrada.CodigoCatalogo
                && x.Codigo == entrada.Codigo
                && x.AmbitoDepartamento == entrada.AmbitoDepartamento
                && x.AmbitoMunicipio == entrada.AmbitoMunicipio);
            if (existente is null)
            {
                _contexto.CatalogosFe.Add(entrada);
                cambios++;
                continue;
            }

            existente.Descripcion = entrada.Descripcion;
            existente.AmbitoDepartamento = entrada.AmbitoDepartamento;
            existente.AmbitoMunicipio = entrada.AmbitoMunicipio;
            existente.Activo = true;
            existente.Orden = entrada.Orden;
        }

        foreach (var obsoleto in existentes.Where(x => !entradas.Any(e => e.CodigoCatalogo == x.CodigoCatalogo
            && e.Codigo == x.Codigo
            && e.AmbitoDepartamento == x.AmbitoDepartamento
            && e.AmbitoMunicipio == x.AmbitoMunicipio)))
        {
            obsoleto.Activo = false;
            cambios++;
        }

        if (cambios > 0)
                await _contexto.SaveChangesAsync(cancellationToken);

        return entradas.Count;
    }
}
