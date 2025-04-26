using BiblioApp.Models;
using BiblioApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
// --- Añadir using para Sesión ---
using Microsoft.AspNetCore.Http;

namespace BiblioApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HomeService _homeService;
        private readonly UsuarioService _usuarioService; // <--- INYECTAR UsuarioService

        // Modificar constructor para incluir UsuarioService
        public HomeController(ILogger<HomeController> logger, HomeService homeService, UsuarioService usuarioService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _homeService = homeService ?? throw new ArgumentNullException(nameof(homeService));
            _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService)); // <--- Asignar UsuarioService
        }

        // GET: /Home/Index o /
        // Muestra la página de Login
        [HttpGet]
        public IActionResult Index()
        {
            // Si ya está logueado, redirigir a Libros (o Dashboard)
            if (HttpContext.Session.GetInt32("UserId").HasValue)
            {
                return RedirectToAction("Index", "Libro");
            }
            // Pasar un mensaje de éxito si viene desde el registro
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            return View(new LoginViewModel());
        }

        // POST: /Home/Login
        // Procesa el inicio de sesión
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel loginViewModel)
        {
            _logger.LogInformation("Intento de login para {Correo}", loginViewModel.Correo);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Modelo de login inválido.");
                return View("Index", loginViewModel);
            }

            try
            {
                var usuarioValidado = await _homeService.ValidarUsuarioAsync(loginViewModel);

                if (usuarioValidado != null && usuarioValidado.Id > 0)
                {
                    _logger.LogInformation("Login exitoso para Usuario ID: {UserId}, Correo: {Correo}, Tipo: {TipoUsuario}",
                        usuarioValidado.Id, usuarioValidado.Correo, usuarioValidado.TipoUsuario);

                    // Guardar datos en sesión
                    HttpContext.Session.SetInt32("UserId", usuarioValidado.Id);
                    HttpContext.Session.SetString("UserName", $"{usuarioValidado.Nombre} {usuarioValidado.Apellido}");
                    HttpContext.Session.SetString("UserRole", usuarioValidado.TipoUsuario ?? "Desconocido");

                    // Redirección basada en tipo
                    if (usuarioValidado.TipoUsuario?.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _logger.LogInformation("Usuario Administrador logueado. Redirigiendo al Dashboard.");
                        return RedirectToAction("Index", "Dashboard");
                    }
                    else
                    {
                        _logger.LogInformation("Usuario Estándar logueado. Redirigiendo a Libros.");
                        return RedirectToAction("Index", "Libro");
                    }
                }
                else
                {
                    _logger.LogWarning("Login fallido para {Correo}: Credenciales inválidas según API.", loginViewModel.Correo);
                    ModelState.AddModelError(string.Empty, "Correo o clave incorrectos.");
                    return View("Index", loginViewModel);
                }
            }
            catch (ApplicationException ex) // Error de conexión
            {
                _logger.LogError(ex, "Error de aplicación durante el login para {Correo}", loginViewModel.Correo);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Index", loginViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado durante el login para {Correo}", loginViewModel.Correo);
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intente de nuevo.");
                return View("Index", loginViewModel);
            }
        }

        // --- ACCIÓN DE LOGOUT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            _logger.LogInformation("Usuario ID: {UserId} realizando Logout.", HttpContext.Session.GetInt32("UserId"));
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // --- NUEVAS ACCIONES DE REGISTRO ---

        // GET: /Home/Register
        // Muestra el formulario de registro
        [HttpGet]
        public IActionResult Register()
        {
            _logger.LogInformation("Accediendo a la página de registro.");
            // Devolver la vista con un modelo vacío
            return View(new RegisterViewModel());
        }

        // POST: /Home/Register
        // Procesa los datos del formulario de registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            _logger.LogInformation("Intento de registro para el correo: {Correo}", model.Correo);

            // La validación [Compare] ya verifica si las contraseñas coinciden
            if (ModelState.IsValid)
            {
                // Mapear RegisterViewModel a UsuarioModel (el que espera el servicio)
                // IMPORTANTE: Incluir la Clave aquí, ya que es el registro público
                var nuevoUsuario = new UsuarioModel
                {
                    Nombre = model.Nombre,
                    Apellido = model.Apellido,
                    Correo = model.Correo,
                    Telefono = model.Telefono,
                    TipoUsuario = model.TipoUsuario,
                    Clave = model.Clave // <--- Pasar la contraseña introducida
                };

                try
                {
                    // Llamar al servicio para crear el usuario (enviando la contraseña)
                    var usuarioCreado = await _usuarioService.CreateUsuarioAsync(nuevoUsuario);

                    if (usuarioCreado != null)
                    {
                        _logger.LogInformation("Registro exitoso para el correo: {Correo}. Redirigiendo a Login.", model.Correo);
                        // Mostrar mensaje de éxito en la página de Login
                        TempData["SuccessMessage"] = "¡Registro exitoso! Ahora puedes iniciar sesión.";
                        return RedirectToAction("Index", "Home"); // Redirigir a la página de Login
                    }
                    else
                    {
                        // Esto no debería ocurrir si la API devuelve el objeto o lanza excepción
                        _logger.LogError("La llamada API para registrar usuario {Correo} devolvió null inesperadamente.", model.Correo);
                        ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado durante el registro.");
                    }
                }
                catch (InvalidOperationException ex) // Capturar errores de negocio (ej. correo duplicado)
                {
                    _logger.LogWarning(ex, "Error de operación durante el registro para {Correo}", model.Correo);
                    // Mostrar el mensaje de error específico (ej. "El correo ya existe")
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (ApplicationException ex) // Error de conexión con la API
                {
                    _logger.LogError(ex, "Error de conexión con la API durante el registro para {Correo}", model.Correo);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex) // Otros errores inesperados
                {
                    _logger.LogError(ex, "Error inesperado durante el registro para {Correo}", model.Correo);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado durante el registro.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo de registro inválido para el correo: {Correo}", model.Correo);
                // Modelo inválido (ej. contraseñas no coinciden, campos requeridos faltantes)
            }

            // Si llegamos aquí, hubo un error, volver a mostrar el formulario de registro con los errores
            return View(model);
        }
        // ---------------------------------


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}