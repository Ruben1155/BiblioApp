// Servicio para interactuar con los endpoints de Libro en la API
using BiblioApp.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text; // Para StringBuilder
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Web;
public class LibroService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<LibroService> _logger; // <--- CAMBIO: Declaración del logger

    // Constructor modificado para inyectar HttpClientFactory, IConfiguration y ILogger
    public LibroService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<LibroService> logger) // <--- CAMBIO: Añadido ILogger
    {
        _httpClient = httpClientFactory?.CreateClient("BiblioApiClient")
            ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _baseUrl = configuration?["ApiSettings:BaseUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("API BaseUrl 'ApiSettings:BaseUrl' not configured.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // <--- CAMBIO: Asignación del logger
    }

    // Obtener todos los libros, AHORA con filtros opcionales
    public async Task<IEnumerable<LibroModel>> GetAllLibrosAsync(string? tituloFilter = null, string? autorFilter = null)
    {
        // Construir la URL base
        var baseUrl = $"{_baseUrl}/libro"; // _baseUrl ya debería estar definido en la clase
        var queryParams = new List<string>();

        _logger.LogInformation("Obteniendo libros desde API con filtros: Titulo='{TituloFilter}', Autor='{AutorFilter}'",
            string.IsNullOrEmpty(tituloFilter) ? "N/A" : tituloFilter,
            string.IsNullOrEmpty(autorFilter) ? "N/A" : autorFilter);

        // Añadir parámetros de query si tienen valor (asegúrate de codificarlos para la URL)
        if (!string.IsNullOrWhiteSpace(tituloFilter))
        {
            queryParams.Add($"tituloFilter={HttpUtility.UrlEncode(tituloFilter)}");
        }
        if (!string.IsNullOrWhiteSpace(autorFilter))
        {
            queryParams.Add($"autorFilter={HttpUtility.UrlEncode(autorFilter)}");
        }

        // Construir la URL final con los query parameters si existen
        string requestUrl = baseUrl;
        if (queryParams.Any())
        {
            requestUrl += "?" + string.Join("&", queryParams);
        }

        _logger.LogDebug("URL final para obtener libros: {RequestUrl}", requestUrl);

        try
        {
            var response = await _httpClient.GetAsync(requestUrl); // Usar la URL con filtros
            response.EnsureSuccessStatusCode(); // Lanza excepción si no es 2xx
            var content = await response.Content.ReadAsStringAsync();
            var libros = JsonConvert.DeserializeObject<List<LibroModel>>(content);
            _logger.LogInformation("Se obtuvieron {Count} libros desde la API aplicando filtros.", libros?.Count ?? 0);
            return libros ?? new List<LibroModel>(); // Devolver lista vacía si es null
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de HttpRequest al obtener libros desde API: {Url}", requestUrl);
            // Considera devolver lista vacía o lanzar excepción
            return new List<LibroModel>();
        }
        catch (Exception ex) // Error de deserialización, etc.
        {
            _logger.LogError(ex, "Error inesperado en GetAllLibrosAsync. URL: {Url}", requestUrl);
            throw;
        }
    }


    // Obtener un libro por ID
    public async Task<LibroModel?> GetLibroByIdAsync(int id)
    {
        var url = $"{_baseUrl}/libro/{id}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // No encontrado
            }
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<LibroModel>(content);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error al obtener libro por ID ({id}) de API: {ex.Message}");
            return null; // O lanzar excepción
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inesperado en LibroService.GetLibroByIdAsync: {ex.Message}");
            throw;
        }
    }

    // Crear un libro nuevo
    public async Task<LibroModel?> CreateLibroAsync(LibroModel libro)
    {
        var url = $"{_baseUrl}/libro";
        var jsonContent = JsonConvert.SerializeObject(libro);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode(); // Espera 200 OK o 201 Created
            var createdJson = await response.Content.ReadAsStringAsync();
            // Asumimos que la API devuelve el objeto creado (puede que sin ID si el SP no lo devuelve)
            return JsonConvert.DeserializeObject<LibroModel>(createdJson);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error al crear libro en API: {ex.Message}");
            // Podrías intentar leer el cuerpo del error si la API devuelve detalles
            return null; // O lanzar excepción
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inesperado en LibroService.CreateLibroAsync: {ex.Message}");
            throw;
        }
    }

    // Actualizar un libro existente
    public async Task<bool> UpdateLibroAsync(int id, LibroModel libro)
    {
        var url = $"{_baseUrl}/libro/{id}";
        var jsonContent = JsonConvert.SerializeObject(libro);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PutAsync(url, httpContent);
            // EnsureSuccessStatusCode lanzaría excepción para 404 Not Found.
            // Podríamos manejar 404 explícitamente si queremos devolver false en ese caso.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false; // No encontrado
            }
            response.EnsureSuccessStatusCode(); // Espera 204 No Content o 200 OK
            return true; // Éxito
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error al actualizar libro ({id}) en API: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inesperado en LibroService.UpdateLibroAsync: {ex.Message}");
            throw;
        }
    }

    // Eliminar un libro
    public async Task<bool> DeleteLibroAsync(int id)
    {
        var url = $"{_baseUrl}/libro/{id}";
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false; // No encontrado
            }
            // Podría haber un 409 Conflict si no se puede borrar (ej. préstamos)
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                Console.WriteLine($"Conflicto al eliminar libro ({id}): Posiblemente tiene préstamos.");
                return false;
            }
            response.EnsureSuccessStatusCode(); // Espera 204 No Content o 200 OK
            return true;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error al eliminar libro ({id}) en API: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inesperado en LibroService.DeleteLibroAsync: {ex.Message}");
            throw;
        }
    }
}