using Microsoft.Extensions.FileProviders;
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
        app.MapGet("/play", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPlay);
            return Results.Ok();
        });

        app.MapGet("/next", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaNext);
            return Results.Ok();
        });

        app.MapGet("/previous", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPrevious);
            return Results.Ok();
        });

        app.MapGet("/arrow-left", () =>
        {
            SimulateFullKeyPress(KeyCode.VcLeft);
            return Results.Ok();
        });

        app.MapGet("/arrow-right", () =>
        {
            SimulateFullKeyPress(KeyCode.VcRight);
            return Results.Ok();
        });

        app.MapGet("/arrow-up", () =>
        {
            SimulateFullKeyPress(KeyCode.VcUp);
            return Results.Ok();
        });

        app.MapGet("/arrow-down", () =>
        {
            SimulateFullKeyPress(KeyCode.VcDown);
            return Results.Ok();
        });

        app.MapGet("/kkey", () =>
        {
            SimulateFullKeyPress(KeyCode.VcK);
            return Results.Ok();
        });

        app.MapGet("/stop", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaStop);
            return Results.Ok();
        });

        app.MapGet("/volume-up", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeUp);
            return Results.Ok();
        });

        app.MapGet("/volume-down", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeDown);
            return Results.Ok();
        });

        app.MapGet("/volume-mute", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeMute);
            return Results.Ok();
        });
    }
}
