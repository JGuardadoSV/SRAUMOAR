using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Servicios
{
    public interface IAlumnoService
    {
        Task<IList<Alumno>> ObtenerAlumnosAsync();
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
    }

}
