using BiblioApp.Models; // Necesario para DashboardViewModel
using BiblioApp.Services; // Necesario para los servicios
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq; // Necesario para .Count() y .Where()
using System.Threading.Tasks;

namespace BiblioApp.Controllers
{
    // Controlador para el Dashboard, accesible solo por administradores
    public class DashboardController : AdminBaseController // Hereda de AdminBaseController para protección
    {
        private readonly LibroService _libroService;
        private readonly UsuarioService _usuarioService;
        private readonly PrestamoService _prestamoService;
        private readonly ILogger<DashboardController> _logger;

        // Inyectar los servicios necesarios
        public DashboardController(
            LibroService libroService,
            UsuarioService usuarioService,
            PrestamoService prestamoService,
            ILogger<DashboardController> logger)
        {
            _libroService = libroService ?? throw new ArgumentNullException(nameof(libroService));
            _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
            _prestamoService = prestamoService ?? throw new ArgumentNullException(nameof(prestamoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /Dashboard o /Dashboard/Index
        // Muestra la página principal del Dashboard
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Accediendo al Dashboard.");
            var viewModel = new DashboardViewModel(); // Crear instancia del ViewModel

            try
            {
                // Obtener los datos de los diferentes servicios en paralelo para eficiencia
                var librosTask = _libroService.GetAllLibrosAsync();
                var usuariosTask = _usuarioService.GetAllUsuariosAsync();
                var prestamosTask = _prestamoService.GetAllPrestamosAsync();

                // Esperar a que todas las tareas terminen
                await Task.WhenAll(librosTask, usuariosTask, prestamosTask);

                // Obtener los resultados
                var libros = await librosTask;
                var usuarios = await usuariosTask;
                var prestamos = await prestamosTask;

                // Calcular los contadores
                viewModel.TotalLibros = libros?.Count() ?? 0;
                viewModel.TotalUsuarios = usuarios?.Count() ?? 0;
                viewModel.PrestamosActivos = prestamos?
                                            .Count(p => p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
                                                        p.Estado.Equals("Atrasado", StringComparison.OrdinalIgnoreCase)) ?? 0;

                // Podrías calcular más datos aquí si los añades al ViewModel
                // viewModel.LibrosDisponibles = libros?.Count(l => l.Existencias > 0) ?? 0;

                _logger.LogInformation("Datos para Dashboard calculados: Libros={TotalLibros}, Usuarios={TotalUsuarios}, PréstamosActivos={PrestamosActivos}",
                    viewModel.TotalLibros, viewModel.TotalUsuarios, viewModel.PrestamosActivos);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para el Dashboard.");
                // Mostrar un mensaje de error y quizás valores por defecto o -1 para indicar error
                TempData["ErrorMessage"] = "Error al cargar los datos del dashboard.";
                viewModel.TotalLibros = -1; // Indicar error
                viewModel.TotalUsuarios = -1;
                viewModel.PrestamosActivos = -1;
            }

            // Pasar el ViewModel a la vista
            return View(viewModel);
        }
    }
}
