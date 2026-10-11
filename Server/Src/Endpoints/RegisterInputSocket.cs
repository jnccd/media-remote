using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Server.Helpers;
using Server.Input;

namespace Server.Endpoints;

/// <summary>
/// Low-latency control channel for the UI. The UI opens a WebSocket to /inputws and must
/// authenticate with the same time-windowed token used by the HTTP endpoints (sent as the
/// first message). After that it can send mouse move/click/scroll, keystrokes, and the
/// whitelisted media/navigation commands, which are all replayed on the host machine.
/// WebSocket (not HTTP) is used because mouse movement needs to stream at high frequency, and
/// because the buttons otherwise had to re-derive the AES token for every single press.
///
/// Security model: the socket is authenticated once, up front, with the same token scheme as
/// every HTTP endpoint, and failed handshakes are throttled by the same
/// <see cref="AuthFailureThrottle"/> the HTTP endpoints use. What an authenticated socket may
/// do afterwards is limited to the message types handled in <see cref="HandleAsync"/> -
/// notably, commands are looked up in the closed <see cref="RemoteCommands"/> set, so the
/// socket offers no action the HTTP API did not already offer. Two bounds keep an
/// *unauthenticated* client from parking resources here: how long the auth frame may take,
/// and how large any single frame may be.
/// </summary>
public static class InputSocket
{
    /// <summary>
    /// How long an accepted socket may wait for its auth frame. The UI sends one as soon as
    /// the socket opens, but has to derive the token first - the slow part on a phone - so
    /// this is generous. It only has to be finite: without a deadline an unauthenticated
    /// client can hold sockets open indefinitely, which is the cheapest way to run this
    /// server out of connections.
    /// </summary>
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Largest frame accepted. Real frames are a few hundred bytes; the only variable-size
    /// case is text pasted into the input pad, which is still well under this. Without a cap
    /// the receive loop buffers whatever it is sent, for as long as it is sent.
    /// </summary>
    private const int MaxMessageBytes = 64 * 1024;

    /// <summary>Signals a frame above <see cref="MaxMessageBytes"/>; only thrown and caught here.</summary>
    private sealed class MessageTooLargeException : Exception { }

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
        string? Command = null,
        double? Dx = null,
        double? Dy = null,
        double? Delta = null);

    public static void RegisterInputSocketEndpoint(this WebApplication app)
    {
        app.MapGet("/inputws", async (HttpContext context, IInputSimulator simulator, RemoteCommandDispatcher commands, AuthFailureThrottle throttle, IConfiguration config) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var ct = context.RequestAborted;
            var expectedPassword = config["PASSWORD"] ?? "pass";
            var address = AuthFailureThrottle.AddressOf(context);

            // Refuse the upgrade while this address is in auth backoff, for the same reason the
            // HTTP path does: completing a handshake would answer the guess.
            var retryAfter = throttle.RetryAfter(address);
            if (retryAfter > TimeSpan.Zero)
            {
                context.Response.Headers["Retry-After"] = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            WebSocket? webSocket = null;

            try
            {
                webSocket = await context.WebSockets.AcceptWebSocketAsync();

                // First message must be an auth request within AuthTimeout; reject otherwise.
                var first = await ReceiveAuthFrameAsync(webSocket, ct);
                if (first is null)
                {
                    // Closed, or nothing arrived in time. Neither is a password guess, so it is
                    // not counted against the address - only real failures are.
                    await CloseQuietlyAsync(webSocket, WebSocketCloseStatus.PolicyViolation, "no auth");
                    return;
                }

                var auth = ParseMessage(first);
                if (auth?.Type != "auth" || !AuthMiddleware.IsAuthorized(auth.Authorization ?? string.Empty, expectedPassword))
                {
                    throttle.RecordFailure(address);
                    await SendJsonAsync(webSocket, new { type = "error", message = "unauthorized" }, ct);
                    await CloseQuietlyAsync(webSocket, WebSocketCloseStatus.PolicyViolation, "unauthorized");
                    return;
                }

                throttle.RecordSuccess(address);
                await SendJsonAsync(webSocket, new { type = "ready" }, ct);

                while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var raw = await ReceiveTextAsync(webSocket, ct);
                    if (raw is null)
                        break;

                    var msg = ParseMessage(raw);
                    if (msg is not null)
                        await HandleAsync(simulator, commands, webSocket, msg, ct);
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
            catch (MessageTooLargeException)
            {
                // Nothing the UI sends comes close to the cap, so this is a client that is not
                // the UI. It only costs the connection, and closing is cheaper than logging a
                // fault for it.
                if (webSocket is not null)
                    await CloseQuietlyAsync(webSocket, WebSocketCloseStatus.MessageTooBig, "message too large");
            }
            finally
            {
                webSocket?.Dispose();
            }
        });
    }

    /// <summary>
    /// The auth frame, waiting at most <see cref="AuthTimeout"/>. Null means the client closed
    /// or never sent one; the caller treats both as "no auth, no socket".
    /// </summary>
    private static async Task<string?> ReceiveAuthFrameAsync(WebSocket webSocket, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(AuthTimeout);
        try
        {
            return await ReceiveTextAsync(webSocket, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
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

    private static async Task HandleAsync(
        IInputSimulator sim,
        RemoteCommandDispatcher commands,
        WebSocket webSocket,
        InputMessage m,
        CancellationToken ct)
    {
        // Button presses. The name is resolved against the closed RemoteCommands set, so an
        // unknown/forged name is a no-op rather than reaching an input backend - the socket
        // accepts no action the HTTP API did not already accept. A failure is reported back to
        // the UI instead of being swallowed, for the same reason the HTTP endpoints never
        // swallowed injection errors: a button that silently does nothing is the bug where
        // "the button says it worked but nothing happens".
        if (m.Type == "command")
        {
            if (!RemoteCommands.TryParse(m.Command, out var command))
                return;

            try
            {
                await commands.ExecuteAsync(command);
            }
            catch (Exception ex) when (!IsClientDisconnect(ex))
            {
                await TrySendJsonAsync(webSocket, new { type = "commandError", command = m.Command, message = ex.Message }, ct);
            }
            return;
        }

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
            // Checked before writing, so a client streaming an endless frame cannot grow this
            // buffer past the cap.
            if (ms.Length + result.Count > MaxMessageBytes)
                throw new MessageTooLargeException();
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task SendJsonAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Best-effort notification. The client may already be gone by the time a command fails
    /// (tab closed, phone lost wifi), and there is nobody left to tell, so a failed send here
    /// must not turn into a logged fault.
    /// </summary>
    private static async Task TrySendJsonAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        try
        {
            await SendJsonAsync(ws, payload, ct);
        }
        catch (Exception ex) when (IsClientDisconnect(ex))
        {
        }
    }
}
