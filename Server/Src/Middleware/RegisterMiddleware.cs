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
                logger?.WriteLine($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} - ORIGIN: {context.Request.Headers.Origin} - {await DescribeBodyAsync(context)}");
                await next.Invoke();
            }
            catch (Exception e)
            {
                // A client that goes away mid-request - the UI navigating off, a
                // phone losing wifi, an app being killed - aborts the request, which
                // surfaces here as a cancellation. Nothing failed on our side and
                // there is nobody left to answer, so this is not worth a stack
                // trace. (The websocket endpoint already handles its own case; this
                // covers every ordinary request that gets abandoned.)
                if (e is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
                    return;

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

    /// <summary>
    /// The body, for the log line.
    ///
    /// On a Release build an authenticated endpoint's body is nothing but the auth token, and
    /// that is not written out: there is no reason to put a credential in log.txt, where it
    /// outlives its usefulness and follows the log into every bug report or chat paste. (It is
    /// only replayable inside its ~5s validity window, so this is hygiene rather than a live
    /// hole - the path already says which button was pressed, and the body says nothing more.)
    ///
    /// Debug builds are the deliberate exception: they are for testing, with a throwaway
    /// password, and are not the build that runs for days, so the raw body is the more useful
    /// thing to have. That rests on how the builds are used rather than on anything the code
    /// enforces - a Debug build run with the real password is the case this comment is
    /// warning about.
    /// </summary>
    static async Task<string> DescribeBodyAsync(HttpContext context)
    {
#if !DEBUG
        if (context.GetEndpoint()?.Metadata.GetMetadata<CustomAuthorizeAttribute>() is not null)
            return "{ <authorization token redacted> }";
#endif

        return $"{{ {await HttpHelpers.GetRequestBody(context.Request)} }}";
    }
}
