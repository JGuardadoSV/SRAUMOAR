﻿using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Alumnos
{
    public class Alumno
    {
        public int AlumnoId { get; set; }

        [Required(ErrorMessage = "El campo Nombres es obligatorio")]
        [MinLength(5, ErrorMessage="El nombre debe de tener al menos 5 caracteres")]
        [Display(Name = "Nombres")]
        public string? Nombres { get; set; }

        [Required(ErrorMessage = "El campo Apellidos es obligatorio")]
        [MinLength(5, ErrorMessage = "Los apellidos debe de tener al menos 5 caracteres")]
        [Display(Name = "Apellidos")]
        public string? Apellidos { get; set; }

        [DisplayFormat(ApplyFormatInEditMode = false, DataFormatString = "{0:dd/MM/yyyy}")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de nacimiento")]
        [Required(ErrorMessage = "El campo fecha es obligatorio")]
        public DateTime FechaDeNacimiento { get; set; }

        [ScaffoldColumn(false)]
        public DateTime FechaDeRegistro { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El email es obligatorio")]
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "El teléfono primario es obligatorio")]
        [Display(Name = "Teléfono primario")]
        [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "El teléfono primario no tiene un formato válido (xxxx-xxxx)")]
        public string? TelefonoPrimario { get; set; }

        [Required(ErrorMessage = "El teléfono primario es obligatorio")]
        [Display(Name = "Teléfono Whatsapp")]
        [RegularExpression(@"^\+\d{2,3} \d{4}-\d{4}$", ErrorMessage = "El teléfono de Whatsapp no tiene un formato válido (+xx xxxx-xxxx)")]
        public string? Whatsapp { get; set; }

        [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "El teléfono secundario no tiene un formato válido (xxxx-xxxx)")]
        [Display(Name = "Teléfono de secundario")]
        public string? TelefonoSecundario { get; set; }

        [Required(ErrorMessage = "El campo contacto de emergencia es obligatorio")]
        [Display(Name = "Contacto de emergencia")]
        public string? ContactoDeEmergencia { get; set; }

        [Required(ErrorMessage = "El teléfono del contacto de emergencia es obligatorio")]
        [Display(Name = "Teléfono de emergencia")]
        [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "El teléfono de emergencia no tiene un formato válido (xxxx-xxxx)")]
        public string? NumeroDeEmergencia { get; set; }

        [Required(ErrorMessage = "El campo Dirección de residencia es obligatorio")]
        [Display(Name = "Dirección de residencia")]
        public string? DireccionDeResidencia { get; set; }
    }

    public class Alumno2
    {
        public int Alumno2Id { get; set; }
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
        public DateTime FechaDeNacimiento { get; set; }
        public DateTime FechaDeRegistro { get; set; } = DateTime.Now;
        public string? Email { get; set; }
        public string? TelefonoPrimario { get; set; }
        public string? Whatsapp { get; set; }
        public string? TelefonoSecundario { get; set; }
        public string? ContactoDeEmergencia { get; set; }
        public string? NumeroDeEmergencia { get; set; }
        public string? DireccionDeResidencia { get; set; }
    }
}
