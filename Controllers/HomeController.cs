using BiblioApp.Models;
using BiblioApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
// --- A�adir using para Sesi�n ---
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
        // Muestra la p�gina de Login
        [HttpGet]
        public IActionResult Index()
        {
            // Si ya est� logueado, redirigir a Libros (o Dashboard)
            if (HttpContext.Session.GetInt32("UserId").HasValue)
            {
                return RedirectToAction("Index", "Libro");
            }
            // Pasar un mensaje de �xito si viene desde el registro
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            return View(new LoginViewModel());
        }

        // POST: /Home/Login
        // Procesa el inicio de sesi�n
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel loginViewModel)
        {
            _logger.LogInformation("Intento de login para {Correo}", loginViewModel.Correo);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Modelo de login inv�lido.");
                return View("Index", loginViewModel);
            }

            try
            {
                var usuarioValidado = await _homeService.ValidarUsuarioAsync(loginViewModel);

                if (usuarioValidado != null && usuarioValidado.Id > 0)
                {
                    _logger.LogInformation("Login exitoso para Usuario ID: {UserId}, Correo: {Correo}, Tipo: {TipoUsuario}",
                        usuarioValidado.Id, usuarioValidado.Correo, usuarioValidado.TipoUsuario);

                    // Guardar datos en sesi�n
                    HttpContext.Session.SetInt32("UserId", usuarioValidado.Id);
                    HttpContext.Session.SetString("UserName", $"{usuarioValidado.Nombre} {usuarioValidado.Apellido}");
                    HttpContext.Session.SetString("UserRole", usuarioValidado.TipoUsuario ?? "Desconocido");

                    // Redirecci�n basada en tipo
                    if (usuarioValidado.TipoUsuario?.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _logger.LogInformation("Usuario Administrador logueado. Redirigiendo al Dashboard.");
                        return RedirectToAction("Index", "Dashboard");
                    }
                    else
                    {
                        _logger.LogInformation("Usuario Est�ndar logueado. Redirigiendo a Libros.");
                        return RedirectToAction("Index", "Libro");
                    }
                }
                else
                {
                    _logger.LogWarning("Login fallido para {Correo}: Credenciales inv�lidas seg�n API.", loginViewModel.Correo);
                    ModelState.AddModelError(string.Empty, "Correo o clave incorrectos.");
                    return View("Index", loginViewModel);
                }
            }
            catch (ApplicationException ex) // Error de conexi�n
            {
                _logger.LogError(ex, "Error de aplicaci�n durante el login para {Correo}", loginViewModel.Correo);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Index", loginViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado durante el login para {Correo}", loginViewModel.Correo);
                ModelState.AddModelError(string.Empty, "Ocurri� un error inesperado. Intente de nuevo.");
                return View("Index", loginViewModel);
            }
        }

        // --- ACCI�N DE LOGOUT ---
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
            _logger.LogInformation("Accediendo a la p�gina de registro.");
            // Devolver la vista con un modelo vac�o
            return View(new RegisterViewModel());
        }

        // POST: /Home/Register
        // Procesa los datos del formulario de registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            _logger.LogInformation("Intento de registro para el correo: {Correo}", model.Correo);

            // La validaci�n [Compare] ya verifica si las contrase�as coinciden
            if (ModelState.IsValid)
            {
                // Mapear RegisterViewModel a UsuarioModel (el que espera el servicio)
                // IMPORTANTE: Incluir la Clave aqu�, ya que es el registro p�blico
                var nuevoUsuario = new UsuarioModel
                {
                    Nombre = model.Nombre,
                    Apellido = model.Apellido,
                    Correo = model.Correo,
                    Telefono = model.Telefono,
                    TipoUsuario = model.TipoUsuario,
                    Clave = model.Clave // <--- Pasar la contrase�a introducida
                };

                try
                {
                    // Llamar al servicio para crear el usuario (enviando la contrase�a)
                    var usuarioCreado = await _usuarioService.CreateUsuarioAsync(nuevoUsuario);

                    if (usuarioCreado != null)
                    {
                        _logger.LogInformation("Registro exitoso para el correo: {Correo}. Redirigiendo a Login.", model.Correo);
                        // Mostrar mensaje de �xito en la p�gina de Login
                        TempData["SuccessMessage"] = "�Registro exitoso! Ahora puedes iniciar sesi�n.";
                        return RedirectToAction("Index", "Home"); // Redirigir a la p�gina de Login
                    }
                    else
                    {
                        // Esto no deber�a ocurrir si la API devuelve el objeto o lanza excepci�n
                        _logger.LogError("La llamada API para registrar usuario {Correo} devolvi� null inesperadamente.", model.Correo);
                        ModelState.AddModelError(string.Empty, "Ocurri� un error inesperado durante el registro.");
                    }
                }
                catch (InvalidOperationException ex) // Capturar errores de negocio (ej. correo duplicado)
                {
                    _logger.LogWarning(ex, "Error de operaci�n durante el registro para {Correo}", model.Correo);
                    // Mostrar el mensaje de error espec�fico (ej. "El correo ya existe")
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (ApplicationException ex) // Error de conexi�n con la API
                {
                    _logger.LogError(ex, "Error de conexi�n con la API durante el registro para {Correo}", model.Correo);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex) // Otros errores inesperados
                {
                    _logger.LogError(ex, "Error inesperado durante el registro para {Correo}", model.Correo);
                    ModelState.AddModelError(string.Empty, "Ocurri� un error inesperado durante el registro.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo de registro inv�lido para el correo: {Correo}", model.Correo);
                // Modelo inv�lido (ej. contrase�as no coinciden, campos requeridos faltantes)
            }

            // Si llegamos aqu�, hubo un error, volver a mostrar el formulario de registro con los errores
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