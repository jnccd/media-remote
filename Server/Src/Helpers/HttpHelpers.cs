namespace Server.Helpers;

public static class HttpHelpers
{
    public static async Task<string> GetRequestBody(HttpRequest request)
    {
        if (!request.Body.CanSeek)
            request.EnableBuffering();
        request.Body.Position = 0;

        var rawRequestBody = await new StreamReader(request.Body).ReadToEndAsync();

        request.Body.Position = 0;

        return rawRequestBody;
    }
}