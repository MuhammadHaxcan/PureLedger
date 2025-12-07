//using System.Reflection;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Mvc.Controllers;
//using PureLedger.Attributes;
//using PureLedger.Services.Interface;

//namespace PureLedger.Middleware
//{
//    public class AuthorizationMiddleware
//    {
//        private readonly RequestDelegate _next;
//        private readonly ILogger<AuthorizationMiddleware> _logger;

//        private static readonly HashSet<string> StaticPrefixes = new(StringComparer.OrdinalIgnoreCase)
//        {
//            "/lib/", "/js/", "/css/", "/images/", "/fonts/", "/favicon.ico", "/uploads/"
//        };

//        public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
//        {
//            _next = next;
//            _logger = logger;
//        }

//        public async Task InvokeAsync(HttpContext context, IAuthService authService)
//        {
//            // Static and OPTIONS
//            if (HttpMethods.IsOptions(context.Request.Method) || IsStatic(context.Request.Path.Value))
//            {
//                await _next(context);
//                return;
//            }

//            // Allow Account controller always (login/logout/etc.)
//            var routeController = context.Request.RouteValues["controller"]?.ToString();
//            if (string.Equals(routeController, "Account", StringComparison.OrdinalIgnoreCase))
//            {
//                await _next(context);
//                return;
//            }

//            // Resolve controller/action via endpoint metadata
//            var cad = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
//            if (cad == null)
//            {
//                // Not an MVC action
//                await _next(context);
//                return;
//            }

//            var controller = cad.ControllerName;
//            var action = cad.ActionName;

//            // Must be authenticated for MVC actions
//            if (context.User.Identity?.IsAuthenticated != true)
//            {
//                var returnUrl = context.Request.Path + context.Request.QueryString;
//                context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
//                return;
//            }

//            // Admin bypass
//            if (context.User.IsInRole("Admin"))
//            {
//                await _next(context);
//                return;
//            }

//            // Resolve user id
//            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
//            if (!int.TryParse(userIdStr, out var userId))
//            {
//                await Deny(context);
//                return;
//            }

//            // Utility endpoints are marked via attribute
//            var isUtility = cad.MethodInfo.GetCustomAttribute<UtilityEndpointAttribute>() is not null;

//            if (!isUtility)
//            {
//                // Regular actions: need controller or action access
//                if (await HasControllerOrActionAccess(authService, userId, controller, action))
//                {
//                    await _next(context);
//                    return;
//                }

//                _logger.LogWarning("Access denied: User {UserId} -> {Controller}/{Action}", userId, controller, action);
//                await Deny(context);
//                return;
//            }

//            // Utility actions: allow if user has controller access OR any action in this controller
//            if (await authService.HasControllerAccessAsync(userId, controller))
//            {
//                await _next(context);
//                return;
//            }

//            // Direct utility action access also allowed
//            if (await authService.HasActionAccessAsync(userId, controller, action))
//            {
//                await _next(context);
//                return;
//            }

//            // Check if user has ANY action access in this controller (modified logic)
//            var userActions = await authService.GetUserActionAccessAsync(userId);
//            if (userActions != null)
//            {
//                var ctrlKey = userActions.Keys.FirstOrDefault(k => string.Equals(k, controller, StringComparison.OrdinalIgnoreCase));
//                if (ctrlKey is not null && userActions[ctrlKey]?.Count > 0)
//                {
//                    // User has at least one action in this controller - allow utility endpoint
//                    await _next(context);
//                    return;
//                }
//            }

//            _logger.LogWarning("Utility access denied: User {UserId} -> {Controller}/{Action}", userId, controller, action);
//            await Deny(context);
//        }

//        private static bool IsStatic(string? path)
//        {
//            if (string.IsNullOrEmpty(path)) return false;
//            foreach (var p in StaticPrefixes)
//            {
//                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
//                    return true;
//            }
//            return false;
//        }

//        private static async Task<bool> HasControllerOrActionAccess(IAuthService authService, int userId, string controller, string action)
//        {
//            if (await authService.HasActionAccessAsync(userId, controller, action)) return true;
//            if (await authService.HasControllerAccessAsync(userId, controller)) return true;
//            return false;
//        }

//        private static async Task Deny(HttpContext context)
//        {
//            if (string.Equals(context.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
//            {
//                context.Response.StatusCode = StatusCodes.Status403Forbidden;
//                await context.Response.WriteAsJsonAsync(new { error = "Access denied" });
//            }
//            else
//            {
//                context.Response.Redirect("/Account/AccessDenied");
//            }
//        }
//    }

//    public static class AuthorizationMiddlewareExtensions
//    {
//        public static IApplicationBuilder UseAuthorizationMiddleware(this IApplicationBuilder builder)
//        {
//            return builder.UseMiddleware<AuthorizationMiddleware>();
//        }
//    }
//}