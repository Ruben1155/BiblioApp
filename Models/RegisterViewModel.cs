using System.ComponentModel.DataAnnotations; // Necesario para DataAnnotations

namespace BiblioApp.Models
{
    // Modelo para los datos del formulario de registro público
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [Display(Name = "Nombre")]
        [StringLength(50)] // Limitar longitud
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [Display(Name = "Apellido")]
        [StringLength(50)]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [Display(Name = "Correo Electrónico")]
        [StringLength(100)]
        public string Correo { get; set; } = string.Empty;

        [Phone(ErrorMessage = "El formato del teléfono no es válido.")]
        [Display(Name = "Teléfono (Opcional)")]
        [StringLength(20)]
        public string? Telefono { get; set; } // Opcional

        [Required(ErrorMessage = "El tipo de usuario es obligatorio.")]
        [Display(Name = "Tipo de Usuario")]
        [StringLength(20)]
        public string TipoUsuario { get; set; } = string.Empty; // Considerar usar un enum o dropdown fijo

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        // Ajustar requisitos de longitud según necesidad
        [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} y máximo {1} caracteres.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Clave { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe confirmar la contraseña.")] // Añadido Required
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Contraseña")]
        [Compare("Clave", ErrorMessage = "La contraseña y la confirmación no coinciden.")] // Compara con el campo 'Clave'
        public string ConfirmarClave { get; set; } = string.Empty;
    }
}
