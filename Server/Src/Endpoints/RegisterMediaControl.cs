using Server.Input;

namespace Server.Endpoints;

public static class MediaControl
{
    /// <summary>
    /// HTTP transport for the media/navigation buttons. The UI drives these over the
    /// WebSocket command channel now (see RegisterInputSocket), because a per-press request
    /// meant re-deriving the AES token (PBKDF2, 100k iterations) before every button and
    /// waiting for a fresh round trip. These routes are kept for clients that predate the
    /// socket and stay authenticated per request by AuthMiddleware.
    ///
    /// Every route dispatches through <see cref="RemoteCommandDispatcher"/>, which is the
    /// same path the socket uses, so this list can never expose an action the socket does
    /// not (or vice versa).
    /// </summary>
    public static void RegisterMediaControlEndpoints(this WebApplication app)
    {
        MapCommand(app, "/play", RemoteCommand.Play);
        MapCommand(app, "/next", RemoteCommand.Next);
        MapCommand(app, "/previous", RemoteCommand.Previous);
        MapCommand(app, "/stop", RemoteCommand.Stop);
        MapCommand(app, "/space", RemoteCommand.Space);
        MapCommand(app, "/arrow-left", RemoteCommand.ArrowLeft);
        MapCommand(app, "/arrow-right", RemoteCommand.ArrowRight);
        MapCommand(app, "/arrow-up", RemoteCommand.ArrowUp);
        MapCommand(app, "/arrow-down", RemoteCommand.ArrowDown);
        MapCommand(app, "/backspace", RemoteCommand.Backspace);
        MapCommand(app, "/escape", RemoteCommand.Escape);
        MapCommand(app, "/f11", RemoteCommand.F11);
        MapCommand(app, "/volume-up", RemoteCommand.VolumeUp);
        MapCommand(app, "/volume-down", RemoteCommand.VolumeDown);
        MapCommand(app, "/volume-mute", RemoteCommand.VolumeMute);
    }

    private static void MapCommand(WebApplication app, string route, RemoteCommand command) =>
        app.MapPost(route, [CustomAuthorize] async (RemoteCommandDispatcher commands) =>
        {
            await commands.ExecuteAsync(command);
            return Results.Ok();
        });
}
