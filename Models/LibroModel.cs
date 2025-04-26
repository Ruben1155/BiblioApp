using System.ComponentModel.DataAnnotations; // Para atributos como [Display]

namespace BiblioApp.Models
{
    public class LibroModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El título es obligatorio.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El autor es obligatorio.")]
        public string Autor { get; set; } = string.Empty;

        [Required(ErrorMessage = "La editorial es obligatoria.")]
        public string Editorial { get; set; } = string.Empty;

        [Required(ErrorMessage = "El ISBN es obligatorio.")]
        public string ISBN { get; set; } = string.Empty;

        [Required(ErrorMessage = "El año es obligatorio.")]
        [Range(1000, 9999, ErrorMessage = "El año debe ser válido.")] // Ajusta el rango si es necesario
        [Display(Name = "Año")] // Nombre para mostrar en vistas
        public int Anio { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Required(ErrorMessage = "Las existencias son obligatorias.")]
        [Range(0, int.MaxValue, ErrorMessage = "Las existencias no pueden ser negativas.")]
        public int Existencias { get; set; }
    }
}