using Microsoft.AspNetCore.Authorization;
using SharpHook;
using SharpHook.Data;

namespace Server.Endpoints;

public static class MediaControl
{
    static EventSimulator simulator = new EventSimulator();
    static void SimulateFullKeyPress(KeyCode keyCode)
    {
        simulator.SimulateKeyPress(keyCode);
        Task.Delay(80).Wait(); // Adding a small delay to ensure the key press is registered
        simulator.SimulateKeyRelease(keyCode);
    }

    public static void RegisterMediaControlEndpoints(this WebApplication app)
    {
        app.MapPost("/play", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPlay);
            return Results.Ok();
        });

        app.MapPost("/next", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaNext);
            return Results.Ok();
        });

        app.MapPost("/previous", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPrevious);
            return Results.Ok();
        });

        app.MapPost("/arrow-left", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcLeft);
            return Results.Ok();
        });

        app.MapPost("/arrow-right", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcRight);
            return Results.Ok();
        });

        app.MapPost("/arrow-up", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcUp);
            return Results.Ok();
        });

        app.MapPost("/arrow-down", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcDown);
            return Results.Ok();
        });

        app.MapPost("/space", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcSpace);
            return Results.Ok();
        });

        app.MapPost("/backspace", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcBackspace);
            return Results.Ok();
        });

        app.MapPost("/stop", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaStop);
            return Results.Ok();
        });

        app.MapPost("/volume-up", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeUp);
            return Results.Ok();
        });

        app.MapPost("/volume-down", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeDown);
            return Results.Ok();
        });

        app.MapPost("/volume-mute", [CustomAuthorize] () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeMute);
            return Results.Ok();
        });
    }
}
