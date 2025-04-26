using BiblioApp.Models; // Necesitas UsuarioModel, etc.
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging; // Para Logging
using System;

namespace BiblioApp.Services
{
    // Servicio para interactuar con los endpoints de Usuario en la API
    public class UsuarioService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<UsuarioService> _logger;

        public UsuarioService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<UsuarioService> logger)
        {
            _httpClient = httpClientFactory?.CreateClient("BiblioApiClient")
                 ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _baseUrl = configuration?["ApiSettings:BaseUrl"]?.TrimEnd('/')
                 ?? throw new InvalidOperationException("API BaseUrl 'ApiSettings:BaseUrl' not configured.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Obtener todos los usuarios
        public async Task<IEnumerable<UsuarioModel>> GetAllUsuariosAsync()
        {
            var url = $"{_baseUrl}/usuario";
            _logger.LogInformation("Obteniendo todos los usuarios desde API: {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Lanza excepción si no es 2xx
                var content = await response.Content.ReadAsStringAsync();
                var usuarios = JsonConvert.DeserializeObject<List<UsuarioModel>>(content);
                _logger.LogInformation("Se obtuvieron {Count} usuarios desde la API.", usuarios?.Count ?? 0);
                return usuarios ?? new List<UsuarioModel>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al obtener usuarios desde API: {Url}", url);
                return new List<UsuarioModel>();
            }
            catch (Exception ex) // Otros errores (ej. deserialización)
            {
                _logger.LogError(ex, "Error inesperado en GetAllUsuariosAsync. URL: {Url}", url);
                throw;
            }
        }

        // Obtener un usuario por ID
        public async Task<UsuarioModel?> GetUsuarioByIdAsync(int id)
        {
            var url = $"{_baseUrl}/usuario/{id}";
            _logger.LogInformation("Obteniendo usuario por ID desde API: {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado en API (404). URL: {Url}", id, url);
                    return null; // No encontrado
                }
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                // Deserializar al modelo de BiblioApp (sin Clave)
                var usuario = JsonConvert.DeserializeObject<UsuarioModel>(content);
                _logger.LogInformation("Usuario ID: {UsuarioId} obtenido exitosamente desde API.", id);
                return usuario;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al obtener usuario ID {UsuarioId} desde API: {Url}", id, url);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en GetUsuarioByIdAsync para ID {UsuarioId}. URL: {Url}", id, url);
                throw;
            }
        }

        // Crear un usuario nuevo.
        // Envía el objeto UsuarioModel completo (que PUEDE incluir Clave si viene del registro) a la API.
        // La API decidirá si usar la clave proporcionada o generar una por defecto.
        public async Task<UsuarioModel?> CreateUsuarioAsync(UsuarioModel usuario)
        {
            var url = $"{_baseUrl}/usuario";
            // El log ya no intenta acceder a usuario.Clave directamente
            _logger.LogInformation("Intentando crear usuario via API: {Url}, Correo: {Correo}", url, usuario.Correo);

            // Serializar el objeto UsuarioModel COMPLETO que se recibe
            // Si viene del registro, incluirá la 'Clave' temporalmente para enviarla.
            // Si viene de la gestión de admin, 'Clave' será null o vacía.
            var jsonContent = JsonConvert.SerializeObject(usuario);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, httpContent);

                // Manejar error de Conflicto (correo duplicado)
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Conflicto al crear usuario (Correo: {Correo}) via API: {StatusCode} - {ErrorResponse}", usuario.Correo, response.StatusCode, error);
                    throw new InvalidOperationException($"Conflicto al crear usuario: {error}");
                }

                // Manejar otros errores específicos si es necesario (ej. 400 Bad Request por validación)
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ProblemDetails? problemDetails = null;
                    try { problemDetails = JsonConvert.DeserializeObject<ProblemDetails>(errorContent); } catch { /* Ignorar si no es JSON válido */ }
                    var errorMessage = problemDetails?.Detail ?? errorContent;
                    _logger.LogWarning("Error al crear usuario (Correo: {Correo}) via API ({StatusCode}): {ErrorMessage}. URL: {Url}", usuario.Correo, response.StatusCode, errorMessage, url);
                    throw new InvalidOperationException($"Error al crear usuario: {errorMessage}");
                }

                // Éxito (200 OK o 201 Created)
                var createdJson = await response.Content.ReadAsStringAsync();
                // Deserializar al modelo de BiblioApp (sin Clave)
                var usuarioCreado = JsonConvert.DeserializeObject<UsuarioModel>(createdJson);
                // La línea para limpiar la clave se eliminó porque usuarioCreado (UsuarioModel de BiblioApp) no tiene Clave.
                _logger.LogInformation("Usuario creado exitosamente via API para Correo: {Correo}", usuario.Correo);
                return usuarioCreado;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al crear usuario (Correo: {Correo}) via API: {Url}", usuario.Correo, url);
                throw new ApplicationException("No se pudo conectar con la API para crear el usuario.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar conflicto u otros errores de negocio
            {
                throw;
            }
            catch (Exception ex) // Otros errores (ej. deserialización)
            {
                _logger.LogError(ex, "Error inesperado en CreateUsuarioAsync (Correo: {Correo}). URL: {Url}", usuario.Correo, url);
                throw;
            }
        }

        // Actualizar un usuario existente
        // Este método sigue sin enviar la clave, lo cual es correcto para la edición de perfil.
        public async Task<bool> UpdateUsuarioAsync(int id, UsuarioModel usuario)
        {
            var url = $"{_baseUrl}/usuario/{id}";
            _logger.LogInformation("Intentando actualizar usuario ID: {UsuarioId} via API: {Url}", id, url);

            // Crear objeto sin Clave para la actualización estándar
            var usuarioData = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.Correo,
                usuario.Telefono,
                usuario.TipoUsuario
            };

            var jsonContent = JsonConvert.SerializeObject(usuarioData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PutAsync(url, httpContent);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado en API para actualizar (404). URL: {Url}", id, url);
                    return false;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict) // 409 Conflict (ej. correo duplicado)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Conflicto al actualizar usuario ID: {UsuarioId} via API: {StatusCode} - {ErrorResponse}", id, response.StatusCode, error);
                    throw new InvalidOperationException($"Conflicto al actualizar usuario: {error}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NotModified) // 304 Not Modified
                {
                    _logger.LogInformation("Usuario ID: {UsuarioId} no modificado (304) via API.", id);
                    return false; // Indicar que no hubo cambios
                }
                response.EnsureSuccessStatusCode(); // Espera 204 No Content o 200 OK
                _logger.LogInformation("Usuario ID: {UsuarioId} actualizado exitosamente via API.", id);
                return true; // Éxito
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al actualizar usuario ID: {UsuarioId} via API: {Url}", id, url);
                throw new ApplicationException("No se pudo conectar con la API para actualizar el usuario.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar conflicto
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en UpdateUsuarioAsync para ID: {UsuarioId}. URL: {Url}", id, url);
                throw;
            }
        }

        // Eliminar un usuario
        public async Task<bool> DeleteUsuarioAsync(int id)
        {
            var url = $"{_baseUrl}/usuario/{id}";
            _logger.LogInformation("Intentando eliminar usuario ID: {UsuarioId} via API: {Url}", id, url);
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Usuario ID: {UsuarioId} no encontrado en API para eliminar (404). URL: {Url}", id, url);
                    return false;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict) // 409 Conflict (ej. préstamos pendientes)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Conflicto al eliminar usuario ID: {UsuarioId} via API: {StatusCode} - {ErrorResponse}", id, response.StatusCode, error);
                    throw new InvalidOperationException($"Conflicto al eliminar usuario: {error}");
                }
                response.EnsureSuccessStatusCode(); // Espera 204 No Content o 200 OK
                _logger.LogInformation("Usuario ID: {UsuarioId} eliminado exitosamente via API.", id);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al eliminar usuario ID: {UsuarioId} via API: {Url}", id, url);
                throw new ApplicationException("No se pudo conectar con la API para eliminar el usuario.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar conflicto
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en DeleteUsuarioAsync para ID: {UsuarioId}. URL: {Url}", id, url);
                throw;
            }
        }
    } // Fin clase UsuarioService

    // Clase auxiliar interna para deserializar ProblemDetails (si no la tienes ya definida)
    internal class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
    }
}