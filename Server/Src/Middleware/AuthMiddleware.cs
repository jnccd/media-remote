using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Server.Helpers;
using Server.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

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
        var endpoint = context.GetEndpoint();
        if (context.Request.Method == "OPTIONS" || endpoint?.Metadata.GetMetadata<CustomAuthorizeAttribute>() == null)
        {
            await _next(context);
            return;
        }

        string authHeaderVal, decryptedDateTimeString;
        try
        {
            var parsedHeader = JsonConvert.DeserializeObject<CustomHttpHeader>(HttpHelpers.GetRequestBody(context.Request).Result);
            authHeaderVal = parsedHeader?.headers?.Authorization!;

            var authHeader = authHeaderVal.ToString();
            var unBase64dAuthHeader = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader));
            var decryptedHeader = XOR.XorCipher(unBase64dAuthHeader, _expectedPassword);
            decryptedDateTimeString = decryptedHeader.Split(',')[1];
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Missing or malformed Authorization header.");
            logger.WriteLine("Unauthorized: Missing or malformed Authorization header.");
            return;
        }
        if (!DateTime.TryParse(decryptedDateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var decryptedDateTime))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid Authorization.");
            logger.WriteLine("Unauthorized: Invalid Authorization.");
            return;
        }
        if (decryptedDateTime < DateTime.UtcNow.AddSeconds(-2) || decryptedDateTime > DateTime.UtcNow.AddSeconds(3))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Outdated Authorization.");
            logger.WriteLine($"Unauthorized: Outdated Authorization. by {decryptedDateTime - DateTime.UtcNow}");
            return;
        }

        await _next(context);
    }
}