using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Servicios
{
    public interface IAlumnoService
    {
        Task<IList<Alumno>> ObtenerAlumnosAsync();
        Task<int> ObtenerTotalAlumnosAsync();
        Task<IList<Alumno>> ObtenerAlumnosPaginadosAsync(int pageNumber, int pageSize);
    }

    public class AlumnoService : IAlumnoService
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public AlumnoService(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public async Task<IList<Alumno>> ObtenerAlumnosAsync()
        {
            return await _context.Alumno.Include(x => x.Usuario).ToListAsync();
        }

        public async Task<int> ObtenerTotalAlumnosAsync()
        {
            return await _context.Alumno.CountAsync();
        }

        public async Task<IList<Alumno>> ObtenerAlumnosPaginadosAsync(int pageNumber, int pageSize)
        {
            return await _context.Alumno
                .Include(x => x.Usuario)
                .OrderByDescending(x => x.FechaDeRegistro)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
