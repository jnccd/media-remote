using Microsoft.Extensions.FileProviders;
using Server.Helpers;
using Server.Services;

namespace Server.Middleware;

public static class RegisterMiddlewareExtensions
{
    public static void RegisterMiddleware(this WebApplication app)
    {
        app.AddRequestLoggingMiddleware();
        // Enable WebSockets so /inputws can carry low-latency mouse/keyboard input.
        app.UseWebSockets();
        app.UseMiddleware<AuthMiddleware>();
    }

    static void AddRequestLoggingMiddleware(this WebApplication app)
    {
        var logger = app.Services.GetService(typeof(LoggerService)) as LoggerService;
        app.Use(async (context, next) =>
        {
            try
            {
                logger?.WriteLine($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} - ORIGIN: {context.Request.Headers.Origin} - {{ {HttpHelpers.GetRequestBody(context.Request).Result} }}");
                await next.Invoke();
            }
            catch (Exception e)
            {
                logger?.WriteLine(e);
            }
        });
    }
}
