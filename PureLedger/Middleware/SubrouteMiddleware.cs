using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PureLedger.Middleware
{
    public class SubrouteMiddleware
    {
        private readonly RequestDelegate _next;

        public SubrouteMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
            string subroute = "Default";

            if (path.StartsWith("/route1/"))
            {
                subroute = "Subroute1";
            }
            else if (path.StartsWith("/route2/"))
            {
                subroute = "Subroute2";
            }

            // Always set the subroute for this request only
            context.Items["Subroute"] = subroute;

            await _next(context);
        }
    }

    public static class SubrouteMiddlewareExtensions
    {
        public static IApplicationBuilder UseSubrouteMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SubrouteMiddleware>();
        }
    }
}