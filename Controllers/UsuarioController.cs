using BiblioApp.Models; // Necesario para UsuarioModel, ErrorViewModel
using BiblioApp.Services; // Necesario para UsuarioService
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Necesario para ILogger
using System; // Necesario para Exception, ArgumentNullException
using System.Threading.Tasks; // Necesario para Task

namespace BiblioApp.Controllers
{
    // Controlador para gestionar las operaciones CRUD de Usuarios
    // [Authorize] // Descomentar si implementas autenticación y quieres proteger todo el controlador
    public class UsuarioController : AdminBaseController
    {
        private readonly UsuarioService _usuarioService;
        private readonly ILogger<UsuarioController> _logger;

        // Inyectar UsuarioService y ILogger a través del constructor
        public UsuarioController(UsuarioService usuarioService, ILogger<UsuarioController> logger)
        {
            _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /Usuario o /Usuario/Index
        // Muestra la lista de todos los usuarios
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Accediendo a la lista de usuarios (Usuario/Index).");
            try
            {
                var usuarios = await _usuarioService.GetAllUsuariosAsync();
                return View(usuarios); // Pasa la lista de usuarios a la vista
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de usuarios.");
                // Considera mostrar una vista de error o un mensaje en TempData
                TempData["ErrorMessage"] = "No se pudo cargar la lista de usuarios.";
                return View(new List<UsuarioModel>()); // Devolver vista con lista vacía en caso de error
            }
        }

        // GET: /Usuario/Details/5
        // Muestra los detalles de un usuario específico
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Solicitando detalles para Usuario ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Solicitud de detalles con ID de usuario inválido: {UsuarioId}", id);
                return BadRequest(); // ID inválido
            }

            try
            {
                var usuario = await _usuarioService.GetUsuarioByIdAsync(id);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado para ver detalles.", id);
                    return NotFound(); // Usuario no encontrado
                }
                return View(usuario); // Pasa el usuario encontrado a la vista
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles para Usuario ID: {UsuarioId}", id);
                TempData["ErrorMessage"] = "Error al cargar los detalles del usuario.";
                return RedirectToAction(nameof(Index)); // Redirigir a la lista si hay error
            }
        }

        // GET: /Usuario/Create
        // Muestra el formulario para crear un nuevo usuario
        public IActionResult Create()
        {
            _logger.LogInformation("Accediendo al formulario de creación de usuario.");
            // Devuelve la vista con un modelo vacío para el formulario
            return View(new UsuarioModel());
        }

        // POST: /Usuario/Create
        // Procesa los datos enviados desde el formulario de creación
        [HttpPost]
        [ValidateAntiForgeryToken] // Protección contra CSRF
        public async Task<IActionResult> Create(UsuarioModel usuario)
        {
            _logger.LogInformation("Intentando crear nuevo usuario con Correo: {Correo}", usuario?.Correo);
            // Quitar la validación de la Clave si el modelo la tuviera (no debería)
            // ModelState.Remove(nameof(UsuarioModel.Clave)); // Descomentar si el modelo tuviera Clave

            if (ModelState.IsValid)
            {
                try
                {
                    var usuarioCreado = await _usuarioService.CreateUsuarioAsync(usuario);
                    if (usuarioCreado != null)
                    {
                        _logger.LogInformation("Usuario creado exitosamente via API con Correo: {Correo}", usuario.Correo);
                        TempData["SuccessMessage"] = "Usuario creado correctamente.";
                        return RedirectToAction(nameof(Index)); // Redirigir a la lista
                    }
                    else
                    {
                        // El servicio devolvió null, indicando un error no esperado durante la llamada API
                        _logger.LogError("La llamada API para crear usuario (Correo: {Correo}) devolvió null.", usuario.Correo);
                        ModelState.AddModelError(string.Empty, "Error al crear el usuario en el servidor. Intente de nuevo.");
                    }
                }
                catch (InvalidOperationException ex) // Capturar conflicto (ej. correo duplicado)
                {
                    _logger.LogWarning(ex, "Conflicto al crear usuario con Correo: {Correo}", usuario.Correo);
                    // Mostrar el mensaje de error específico de la excepción (viene de la API/Servicio)
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex) // Capturar otros errores
                {
                    _logger.LogError(ex, "Error inesperado al crear usuario con Correo: {Correo}", usuario.Correo);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al crear el usuario.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para crear usuario inválido. Correo: {Correo}", usuario?.Correo);
            }

            // Si llegamos aquí, hubo un error, volver a mostrar el formulario con el modelo y los errores
            return View(usuario);
        }

        // GET: /Usuario/Edit/5
        // Muestra el formulario para editar un usuario existente
        public async Task<IActionResult> Edit(int id)
        {
            _logger.LogInformation("Accediendo al formulario de edición para Usuario ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Solicitud de edición con ID de usuario inválido: {UsuarioId}", id);
                return BadRequest();
            }

            try
            {
                var usuario = await _usuarioService.GetUsuarioByIdAsync(id);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado para editar.", id);
                    return NotFound();
                }
                return View(usuario); // Pasa el usuario encontrado a la vista de edición
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para editar Usuario ID: {UsuarioId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del usuario para editar.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Usuario/Edit/5
        // Procesa los datos enviados desde el formulario de edición
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UsuarioModel usuario)
        {
            _logger.LogInformation("Intentando actualizar Usuario ID: {UsuarioId}", id);
            if (id != usuario.Id)
            {
                _logger.LogWarning("ID de ruta ({RouteId}) no coincide con ID de modelo ({ModelId}) en edición.", id, usuario.Id);
                return BadRequest("Inconsistencia en el ID del usuario.");
            }

            // Quitar validación de Clave si existiera en el modelo
            // ModelState.Remove(nameof(UsuarioModel.Clave));

            if (ModelState.IsValid)
            {
                try
                {
                    // Llamar al servicio para actualizar (sin enviar clave)
                    var actualizado = await _usuarioService.UpdateUsuarioAsync(id, usuario);
                    if (actualizado)
                    {
                        _logger.LogInformation("Usuario ID: {UsuarioId} actualizado exitosamente.", id);
                        TempData["SuccessMessage"] = "Usuario actualizado correctamente.";
                        return RedirectToAction(nameof(Index)); // Redirigir a la lista
                    }
                    else
                    {
                        // El servicio devolvió false (posiblemente no encontrado por la API)
                        _logger.LogWarning("La llamada API para actualizar Usuario ID: {UsuarioId} devolvió false (posiblemente no encontrado).", id);
                        ModelState.AddModelError(string.Empty, "No se pudo actualizar el usuario. Es posible que ya no exista.");
                        // Podríamos re-verificar si existe aquí antes de mostrar el error
                    }
                }
                catch (InvalidOperationException ex) // Capturar conflicto (ej. correo duplicado)
                {
                    _logger.LogWarning(ex, "Conflicto al actualizar Usuario ID: {UsuarioId}", id);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex) // Capturar otros errores
                {
                    _logger.LogError(ex, "Error inesperado al actualizar Usuario ID: {UsuarioId}", id);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar el usuario.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para editar Usuario ID: {UsuarioId} inválido.", id);
            }

            // Si hay errores, volver a mostrar el formulario de edición
            return View(usuario);
        }

        // GET: /Usuario/Delete/5
        // Muestra la página de confirmación para eliminar un usuario
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Accediendo a la confirmación de eliminación para Usuario ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Solicitud de eliminación con ID de usuario inválido: {UsuarioId}", id);
                return BadRequest();
            }

            try
            {
                var usuario = await _usuarioService.GetUsuarioByIdAsync(id);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado para eliminar.", id);
                    return NotFound();
                }
                return View(usuario); // Pasa el usuario a la vista de confirmación
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para eliminar Usuario ID: {UsuarioId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del usuario para eliminar.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Usuario/Delete/5
        // Ejecuta la eliminación del usuario tras la confirmación
        [HttpPost, ActionName("Delete")] // Especifica que esta acción responde a la confirmación del GET Delete
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Confirmada eliminación para Usuario ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Confirmación de eliminación con ID de usuario inválido: {UsuarioId}", id);
                return BadRequest();
            }

            try
            {
                var eliminado = await _usuarioService.DeleteUsuarioAsync(id);
                if (eliminado)
                {
                    _logger.LogInformation("Usuario ID: {UsuarioId} eliminado exitosamente.", id);
                    TempData["SuccessMessage"] = "Usuario eliminado correctamente.";
                }
                else
                {
                    // El servicio devolvió false (no encontrado por la API)
                    _logger.LogWarning("La llamada API para eliminar Usuario ID: {UsuarioId} devolvió false (no encontrado).", id);
                    TempData["ErrorMessage"] = "No se pudo eliminar el usuario. Es posible que ya no exista.";
                }
            }
            catch (InvalidOperationException ex) // Capturar conflicto (ej. préstamos pendientes)
            {
                _logger.LogWarning(ex, "Conflicto al eliminar Usuario ID: {UsuarioId}", id);
                TempData["ErrorMessage"] = ex.Message; // Mostrar mensaje específico del error
            }
            catch (Exception ex) // Capturar otros errores
            {
                _logger.LogError(ex, "Error inesperado al eliminar Usuario ID: {UsuarioId}", id);
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar el usuario.";
            }

            return RedirectToAction(nameof(Index)); // Siempre redirigir a la lista
        }
    }
}
