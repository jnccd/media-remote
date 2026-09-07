using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Server.Helpers;
using Server.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CustomAuthorizeAttribute : Attribute { }

public class AuthMiddleware(RequestDelegate next, IConfiguration config, LoggerService logger)
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

        string authHeaderVal;
        try
        {
            var parsedHeader = JsonConvert.DeserializeObject<CustomHttpHeader>(HttpHelpers.GetRequestBody(context.Request).Result);
            authHeaderVal = parsedHeader?.headers?.Authorization!;
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Missing or malformed Authorization header.");
            logger.WriteLine("Unauthorized: Missing or malformed Authorization header.");
            return;
        }

        if (!IsAuthorized(authHeaderVal, _expectedPassword))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid Authorization.");
            logger.WriteLine("Unauthorized: Invalid Authorization.");
            return;
        }

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
