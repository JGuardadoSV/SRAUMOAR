using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.Autenticacion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class ResetearContrasenasModel : PageModel
    {
        private readonly Contexto _context;
        private readonly IEmailService _emailService;

        public ResetearContrasenasModel(Contexto context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public bool MostrarSeleccion { get; set; }
        public string ResultadoMensaje { get; set; }
        public List<SelectListItem> ListaSeleccion { get; set; } = new List<SelectListItem>();

        [BindProperty]
        public ResetInput Input { get; set; } = new ResetInput();

        public class ResetInput
        {
            [Required]
            public int Rol { get; set; } // 3 Docente, 4 Alumno

            [Required]
            public string Modo { get; set; } = "Todos"; // Todos | Seleccionados

            public List<int> SeleccionIds { get; set; } = new List<int>(); // Ids de AlumnoId o DocenteId segun rol

            public bool EnviarEmail { get; set; } = false; // Enviar notificación por email

            [Required(ErrorMessage = "Debe confirmar antes de aplicar cambios.")]
            public bool Confirm { get; set; }
        }

        public void OnGet()
        {
            // estado inicial
            Input.Rol = 3;
            Input.Modo = "Todos";
            MostrarSeleccion = false;
            // Cargar lista inicial si es necesario
            _ = CargarSeleccionAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarSeleccionAsync();
                return Page();
            }

            MostrarSeleccion = string.Equals(Input.Modo, "Seleccionados", StringComparison.OrdinalIgnoreCase);

            if (MostrarSeleccion && (Input.SeleccionIds == null || Input.SeleccionIds.Count == 0))
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar al menos un registro.");
                await CargarSeleccionAsync();
                return Page();
            }

            int rolObjetivo = Input.Rol; // 3 Docente, 4 Alumno

            int afectados = 0;
            int sinUsuario = 0;
            int sinEmail = 0;
            int creados = 0;
            int corregidosDuplicados = 0;
            int emailsEnviados = 0;
            int emailsFallidos = 0;
            var alumnosPorVincular = new List<(Alumno alumno, Usuario user, string nombreCompleto)>();
            var docentesPorVincular = new List<(Docente docente, Usuario user, string nombreCompleto)>();
            var notificaciones = new List<(string email, string nombreUsuario, string clave, string nombreCompleto)>();

            if (rolObjetivo == 4)
            {
                // Alumnos
                IQueryable<Alumno> query = _context.Alumno;
                if (MostrarSeleccion)
                {
                    query = query.Where(a => Input.SeleccionIds.Contains(a.AlumnoId));
                }

                var alumnos = await query.Select(a => new { Entidad = a, a.AlumnoId, a.UsuarioId, a.Email, a.Nombres, a.Apellidos }).ToListAsync();
                var usuarioIds = alumnos.Where(a => a.UsuarioId.HasValue).Select(a => a.UsuarioId.Value).ToList();

                var usuarios = await _context.Usuarios.Where(u => usuarioIds.Contains(u.IdUsuario) || (u.NivelAccesoId == 4)).ToListAsync();

                foreach (var al in alumnos)
                {
                    var alumno = al.Entidad;
                    Usuario user = null;
                    string email = alumno.Email;
                    string nombreCompleto = ($"{al.Nombres} {al.Apellidos}").Trim();
                    
                    if (alumno.UsuarioId.HasValue)
                    {
                        user = usuarios.FirstOrDefault(u => u.IdUsuario == alumno.UsuarioId.Value);
                        if (user == null)
                        {
                            sinUsuario++;
                            continue;
                        }
                        email = string.IsNullOrWhiteSpace(alumno.Email) ? user.Email : alumno.Email;
                    }

                    if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                    {
                        sinEmail++;
                        continue;
                    }
                    string local = email.Split('@')[0];
                    // Verificar duplicados por email (NombreUsuario)
                    var existentePorMail = usuarios.FirstOrDefault(u => u.NombreUsuario == email);
                    if (alumno.UsuarioId.HasValue && user != null)
                    {
                        // actualizar existente
                        if (existentePorMail != null && existentePorMail.IdUsuario != user.IdUsuario)
                        {
                            corregidosDuplicados++;
                        }
                        user.NombreUsuario = email;
                        user.Email = email;
                        user.Clave = local;
                        user.NivelAccesoId = 4;
                        afectados++;
                        // Agregar a notificación
                        notificaciones.Add((user.Email, user.NombreUsuario, user.Clave, nombreCompleto));
                    }
                    else
                    {
                        // crear usuario nuevo o reutilizar existente por email
                        if (existentePorMail != null)
                        {
                            user = existentePorMail;
                        }
                        else
                        {
                            user = new Usuario
                            {
                                NombreUsuario = email,
                                Email = email,
                                Clave = local,
                                NivelAccesoId = 4,
                                Activo = true
                            };
                            _context.Usuarios.Add(user);
                            usuarios.Add(user);
                            creados++;
                        }
                        alumnosPorVincular.Add((alumno, user, nombreCompleto));
                        afectados++;
                        // Agregar a notificación (se usará tras guardar)
                        notificaciones.Add((email, email, local, nombreCompleto));
                    }
                }
            }
            else if (rolObjetivo == 3)
            {
                // Docentes
                IQueryable<Docente> query = _context.Docentes;
                if (MostrarSeleccion)
                {
                    query = query.Where(d => Input.SeleccionIds.Contains(d.DocenteId));
                }

                var docentes = await query.Select(d => new { Entidad = d, d.DocenteId, d.UsuarioId, d.Email, d.Nombres, d.Apellidos }).ToListAsync();
                var usuarioIds = docentes.Where(d => d.UsuarioId.HasValue).Select(d => d.UsuarioId.Value).ToList();

                var usuarios = await _context.Usuarios.Where(u => usuarioIds.Contains(u.IdUsuario) || (u.NivelAccesoId == 3)).ToListAsync();

                foreach (var dc in docentes)
                {
                    var docente = dc.Entidad;
                    Usuario user = null;
                    string email = docente.Email;
                    string nombreCompleto = ($"{dc.Nombres} {dc.Apellidos}").Trim();
                    
                    if (docente.UsuarioId.HasValue)
                    {
                        user = usuarios.FirstOrDefault(u => u.IdUsuario == docente.UsuarioId.Value);
                        if (user == null)
                        {
                            sinUsuario++;
                            continue;
                        }
                        email = string.IsNullOrWhiteSpace(docente.Email) ? user.Email : docente.Email;
                    }
                    if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                    {
                        sinEmail++;
                        continue;
                    }
                    string local = email.Split('@')[0];
                    var existentePorMail = usuarios.FirstOrDefault(u => u.NombreUsuario == email);
                    if (docente.UsuarioId.HasValue && user != null)
                    {
                        if (existentePorMail != null && existentePorMail.IdUsuario != user.IdUsuario)
                        {
                            corregidosDuplicados++;
                        }
                        user.NombreUsuario = email;
                        user.Email = email;
                        user.Clave = local;
                        user.NivelAccesoId = 3;
                        afectados++;
                        // Agregar a notificación
                        notificaciones.Add((user.Email, user.NombreUsuario, user.Clave, nombreCompleto));
                    }
                    else
                    {
                        if (existentePorMail != null)
                        {
                            user = existentePorMail;
                        }
                        else
                        {
                            user = new Usuario
                            {
                                NombreUsuario = email,
                                Email = email,
                                Clave = local,
                                NivelAccesoId = 3,
                                Activo = true
                            };
                            _context.Usuarios.Add(user);
                            usuarios.Add(user);
                            creados++;
                        }
                        docentesPorVincular.Add((docente, user, nombreCompleto));
                        afectados++;
                        // Agregar a notificación (se usará tras guardar)
                        notificaciones.Add((email, email, local, nombreCompleto));
                    }
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Rol no válido.");
                await CargarSeleccionAsync();
                return Page();
            }

            // Primer guardado para obtener IdUsuario de nuevos usuarios
            await _context.SaveChangesAsync();

            // Vincular ahora que los IdUsuario ya existen
            foreach (var pair in alumnosPorVincular)
            {
                pair.alumno.UsuarioId = pair.user.IdUsuario;
            }
            foreach (var pair in docentesPorVincular)
            {
                pair.docente.UsuarioId = pair.user.IdUsuario;
            }

            if (alumnosPorVincular.Count > 0 || docentesPorVincular.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            // Enviar emails si está habilitado
            if (Input.EnviarEmail && notificaciones.Count > 0)
            {
                foreach (var n in notificaciones)
                {
                    try
                    {
                        bool enviado = await _emailService.EnviarNotificacionCambioContrasenaAsync(
                            n.email,
                            n.nombreUsuario,
                            n.clave,
                            n.nombreCompleto);
                        if (enviado) emailsEnviados++; else emailsFallidos++;
                    }
                    catch { emailsFallidos++; }
                }
            }

            // Construir mensaje de resultado
            var mensaje = $"Usuarios actualizados: {afectados}. Creados: {creados}. Posibles duplicados corregidos: {corregidosDuplicados}. Sin usuario vinculado: {sinUsuario}. Sin email válido: {sinEmail}.";
            
            if (Input.EnviarEmail)
            {
                mensaje += $" Emails enviados: {emailsEnviados}. Emails fallidos: {emailsFallidos}.";
            }

            ResultadoMensaje = mensaje;

            // Limpiar el estado del formulario después de completar
            Input = new ResetInput
            {
                Rol = Input.Rol, // Mantener el rol seleccionado
                Modo = Input.Modo, // Mantener el modo seleccionado
                SeleccionIds = new List<int>(), // Limpiar selección
                EnviarEmail = false, // Desmarcar checkbox de email
                Confirm = false // Desmarcar checkbox de confirmación
            };

            await CargarSeleccionAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCargarAsync(int rol)
        {
            Input = new ResetInput { Rol = rol, Modo = "Seleccionados" };
            await CargarSeleccionAsync();
            
            // Devolver solo el HTML de la lista para AJAX
            return Partial("_ListaSeleccion", new { ListaSeleccion, MostrarSeleccion = true });
        }

        private async Task CargarSeleccionAsync()
        {
            MostrarSeleccion = string.Equals(Input?.Modo, "Seleccionados", StringComparison.OrdinalIgnoreCase);
            ListaSeleccion = new List<SelectListItem>();
            if (!MostrarSeleccion)
            {
                return;
            }
            int rol = Input?.Rol ?? 3;
            if (rol == 4)
            {
                var items = await _context.Alumno
                    .OrderBy(a => a.Nombres)
                    .Select(a => new SelectListItem
                    {
                        Value = a.AlumnoId.ToString(),
                        Text = (a.Nombres + " " + a.Apellidos + (string.IsNullOrWhiteSpace(a.Email) ? string.Empty : (" - " + a.Email))).Trim()
                    })
                    .ToListAsync();
                ListaSeleccion.AddRange(items);
            }
            else
            {
                var items = await _context.Docentes
                    .OrderBy(d => d.Nombres)
                    .Select(d => new SelectListItem
                    {
                        Value = d.DocenteId.ToString(),
                        Text = (d.Nombres + " " + d.Apellidos + (string.IsNullOrWhiteSpace(d.Email) ? string.Empty : (" - " + d.Email))).Trim()
                    })
                    .ToListAsync();
                ListaSeleccion.AddRange(items);
            }
        }
    }
}


