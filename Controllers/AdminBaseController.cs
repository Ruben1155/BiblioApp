using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters; // Necesario para IAsyncActionFilter o ActionFilterAttribute
using Microsoft.AspNetCore.Http; // Necesario para ISession
using System;

namespace BiblioApp.Controllers
{
    // Controlador base para acciones que requieren rol de Administrador
    public abstract class AdminBaseController : Controller
    {
        // Este método se ejecuta ANTES de cada acción en los controladores que hereden de él.
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Obtener el rol de la sesión
            string? userRole = context.HttpContext.Session.GetString("UserRole");

            // Verificar si el rol es "Administrador" (ignorando mayúsculas/minúsculas)
            if (userRole?.Equals("Administrador", StringComparison.OrdinalIgnoreCase) != true)
            {
                // Si no es administrador (o no hay sesión), redirigir
                // Puedes redirigir al Login o a una página específica de "Acceso Denegado"

                // Opción 1: Redirigir a Login
                // context.Result = RedirectToAction("Index", "Home");

                // Opción 2: Redirigir a una vista de Acceso Denegado
                context.Result = new ViewResult { ViewName = "AccessDenied" }; // Busca Views/Shared/AccessDenied.cshtml o Views/ControllerName/AccessDenied.cshtml

                // Opcional: Añadir un mensaje
                TempData["ErrorMessage"] = "Acceso denegado. Se requiere rol de Administrador.";

                // Importante: No llamar a await next() si rediriges, para detener la ejecución de la acción original.
                return;
            }

            // Si es administrador, continuar con la ejecución normal de la acción
            await next();
        }
    }
}
