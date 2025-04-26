using System.ComponentModel.DataAnnotations; // Para [Display]

namespace BiblioApp.Models
{
    // Modelo para pasar los datos resumidos al Dashboard
    public class DashboardViewModel
    {
        [Display(Name = "Total Libros")]
        public int TotalLibros { get; set; }

        [Display(Name = "Total Usuarios")]
        public int TotalUsuarios { get; set; }

        [Display(Name = "Préstamos Activos")] // Pendientes o Atrasados
        public int PrestamosActivos { get; set; }

        // Podrías añadir más contadores si lo deseas, por ejemplo:
        // public int PrestamosAtrasados { get; set; }
        // public int LibrosDisponibles { get; set; }
    }
}
