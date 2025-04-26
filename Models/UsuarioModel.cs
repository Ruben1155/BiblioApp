namespace BiblioApp.Models
{
    // Modelo para representar usuarios (similar al de la API, pero sin clave)
    public class UsuarioModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string TipoUsuario { get; set; } = string.Empty;
        public string? Clave { get; set; }
    }
}