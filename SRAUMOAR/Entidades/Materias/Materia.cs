﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Materias
{

    [Table("Materias")]
    public class Materia
    {
        public int MateriaId { get; set; }

        [Required(ErrorMessage = "El campo nombre de la materia es obligatorio")]
        [Display(Name = "Nombre de la materia")]
        public string? NombreMateria { get; set; }

        [Required(ErrorMessage = "El campo código es obligatorio")]
        [Display(Name = "Código de la materia")]
        public string? CodigoMateria { get; set; }

        [Required(ErrorMessage = "El campo correlativo es obligatorio")]
        [Display(Name = "Correlativo en pensum")]
        public int Correlativo { get; set; }

        [Required(ErrorMessage = "El campo ciclo es obligatorio")]
        [Display(Name = "Ciclo en el que se imparte")]
        public int Ciclo { get; set; }

        [Required(ErrorMessage = "El campo UV es obligatorio")]
        [Display(Name = "Unidades Valorativas")]
        public int uv { get; set; }

        [Required(ErrorMessage = "El campo pensum es obligatorio")]
        [Display(Name = "Pensum al que pertenece")]
        public int PensumId { get; set; }

        
        [Display(Name = "Es requisito bachillerato")]
        public Boolean RequisitoBachillerato { get; set; }


        public virtual Pensum? Pensum { get; set; }

        public ICollection<MateriaPrerequisito>? Prerrequisitos { get; set; }

       
    }
}
