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

            var ct = context.RequestAborted;
            var expectedPassword = config["PASSWORD"] ?? "pass";
            WebSocket? webSocket = null;

            try
            {
                webSocket = await context.WebSockets.AcceptWebSocketAsync();

                // First message must be an auth request; reject otherwise.
                var first = await ReceiveTextAsync(webSocket, ct);
                var auth = ParseMessage(first);
                if (auth?.Type != "auth" || !AuthMiddleware.IsAuthorized(auth.Authorization ?? string.Empty, expectedPassword))
                {
                    await SendJsonAsync(webSocket, new { type = "error", message = "unauthorized" }, ct);
                    await CloseQuietlyAsync(webSocket, WebSocketCloseStatus.PolicyViolation, "unauthorized");
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

                await CloseQuietlyAsync(webSocket, WebSocketCloseStatus.NormalClosure, "bye");
            }
            catch (Exception ex) when (IsClientDisconnect(ex))
            {
                // Swallowed on purpose: this is the UI tab closing, a phone losing
                // wifi, or the wrapper being killed, not a fault here. There is
                // nobody left to answer, and letting it escape would make the
                // request-logging middleware log a full stack trace for it - the
                // most common kind of log entry this endpoint produces.
            }
            finally
            {
                webSocket?.Dispose();
            }
        });
    }

    /// <summary>
    /// Whether an exception is the client going away rather than something wrong on
    /// our side. A dropped connection surfaces as a cancellation (the request token
    /// is aborted) and/or as a WebSocket/socket-level failure, a disposed socket, or
    /// an IO error, depending on how far the connection got.
    /// </summary>
    private static bool IsClientDisconnect(Exception ex) =>
        ex is OperationCanceledException // includes TaskCanceledException, what an aborted request throws
            or WebSocketException
            or System.Net.Sockets.SocketException
            or ObjectDisposedException
            or IOException;

    /// <summary>
    /// Closes the socket without turning a client that is already gone into an
    /// exception. CloseAsync waits for the peer's close frame, so a peer that
    /// vanished (closed tab, lost wifi, killed process) makes it throw
    /// TaskCanceledException/WebSocketException instead of returning; and by this
    /// point the request token is usually cancelled already, which is the other way
    /// the polite close turned into a logged stack trace. Hence the fresh, short
    /// timeout rather than the request token.
    /// </summary>
    private static async Task CloseQuietlyAsync(WebSocket webSocket, WebSocketCloseStatus status, string description)
    {
        if (webSocket.State != WebSocketState.Open && webSocket.State != WebSocketState.CloseReceived)
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await webSocket.CloseAsync(status, description, timeout.Token);
        }
        catch (Exception ex) when (IsClientDisconnect(ex))
        {
        }
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
