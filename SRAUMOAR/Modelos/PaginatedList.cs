using Microsoft.EntityFrameworkCore;

namespace SRAUMOAR.Modelos
{
    public class PaginatedList<T> : List<T>
    {
        public int IndicePagina { get; private set; }
        public int TotalPaginas { get; private set; }
        public int TotalRegistros { get; private set; }
        public bool TienePaginaAnterior => IndicePagina > 1;
        public bool TienePaginaSiguiente => IndicePagina < TotalPaginas;

        public PaginatedList(List<T> items, int totalRegistros, int indicePagina, int tamanoPagina)
        {
            TotalRegistros = totalRegistros;
            IndicePagina = indicePagina;
            TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanoPagina);

            AddRange(items);
        }

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int indicePagina, int tamanoPagina)
        {
            var totalRegistros = await source.CountAsync();
            var items = await source.Skip((indicePagina - 1) * tamanoPagina).Take(tamanoPagina).ToListAsync();
            return new PaginatedList<T>(items, totalRegistros, indicePagina, tamanoPagina);
        }
    }
}


