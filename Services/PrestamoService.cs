using BiblioApp.Models; // Necesitas PrestamoModel, UsuarioModel, LibroModel de BiblioApp
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace BiblioApp.Services
{
    // Servicio para interactuar con los endpoints de Préstamo en la API
    public class PrestamoService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<PrestamoService> _logger;

        public PrestamoService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PrestamoService> logger)
        {
            _httpClient = httpClientFactory?.CreateClient("BiblioApiClient")
                 ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _baseUrl = configuration?["ApiSettings:BaseUrl"]?.TrimEnd('/')
                 ?? throw new InvalidOperationException("API BaseUrl 'ApiSettings:BaseUrl' not configured.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Obtener todos los préstamos
        // Asume que la API devuelve una lista de PrestamoModel (posiblemente con datos de usuario/libro si el SP hace JOINs)
        public async Task<IEnumerable<PrestamoModel>> GetAllPrestamosAsync()
        {
            var url = $"{_baseUrl}/prestamo";
            _logger.LogInformation("Obteniendo todos los préstamos desde API: {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                // Deserializar la respuesta. Asegúrate que PrestamoModel en BiblioApp coincida con lo que devuelve la API.
                var prestamos = JsonConvert.DeserializeObject<List<PrestamoModel>>(content);
                _logger.LogInformation("Se obtuvieron {Count} préstamos desde la API.", prestamos?.Count ?? 0);
                return prestamos ?? new List<PrestamoModel>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al obtener préstamos desde API: {Url}", url);
                return new List<PrestamoModel>(); // Devolver lista vacía en caso de error de conexión
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en GetAllPrestamosAsync. URL: {Url}", url);
                throw;
            }
        }

        // Obtener un préstamo por ID (Si la API tuviera este endpoint)
        // Nota: El controlador API actual no tiene un endpoint GetPrestamoById.
        // Si lo necesitaras, tendrías que añadirlo a la API primero.
        /*
        public async Task<PrestamoModel?> GetPrestamoByIdAsync(int id)
        {
            var url = $"{_baseUrl}/prestamo/{id}";
            _logger.LogInformation("Obteniendo préstamo por ID desde API: {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Préstamo ID: {PrestamoId} no encontrado en API (404). URL: {Url}", id, url);
                    return null;
                }
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var prestamo = JsonConvert.DeserializeObject<PrestamoModel>(content);
                _logger.LogInformation("Préstamo ID: {PrestamoId} obtenido exitosamente desde API.", id);
                return prestamo;
            }
            catch (HttpRequestException ex)
            {
                 _logger.LogError(ex, "Error de HttpRequest al obtener préstamo ID {PrestamoId} desde API: {Url}", id, url);
                 return null;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error inesperado en GetPrestamoByIdAsync para ID {PrestamoId}. URL: {Url}", id, url);
                 throw;
            }
        }
        */

        // Registrar un nuevo préstamo
        public async Task<PrestamoModel?> CreatePrestamoAsync(PrestamoModel prestamo)
        {
            var url = $"{_baseUrl}/prestamo";
            _logger.LogInformation("Intentando registrar préstamo via API: {Url}, UsuarioID: {UsuarioId}, LibroID: {LibroId}", url, prestamo.IdUsuario, prestamo.IdLibro);

            // Crear el objeto a enviar (puede ser el mismo modelo si no hay datos sensibles)
            var prestamoData = new
            {
                prestamo.IdUsuario,
                prestamo.IdLibro,
                prestamo.FechaDevolucionEsperada,
                prestamo.Estado // El estado inicial podría definirse aquí o en la API/SP
            };

            var jsonContent = JsonConvert.SerializeObject(prestamoData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, httpContent);

                // Manejar errores específicos devueltos por la API (ej. 400 Bad Request si no hay stock)
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var problemDetails = JsonConvert.DeserializeObject<ProblemDetails>(errorContent); // Intenta leer ProblemDetails
                    var errorMessage = problemDetails?.Detail ?? errorContent; // Usa el detalle o el contenido crudo

                    _logger.LogWarning("Error al registrar préstamo via API ({StatusCode}): {ErrorMessage}. URL: {Url}", response.StatusCode, errorMessage, url);
                    // Lanzar excepción para que el controlador la maneje y muestre el error
                    throw new InvalidOperationException($"Error al registrar préstamo: {errorMessage}");
                }

                // Si fue exitoso (200 OK o 201 Created)
                var createdJson = await response.Content.ReadAsStringAsync();
                // Asumimos que la API devuelve el objeto creado (puede que sin ID si el SP no lo devuelve)
                var prestamoCreado = JsonConvert.DeserializeObject<PrestamoModel>(createdJson);
                _logger.LogInformation("Préstamo registrado exitosamente via API para UsuarioID: {UsuarioId}, LibroID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                return prestamoCreado;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al registrar préstamo (UsuarioID: {UsuarioId}, LibroID: {LibroId}) via API: {Url}", prestamo.IdUsuario, prestamo.IdLibro, url);
                // Lanzar excepción para indicar fallo de conexión
                throw new ApplicationException("No se pudo conectar con la API para registrar el préstamo.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar conflicto/error de negocio
            {
                throw;
            }
            catch (Exception ex) // Otros errores (ej. deserialización)
            {
                _logger.LogError(ex, "Error inesperado en CreatePrestamoAsync (UsuarioID: {UsuarioId}, LibroID: {LibroId}). URL: {Url}", prestamo.IdUsuario, prestamo.IdLibro, url);
                throw;
            }
        }

        // Actualizar un préstamo existente (ej. registrar devolución)
        public async Task<bool> UpdatePrestamoAsync(int id, PrestamoModel prestamo)
        {
            var url = $"{_baseUrl}/prestamo/{id}";
            _logger.LogInformation("Intentando actualizar préstamo ID: {PrestamoId} via API: {Url}", id, url);

            // Crear objeto con los datos a actualizar
            var prestamoData = new
            {
                prestamo.Id, // Incluir ID para validación
                prestamo.IdUsuario,
                prestamo.IdLibro,
                prestamo.FechaPrestamo, // Puede que no sea necesario enviar esto
                prestamo.FechaDevolucionEsperada, // Puede que no sea necesario enviar esto
                prestamo.FechaDevolucionReal, // Fecha de devolución real
                prestamo.Estado // Nuevo estado (ej. 'Devuelto')
            };

            var jsonContent = JsonConvert.SerializeObject(prestamoData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PutAsync(url, httpContent);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Préstamo ID: {PrestamoId} no encontrado en API para actualizar (404). URL: {Url}", id, url);
                    return false; // No encontrado
                }

                if (!response.IsSuccessStatusCode) // Otros errores (ej. 400 Bad Request por estado inválido)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var problemDetails = JsonConvert.DeserializeObject<ProblemDetails>(errorContent);
                    var errorMessage = problemDetails?.Detail ?? errorContent;
                    _logger.LogWarning("Error al actualizar préstamo ID: {PrestamoId} via API ({StatusCode}): {ErrorMessage}. URL: {Url}", id, response.StatusCode, errorMessage, url);
                    // Lanzar excepción para que el controlador la maneje
                    throw new InvalidOperationException($"Error al actualizar préstamo: {errorMessage}");
                }

                // Éxito (normalmente 204 No Content)
                _logger.LogInformation("Préstamo ID: {PrestamoId} actualizado exitosamente via API.", id);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al actualizar préstamo ID: {PrestamoId} via API: {Url}", id, url);
                throw new ApplicationException("No se pudo conectar con la API para actualizar el préstamo.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar error de negocio
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en UpdatePrestamoAsync para ID: {PrestamoId}. URL: {Url}", id, url);
                throw;
            }
        }

        // Eliminar un préstamo
        public async Task<bool> DeletePrestamoAsync(int id)
        {
            var url = $"{_baseUrl}/prestamo/{id}";
            _logger.LogInformation("Intentando eliminar préstamo ID: {PrestamoId} via API: {Url}", id, url);
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Préstamo ID: {PrestamoId} no encontrado en API para eliminar (404). URL: {Url}", id, url);
                    return false; // No encontrado
                }

                if (!response.IsSuccessStatusCode) // Otros errores
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var problemDetails = JsonConvert.DeserializeObject<ProblemDetails>(errorContent);
                    var errorMessage = problemDetails?.Detail ?? errorContent;
                    _logger.LogWarning("Error al eliminar préstamo ID: {PrestamoId} via API ({StatusCode}): {ErrorMessage}. URL: {Url}", id, response.StatusCode, errorMessage, url);
                    // Lanzar excepción
                    throw new InvalidOperationException($"Error al eliminar préstamo: {errorMessage}");
                }

                // Éxito (normalmente 204 No Content)
                _logger.LogInformation("Préstamo ID: {PrestamoId} eliminado exitosamente via API.", id);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de HttpRequest al eliminar préstamo ID: {PrestamoId} via API: {Url}", id, url);
                throw new ApplicationException("No se pudo conectar con la API para eliminar el préstamo.", ex);
            }
            catch (InvalidOperationException) // Re-lanzar error de negocio
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en DeletePrestamoAsync para ID: {PrestamoId}. URL: {Url}", id, url);
                throw;
            }
        }

        // --- Métodos auxiliares (Opcional) ---
        // Podrías añadir métodos aquí para obtener listas de Usuarios y Libros
        // que se usarían para llenar DropDownLists en los formularios de Préstamo.
        // Estos llamarían a los endpoints correspondientes en UsuarioController y LibroController de la API.
        /*
        public async Task<IEnumerable<UsuarioModel>> GetUsuariosForDropdownAsync() { ... }
        public async Task<IEnumerable<LibroModel>> GetLibrosDisponiblesForDropdownAsync() { ... }
        */
    }
}

 
