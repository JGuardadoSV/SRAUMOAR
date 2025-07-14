using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.grupos
{
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        [BindProperty]
        public MateriasGrupo MateriasGrupo { get; set; } = default!;
        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty]
        public IList<MateriasGrupo> ListadoMateriasGrupo { get; set; } = default!;

        [BindProperty]
        public Grupo Grupo { get; set; } = default!;

        [BindProperty]
        public int? GrupoId { get; set; }
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            GrupoId = id.Value;
            if (id == null) //Id del grupo a administrar
            {
                return NotFound();
            }

            var grupo = await _context.Grupo.FirstOrDefaultAsync(m => m.GrupoId == id); //datos completos del grupo
            if (grupo == null)
            {
                return NotFound();
            }
            Grupo = grupo;
            var carreras = _context.Carreras.Where(x=>x.CarreraId==grupo.CarreraId).ToList();
            var pensums = _context.Pensums.Where(x => x.PensumId == grupo.PensumId).ToList();

            carreras.Insert(0, new Carrera { CarreraId = 0, NombreCarrera = "Seleccione una carrera" });   //para el select
            ViewData["CarreraId"] = new SelectList(carreras, "CarreraId", "NombreCarrera");//para el select
            pensums.Insert(0, new Pensum { PensumId = 0, NombrePensum = "Seleccione un pensum" });
            ViewData["PensumId"] = new SelectList(pensums, "PensumId", "NombrePensum");
            
            
            ViewData["CicloId"] = new SelectList(
                  _context.Ciclos
                  .Where(x => x.Activo == true).Where(x => x.Id == grupo.CicloId) //el ciclo que seleccionó al registrar el grupo
                  .Select(d => new
                  {
                      Id = d.Id,
                      Ciclon = d.NCiclo + "-" + d.anio
                  }), "Id", "Ciclon");  //CICLO



            ViewData["DocenteId"] = new SelectList(
                 _context.Docentes.Select(d => new
                 {
                     DocenteId = d.DocenteId,
                     NombreCompleto = d.Nombres + " " + d.Apellidos
                 }),
                 "DocenteId",
                 "NombreCompleto"
             );


            // Obtener los IDs de las materias ya registradas en el grupo
            var materiasRegistradas = _context.MateriasGrupo
                .Where(mg => mg.GrupoId == grupo.GrupoId)
                .Select(mg => mg.MateriaId)
                .ToList();

            // Obtener las materias que no están registradas en el grupo
            var materiasDisponibles = _context.Materias
                .Include(x => x.Pensum)
                .ThenInclude(x => x.Carrera)
                .Where(x => x.Pensum.Activo == true && x.Pensum.CarreraId == grupo.CarreraId)
                .Where(x => !materiasRegistradas.Contains(x.MateriaId)) // Filtrar materias no registradas
                .Select(x => new
                {
                    MateriaId = x.MateriaId,
                    NombreCompleto = x.NombreMateria + " - " + x.Pensum.CodigoPensum
                })
                .ToList();

            // Agregar la materia actual de cada MateriasGrupo a la lista de materias disponibles
            var materiasGrupoActuales = _context.MateriasGrupo
                .Where(mg => mg.GrupoId == grupo.GrupoId)
                .Select(mg => mg.Materia)
                .Where(m => m != null)
                .Select(m => new {
                    MateriaId = m.MateriaId,
                    NombreCompleto = m.NombreMateria + " - " + m.Pensum.CodigoPensum
                })
                .ToList();

            // Unir ambas listas y eliminar duplicados
            var materiasParaSelect = materiasDisponibles
                .Union(materiasGrupoActuales)
                .GroupBy(m => m.MateriaId)
                .Select(g => g.First())
                .ToList();

            ViewData["MateriaId"] = new SelectList(
                materiasParaSelect,
                "MateriaId",
                "NombreCompleto"
            );



            ListadoMateriasGrupo = await _context.MateriasGrupo.Where(x => x.GrupoId == id)
                .Include(m => m.Grupo)
                .Include(m => m.Materia)
                .Include(m => m.Docente)
                .Include(m=>m.MateriasInscritas)
                .Where(x => x.GrupoId == id)
                .ToListAsync();



            ViewData["GrupoId"] = new SelectList(_context.Grupo.Where(x => x.GrupoId == id), "GrupoId", "Nombre");

            return Page();
        }
       
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            int idredireccion = 0;
            if (Request.Form.ContainsKey("Grupo.Limite"))
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                 _context.Attach(Grupo).State = EntityState.Modified;
                idredireccion = Grupo.GrupoId;
                //_context.MateriasGrupo.Add(MateriasGrupo);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrupoExists(Grupo.GrupoId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else if (Request.Form.ContainsKey("MateriasGrupo.Aula"))
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }
                idredireccion=MateriasGrupo.GrupoId;
                // verificar si ya esta registrada 
                // Verificar si la materia ya está registrada en el grupo
                var materiaDuplicada = await _context.MateriasGrupo
                    .AnyAsync(mg => mg.GrupoId == MateriasGrupo.GrupoId && mg.MateriaId == MateriasGrupo.MateriaId);

                if (materiaDuplicada)
                {
                    ModelState.AddModelError(string.Empty, "Esta materia ya está registrada en este grupo.");
                    //return Page();
                    return RedirectToPage("./Edit", new { id = MateriasGrupo.GrupoId });
                }



                // _context.Attach(Grupo).State = EntityState.Modified;
                _context.MateriasGrupo.Add(MateriasGrupo);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrupoExists(Grupo.GrupoId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }




            return RedirectToPage("./Edit", new { id = idredireccion });
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostEditarMateriaGrupoAsync()
        {
            int materiasGrupoId = int.Parse(Request.Form["MateriasGrupoId"]);
            var materiaGrupo = await _context.MateriasGrupo.FindAsync(materiasGrupoId);
            if (materiaGrupo == null)
            {
                return new JsonResult(new { success = false, message = "No se encontró la materia del grupo." });
            }

            // Actualizar campos
            materiaGrupo.MateriaId = int.Parse(Request.Form["MateriaId"]);
            materiaGrupo.Aula = Request.Form["Aula"];
            materiaGrupo.Dia = Enum.TryParse(typeof(DiaSemana), Request.Form["Dia"], out var dia) ? (DiaSemana)dia : materiaGrupo.Dia;
            materiaGrupo.HoraInicio = TimeSpan.Parse(Request.Form["HoraInicio"]);
            materiaGrupo.HoraFin = TimeSpan.Parse(Request.Form["HoraFin"]);
            materiaGrupo.DocenteId = int.Parse(Request.Form["DocenteId"]);

            try
            {
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Error al actualizar: " + ex.Message });
            }
        }

        private bool GrupoExists(int id)
        {
            return _context.Grupo.Any(e => e.GrupoId == id);
        }
    }
}
