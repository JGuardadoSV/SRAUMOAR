using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using System.Security.Claims;

namespace SRAUMOAR.Pages.portal.estudiante
{

    //Allow only Estudiantes rol
    [Authorize(Roles = "Estudiantes")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        public Alumno Alumno { get; set; } = default!;
        public Ciclo Ciclo { get; set; } = default!;
        [BindProperty]
        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<Arancel> Arancel { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademica { get; set; } = default!;
        public IList<DetallesCobroArancel> DetallesCobroArancel { get; set; } = default!;
        public void OnGet()
        {
            var userId = User.FindFirstValue("UserId") ?? "0"; 
            int idusuario = int.Parse(userId);
            int rol = _context.Usuarios.Where(x => x.IdUsuario == idusuario).First().NivelAccesoId;
            this.Alumno = _context.Alumno.Include(c=>c.Carrera).Where(c=>c.UsuarioId == idusuario).First();

            Ciclo = _context.Ciclos.Where(x => x.Activo == true).First();

            //seleccionar todas las materias inscritas por el alumno
            MateriasInscritas =  _context.MateriasInscritas
            .Include(mi => mi.MateriasGrupo)
                .ThenInclude(mg => mg.Materia)
            .Include(mi => mi.MateriasGrupo)
                .ThenInclude(mg => mg.Grupo)
                .ThenInclude(mg => mg.Docente)
            .Where(mi => mi.MateriasGrupo.Grupo.CicloId == Ciclo.Id && mi.Alumno.AlumnoId == Alumno.AlumnoId)
            .ToList();

            // Consulta modificada para manejar múltiples pagos
            Arancel =  _context.Aranceles.Where(x => x.Ciclo.Id == Ciclo.Id)
                 .Include(a => a.Ciclo).ToList();

            DetallesCobroArancel = _context.DetallesCobrosArancel
                .Include(x => x.CobroArancel)
                .Include(x => x.Arancel)
                .Where(x => x.CobroArancel.CicloId == Ciclo.Id && x.CobroArancel.AlumnoId == Alumno.AlumnoId).ToList();


            ActividadAcademica =  _context.ActividadesAcademicas
                .Include(a => a.Arancel)
                .Include(a => a.Ciclo).Where(c => c.CicloId == Ciclo.Id).ToList();
            // var alumnos = _context.Alumno.Include(c => c.Carrera).Where(c => c.UsuarioId == idusuario).First();


            // var x = 10;


        }
    }
}
