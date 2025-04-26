// --- BiblioApp/Program.cs ---

using BiblioApp.Services;
using System;
using System.Net.Http.Headers;
// --- A�adir using para HttpContextAccessor si no est� ---
using Microsoft.AspNetCore.Http;


var builder = WebApplication.CreateBuilder(args);

// 1. --- Configuraci�n de Servicios ---
builder.Services.AddControllersWithViews();

// Configurar HttpClientFactory
builder.Services.AddHttpClient("BiblioApiClient", client =>
{
    string? baseUrl = builder.Configuration["ApiSettings:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("API BaseUrl 'ApiSettings:BaseUrl' not configured in appsettings.json");
    }
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Registrar servicios de aplicaci�n
builder.Services.AddScoped<HomeService>();
builder.Services.AddScoped<LibroService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<PrestamoService>();

// Configuraci�n de Sesi�n
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".BiblioApp.Session";
});

// *** A�ADIR REGISTRO DE IHttpContextAccessor ***
// Permite acceder al HttpContext actual (y por lo tanto a la sesi�n)
// desde servicios o componentes donde no est� disponible directamente (como TagHelpers o vistas con @inject).
builder.Services.AddHttpContextAccessor();
// *******************************************

var app = builder.Build();

// 2. --- Configuraci�n del Pipeline de Peticiones HTTP ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middleware de Sesi�n (antes de Auth y Map)
app.UseSession();

// app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 3. --- Ejecutar la Aplicaci�n ---
app.Run();
