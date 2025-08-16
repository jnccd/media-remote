using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Server.Helpers;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedPassword;

    public AuthMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _expectedPassword = config["PASSWORD"] ?? "very-secure-standard-password";
    }

    record CustomHttpHeader(CustomHttpHeaders headers);
    record CustomHttpHeaders(string Authorization);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "OPTIONS" || context.Request.Path.Value?.Contains("/swagger/") == true)
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
            await context.Response.WriteAsync("Unauthorized: Missing Authorization header.");
            return;
        }

        var authHeader = authHeaderVal.ToString();
        var unBase64dAuthHeader = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader));
        var decryptedHeader = XOR.XorCipher(unBase64dAuthHeader, _expectedPassword);
        var decryptedDateTimeString = decryptedHeader.Split(',')[1];
        if (!DateTime.TryParse(decryptedDateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var decryptedDateTime))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid Authorization.");
            return;
        }
        if (decryptedDateTime < DateTime.UtcNow.AddSeconds(-2) || decryptedDateTime > DateTime.UtcNow.AddSeconds(1))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Outdated Authorization.");
            return;
        }

        await _next(context);
    }
}