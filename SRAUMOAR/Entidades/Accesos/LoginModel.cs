using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Accesos
{
    public class LoginModel
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        public string NombreUsuario { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [DataType(DataType.Password)]
        public string Clave { get; set; }
    }
}
