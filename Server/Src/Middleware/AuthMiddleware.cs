using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Server.Helpers;
using Server.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CustomAuthorizeAttribute : Attribute { }

public class AuthMiddleware(RequestDelegate next, IConfiguration config, LoggerService logger, AuthFailureThrottle throttle)
{
    private readonly RequestDelegate _next = next;
    private readonly string _expectedPassword = config["PASSWORD"] ?? "pass";

    record CustomHttpHeader(CustomHttpHeaders headers);
    record CustomHttpHeaders(string Authorization);

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.AccessControlAllowOrigin = "*";

        var endpoint = context.GetEndpoint();
        if (context.Request.Method == "OPTIONS" || endpoint?.Metadata.GetMetadata<CustomAuthorizeAttribute>() == null)
        {
            await _next(context);
            return;
        }

        var address = AuthFailureThrottle.AddressOf(context);

        // While this address is in backoff its token is not even looked at. Checking it would
        // still answer the guess, which is the whole thing the throttle exists to prevent.
        var retryAfter = throttle.RetryAfter(address);
        if (retryAfter > TimeSpan.Zero)
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            context.Response.Headers["Retry-After"] = seconds.ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync($"Unauthorized: Too many failed attempts, retry in {seconds}s.");
            logger.WriteLine($"Unauthorized: throttled (client {address}, retry in {seconds}s).");
            return;
        }

        string authHeaderVal;
        try
        {
            var parsedHeader = JsonConvert.DeserializeObject<CustomHttpHeader>(await HttpHelpers.GetRequestBody(context.Request));
            authHeaderVal = parsedHeader?.headers?.Authorization!;
        }
        catch
        {
            throttle.RecordFailure(address);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Missing or malformed Authorization header.");
            logger.WriteLine($"Unauthorized: Missing or malformed Authorization header (client {address}).");
            return;
        }

        if (!IsAuthorized(authHeaderVal, _expectedPassword))
        {
            throttle.RecordFailure(address);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid Authorization.");
            logger.WriteLine($"Unauthorized: Invalid Authorization (client {address}).");
            return;
        }

        throttle.RecordSuccess(address);
        await _next(context);
    }

    /// <summary>
    /// Validates an AES-GCM-encrypted ISO timestamp token against the expected password,
    /// requiring the timestamp to be within a small window around "now". Shared by the HTTP
    /// middleware and the input WebSocket so both use the same authenticated scheme.
    /// </summary>
    public static bool IsAuthorized(string token, string expectedPassword)
    {
        if (!TokenCrypto.TryDecrypt(expectedPassword, token, out var plaintext))
            return false;
        if (!DateTime.TryParse(plaintext, null, System.Globalization.DateTimeStyles.RoundtripKind, out var decryptedDateTime))
            return false;
        return decryptedDateTime >= DateTime.UtcNow.AddSeconds(-2) &&
               decryptedDateTime <= DateTime.UtcNow.AddSeconds(3);
    }
}
