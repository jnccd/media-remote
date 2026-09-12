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
                // Log it, but do NOT swallow it. Swallowing here turned every
                // failed input injection into an empty HTTP 200, which is how
                // "the button says it worked but nothing happens" happened: the
                // endpoint threw (e.g. ydotool missing), this catch logged the
                // stack trace, and the client still saw 200 OK.
                logger?.WriteLine(e);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = e.Message,
                        type = e.GetType().Name,
                    });
                }
            }
        });
    }
}
