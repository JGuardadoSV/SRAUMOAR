using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.grupos
{
    [Authorize(Roles = "Administrador,Administracion,Docentes")]
    public class HistoricoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public HistoricoModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Grupo> Grupo { get; set; } = default!;
        public IList<Carrera> Carreras { get; set; } = default!;
        public IList<Ciclo> Ciclos { get; set; } = default!;
        
        [BindProperty(SupportsGet = true)]
        public int? CicloId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? CarreraId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BuscarAlumno { get; set; }

        public Ciclo? CicloSeleccionado { get; set; }
        public Entidades.Alumnos.Alumno? AlumnoEncontrado { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Cargar carreras activas
            Carreras = await _context.Carreras.Where(c => c.Activa).ToListAsync();
            
            // Cargar todos los ciclos ordenados por año y número descendente
            Ciclos = await _context.Ciclos
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .ToListAsync();

            // Si no hay ciclo seleccionado, usar el activo por defecto
            if (!CicloId.HasValue)
            {
                var cicloActivo = await _context.Ciclos
                    .Where(c => c.Activo)
                    .FirstOrDefaultAsync();
                if (cicloActivo != null)
                {
                    CicloId = cicloActivo.Id;
                }
            }

            // Validar que el ciclo seleccionado existe
            if (CicloId.HasValue)
            {
                CicloSeleccionado = await _context.Ciclos
                    .FirstOrDefaultAsync(c => c.Id == CicloId.Value);
                
                if (CicloSeleccionado == null)
                {
                    CicloId = null;
                }
            }

            // Si hay búsqueda de alumno, buscar primero el alumno
            if (!string.IsNullOrWhiteSpace(BuscarAlumno))
            {
                var terminoBusqueda = BuscarAlumno.Trim();
                
                // Buscar por carnet O por parte antes del @ del email
                // Primero obtener todos los alumnos que coincidan parcialmente
                var alumnos = await _context.Alumno
                    .Where(a => 
                        (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda)) ||
                        (!string.IsNullOrEmpty(a.Email) && a.Email.Contains(terminoBusqueda))
                    )
                    .ToListAsync();
                
                // Filtrar en memoria: buscar por carnet O por parte antes del @ del email
                AlumnoEncontrado = alumnos.FirstOrDefault(a => 
                {
                    // 1. Buscar por carnet (si tiene carnet y contiene el término)
                    if (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda))
                        return true;
                    
                    // 2. Buscar por parte antes del @ del email (si tiene email y contiene @)
                    if (!string.IsNullOrEmpty(a.Email) && a.Email.Contains("@"))
                    {
                        var parteEmail = a.Email.Substring(0, a.Email.IndexOf("@"));
                        if (parteEmail.Contains(terminoBusqueda))
                            return true;
                    }
                    
                    return false;
                });
            }

            // Si hay ciclo seleccionado, cargar grupos
            if (CicloId.HasValue && CicloSeleccionado != null)
            {
                var userId = User.FindFirstValue("UserId") ?? "0";
                int idusuario = int.Parse(userId);
                int rol = _context.Usuarios.Where(x => x.IdUsuario == idusuario).First().NivelAccesoId;
                
                IQueryable<Grupo> query;
                
                if (rol == 1 || rol == 2) // Administrador o Administracion
                {
                    query = _context.Grupo
                       .Where(x => x.CicloId == CicloId.Value)
                       .Include(g => g.Carrera)
                       .Include(g => g.Ciclo)
                       .Include(g => g.Docente);
                }
                else if (rol == 3) // Docente
                {
                    int IdDocente = _context.Docentes.Where(x => x.UsuarioId == idusuario).First().DocenteId;
                    query = _context.Grupo
                   .Where(x => x.CicloId == CicloId.Value && x.Docente.DocenteId == IdDocente)
                   .Include(g => g.Carrera)
                   .Include(g => g.Ciclo)
                   .Include(g => g.Docente);
                }
                else
                {
                    return Unauthorized();
                }
                
                // Filtrar por carrera si está seleccionada
                if (CarreraId.HasValue && CarreraId.Value > 0)
                {
                    query = query.Where(g => g.CarreraId == CarreraId.Value);
                }

                // Si hay búsqueda de alumno y se encontró, filtrar grupos donde esté inscrito
                if (AlumnoEncontrado != null)
                {
                    var gruposConAlumno = await _context.MateriasInscritas
                        .Include(mi => mi.MateriasGrupo)
                            .ThenInclude(mg => mg.Grupo)
                        .Where(mi => mi.AlumnoId == AlumnoEncontrado.AlumnoId && 
                                     mi.MateriasGrupo != null && 
                                     mi.MateriasGrupo.Grupo != null &&
                                     mi.MateriasGrupo.Grupo.CicloId == CicloId.Value)
                        .Select(mi => mi.MateriasGrupo!.Grupo!.GrupoId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(g => gruposConAlumno.Contains(g.GrupoId));
                }
                
                Grupo = await query
                    .Include(g => g.MateriasGrupos!)
                        .ThenInclude(mg => mg.Materia)
                    .ToListAsync();
            }
            else
            {
                Grupo = new List<Grupo>();
            }

            return Page();
        }
    }
}

