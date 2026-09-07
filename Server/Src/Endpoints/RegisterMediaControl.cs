using Server.Input;

namespace Server.Endpoints;

public static class MediaControl
{
    public static void RegisterMediaControlEndpoints(this WebApplication app)
    {
        // Media transport is routed through IMediaController (MPRIS on Linux,
        // simulated media keys on Windows).
        app.MapPost("/play", [CustomAuthorize] async (IMediaController media) =>
        {
            await media.PlayPauseAsync();
            return Results.Ok();
        });

        app.MapPost("/next", [CustomAuthorize] async (IMediaController media) =>
        {
            await media.NextAsync();
            return Results.Ok();
        });

        app.MapPost("/previous", [CustomAuthorize] async (IMediaController media) =>
        {
            await media.PreviousAsync();
            return Results.Ok();
        });

        app.MapPost("/stop", [CustomAuthorize] async (IMediaController media) =>
        {
            await media.StopAsync();
            return Results.Ok();
        });

        // Navigation keys are always raw injection (worked on every platform, regardless
        // of whether a media player is present).
        app.MapPost("/space", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Space);
            return Results.Ok();
        });

        app.MapPost("/arrow-left", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Left);
            return Results.Ok();
        });

        app.MapPost("/arrow-right", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Right);
            return Results.Ok();
        });

        app.MapPost("/arrow-up", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Up);
            return Results.Ok();
        });

        app.MapPost("/arrow-down", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Down);
            return Results.Ok();
        });

        app.MapPost("/backspace", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Backspace);
            return Results.Ok();
        });

        app.MapPost("/escape", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.Escape);
            return Results.Ok();
        });

        app.MapPost("/f11", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.F11);
            return Results.Ok();
        });

        // Volume is treated as system volume, so it stays as raw key injection
        // (matching Windows behavior, where VK_VOLUME_* adjusts system volume).
        app.MapPost("/volume-up", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.VolumeUp);
            return Results.Ok();
        });

        app.MapPost("/volume-down", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.VolumeDown);
            return Results.Ok();
        });

        app.MapPost("/volume-mute", [CustomAuthorize] async (IInputSimulator sim) =>
        {
            await sim.PressAsync(SimKey.VolumeMute);
            return Results.Ok();
        });
    }
}
