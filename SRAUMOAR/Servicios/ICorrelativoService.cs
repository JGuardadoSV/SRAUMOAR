using Microsoft.EntityFrameworkCore;
using System.Data;
using SRAUMOAR.Entidades.Procesos;

namespace SRAUMOAR.Servicios;

public interface ICorrelativoService
{
    Task<long> ObtenerSiguienteCorrelativo(string tipoDocumento, string ambiente, string establecimiento = "M001", string puntoVenta = "P001");
}

public class CorrelativoService : ICorrelativoService
{
    private readonly SRAUMOAR.Modelos.Contexto _context;

    public CorrelativoService(SRAUMOAR.Modelos.Contexto context) => _context = context;

    public async Task<long> ObtenerSiguienteCorrelativo(string tipoDocumento, string ambiente, string establecimiento = "M001", string puntoVenta = "P001")
    {
        for (var intento = 1; intento <= 3; intento++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var correlativo = await _context.DteCorrelativos
                    .SingleOrDefaultAsync(x => x.TipoDocumento == tipoDocumento && x.Ambiente == ambiente && x.Establecimiento == establecimiento && x.PuntoVenta == puntoVenta);

                if (correlativo is null)
                {
                    correlativo = new DteCorrelativo
                    {
                        TipoDocumento = tipoDocumento,
                        Ambiente = ambiente,
                        Establecimiento = establecimiento,
                        PuntoVenta = puntoVenta,
                        Correlativo = 1,
                        UltimaActualizacion = DateTime.UtcNow
                    };
                    _context.DteCorrelativos.Add(correlativo);
                }
                else
                {
                    correlativo.Correlativo++;
                    correlativo.UltimaActualizacion = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return correlativo.Correlativo;
            }
            catch (DbUpdateException) when (intento < 3)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                await Task.Delay(25 * intento);
            }
        }

        throw new InvalidOperationException("No se pudo reservar un correlativo DTE después de varios intentos.");
    }
}
