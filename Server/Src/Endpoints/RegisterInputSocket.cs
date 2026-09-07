using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Server.Input;

namespace Server.Endpoints;

/// <summary>
/// Low-latency input channel for general mouse/keyboard control. The UI opens a WebSocket to
/// /inputws and must authenticate with the same time-windowed token used by the HTTP endpoints
/// (sent as the first message). After that it can send mouse move/click/scroll and keystrokes,
/// which are all replayed on the host machine. WebSocket (not HTTP) is used because mouse
/// movement needs to stream at high frequency.
/// </summary>
public static class InputSocket
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // All properties are optional (every message only carries the fields it needs), so give the
    // record default values; otherwise System.Text.Json requires every constructor parameter in
    // the payload and throws JsonException for partial messages like {"type":"mousemove","dx":..}.
    private record InputMessage(
        string? Type = null,
        string? Authorization = null,
        string? Text = null,
        string? Key = null,
        string? Button = null,
        double? Dx = null,
        double? Dy = null,
        double? Delta = null);

    public static void RegisterInputSocketEndpoint(this WebApplication app)
    {
        app.MapGet("/inputws", async (HttpContext context, IInputSimulator simulator, IConfiguration config) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var ct = context.RequestAborted;
            var expectedPassword = config["PASSWORD"] ?? "pass";

            // First message must be an auth request; reject otherwise.
            var first = await ReceiveTextAsync(webSocket, ct);
            var auth = ParseMessage(first);
            if (auth?.Type != "auth" || !AuthMiddleware.IsAuthorized(auth.Authorization ?? string.Empty, expectedPassword))
            {
                await SendJsonAsync(webSocket, new { type = "error", message = "unauthorized" }, ct);
                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unauthorized", ct);
                return;
            }
            await SendJsonAsync(webSocket, new { type = "ready" }, ct);

            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var raw = await ReceiveTextAsync(webSocket, ct);
                if (raw is null)
                    break;

                var msg = ParseMessage(raw);
                if (msg is not null)
                    await HandleAsync(simulator, msg);
            }
        });
    }

    private static async Task HandleAsync(IInputSimulator sim, InputMessage m)
    {
        try
        {
            switch (m.Type)
            {
                case "mousemove":
                    if (m.Dx is not null && m.Dy is not null)
                        await sim.MoveMouseAsync((int)Math.Round(m.Dx.Value), (int)Math.Round(m.Dy.Value));
                    break;
                case "click":
                    if (m.Button is not null && Enum.TryParse<MouseButton>(m.Button, true, out var button))
                        await sim.ClickAsync(button);
                    break;
                case "scroll":
                    if (m.Delta is not null)
                        await sim.ScrollAsync((int)Math.Round(m.Delta.Value));
                    break;
                case "key":
                    if (m.Key is not null && Enum.TryParse<SimKey>(m.Key, true, out var key))
                        await sim.PressAsync(key);
                    break;
                case "text":
                    if (!string.IsNullOrEmpty(m.Text))
                        await sim.TypeTextAsync(m.Text);
                    break;
            }
        }
        catch
        {
            // Ignore malformed/unsupported messages rather than tearing down the socket.
        }
    }

    private static InputMessage? ParseMessage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<InputMessage>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task SendJsonAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}
