using System.ComponentModel.DataAnnotations; // Necesario para DataAnnotations

namespace BiblioApp.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La clave es obligatoria.")]
        [DataType(DataType.Password)]
        public string Clave { get; set; } = string.Empty;
    }
}