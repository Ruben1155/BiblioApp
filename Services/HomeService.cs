using BiblioApp.Models;
using Newtonsoft.Json; // Para JSON
using System.Net.Http; // Para HttpClient
using System.Net.Http.Headers; // Para MediaTypeWithQualityHeaderValue
using System.Text; // Para Encoding
using System.Threading.Tasks; // Para Task
using System.Collections.Generic; // Para IEnumerable, List
using Microsoft.Extensions.Configuration; // Para IConfiguration
using System; // Para Uri, Exception, ArgumentNullException
using Microsoft.Extensions.Logging; // Para ILogger (opcional pero recomendado)

namespace BiblioApp.Services
{
    // --- BiblioApp/Services/HomeService.cs ---
    // Servicio para la lógica del HomeController, principalmente login
    public class HomeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<HomeService> _logger; // Opcional: para logging

        // Inyectar HttpClientFactory, IConfiguration y ILogger
        public HomeService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<HomeService> logger)
        {
            _httpClient = httpClientFactory?.CreateClient("BiblioApiClient") // Usa el cliente nombrado
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _baseUrl = configuration?["ApiSettings:BaseUrl"]?.TrimEnd('/') // Asegurar que no termine con /
                 ?? throw new InvalidOperationException("API BaseUrl 'ApiSettings:BaseUrl' not configured in appsettings.json");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Método para validar usuario llamando a la API de forma SEGURA (POST con Body)
        public async Task<UsuarioModel?> ValidarUsuarioAsync(LoginViewModel credenciales)
        {
            // URL del endpoint de validación en la API (sin query parameters)
            var url = $"{_baseUrl}/usuario/validar";
            _logger.LogInformation("Llamando a API para validar usuario: {Url}", url);

            // Crear el objeto que se enviará en el cuerpo de la solicitud POST
            // Debe coincidir con el modelo 'LoginRequest' esperado por la API
            var loginRequestData = new
            {
                Correo = credenciales.Correo,
                Clave = credenciales.Clave
            };

            // Serializar el objeto a JSON
            var jsonContent = JsonConvert.SerializeObject(loginRequestData);
            // Crear el contenido HTTP con el JSON y el tipo de medio correcto
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                // Realizar la petición POST enviando el contenido JSON en el cuerpo
                _logger.LogDebug("Enviando solicitud POST a {Url} con cuerpo: {JsonBody}", url, jsonContent);
                var response = await _httpClient.PostAsync(url, httpContent);

                // Procesar la respuesta de la API
                if (response.IsSuccessStatusCode) // Status 200 OK (Login exitoso)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Validación exitosa desde API para correo: {Correo}. Respuesta: {ApiResponse}", credenciales.Correo, jsonResponse);
                    var usuario = JsonConvert.DeserializeObject<UsuarioModel>(jsonResponse);
                    return usuario;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) // Status 401 (Credenciales inválidas)
                {
                    _logger.LogWarning("Validación fallida desde API para correo: {Correo}. API devolvió Unauthorized (401).", credenciales.Correo);
                    return null; // Credenciales inválidas
                }
                else // Otros códigos de error de la API (400 Bad Request, 500 Internal Server Error, etc.)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error desde API al validar correo {Correo}. Status: {StatusCode}, Contenido: {ErrorContent}",
                                     credenciales.Correo, response.StatusCode, errorContent);
                    // Podrías intentar deserializar un objeto ProblemDetails si la API lo devuelve
                    return null; // Indicar error en la validación
                }
            }
            catch (HttpRequestException ex) // Errores de conexión, DNS, Timeout, etc.
            {
                _logger.LogError(ex, "Error de conexión con la API al validar correo {Correo}. URL: {Url}", credenciales.Correo, url);
                // Lanzar una excepción específica de la aplicación para que el controlador la maneje
                throw new ApplicationException("No se pudo conectar con el servicio de autenticación. Verifique que la API esté en ejecución y la URL sea correcta.", ex);
            }
            catch (JsonException ex) // Errores al deserializar la respuesta JSON
            {
                _logger.LogError(ex, "Error al deserializar la respuesta JSON de la API para correo {Correo}. URL: {Url}", credenciales.Correo, url);
                throw new ApplicationException("La respuesta del servicio de autenticación no tuvo el formato esperado.", ex);
            }
            catch (Exception ex) // Capturar cualquier otro error inesperado
            {
                _logger.LogError(ex, "Error inesperado en HomeService.ValidarUsuarioAsync para correo {Correo}. URL: {Url}", credenciales.Correo, url);
                throw; // Re-lanzar para manejo global de errores
            }
        }
    }
}
