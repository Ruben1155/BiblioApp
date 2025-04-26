using System;
using System.ComponentModel.DataAnnotations;

namespace BiblioApp.Models
{
    // Modelo para representar préstamos (similar al de la API)
    public class PrestamoModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Usuario")]
        public int IdUsuario { get; set; }
        // Podrías añadir propiedades para mostrar nombres si el servicio los trae
        public string? NombreUsuario { get; set; }

        [Required]
        [Display(Name = "Libro")]
        public int IdLibro { get; set; }
        public string? TituloLibro { get; set; }

        [Display(Name = "Fecha Préstamo")]
        [DataType(DataType.DateTime)]
        public DateTime FechaPrestamo { get; set; }

        [Required]
        [Display(Name = "Devolución Esperada")]
        [DataType(DataType.Date)] // O DateTime según necesites
        public DateTime FechaDevolucionEsperada { get; set; }

        [Display(Name = "Devolución Real")]
        [DataType(DataType.Date)] // O DateTime
        public DateTime? FechaDevolucionReal { get; set; }

        [Required]
        public string Estado { get; set; } = string.Empty;

        // Propiedades adicionales si vas a usar SelectLists en las vistas
        // public List<SelectListItem>? UsuariosList { get; set; }
        // public List<SelectListItem>? LibrosList { get; set; }
    }
}