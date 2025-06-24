using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
namespace SRAUMOAR.Servicios
{
    public interface ICorrelativoService
    {
        Task<long> ObtenerSiguienteCorrelativo(string tipoDocumento, string ambiente);
    }

    public class CorrelativoService : ICorrelativoService
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CorrelativoService(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public async Task<long> ObtenerSiguienteCorrelativo(string tipoDocumento, string ambiente)
        {
            var correlativo = await _context.DteCorrelativos
                .FirstOrDefaultAsync(x => x.TipoDocumento == tipoDocumento && x.Ambiente == ambiente);

            if (correlativo == null)
            {
                // Si no existe, lo creamos con el primer correlativo
                correlativo = new DteCorrelativo
                {
                    TipoDocumento = tipoDocumento,
                    Ambiente = ambiente,
                    Correlativo = 1,
                    UltimaActualizacion = DateTime.Now
                };

                _context.DteCorrelativos.Add(correlativo);
            }
            else
            {
                // Si existe, incrementamos el correlativo
                correlativo.Correlativo++;
                correlativo.UltimaActualizacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return correlativo.Correlativo;
        }
    }
}
