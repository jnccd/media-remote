using Microsoft.Extensions.FileProviders;
using SharpHook;
using SharpHook.Data;

namespace Server.Endpoints;

public static class MediaControl
{
    static EventSimulator simulator = new EventSimulator();

    public static void RegisterMediaControlEndpoints(this WebApplication app)
    {
        app.MapGet("/play", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcMediaPlay);
            return Results.Ok();
        });

        app.MapGet("/next", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcMediaNext);
            return Results.Ok();
        });

        app.MapGet("/previous", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcMediaPrevious);
            return Results.Ok();
        });

        app.MapGet("/arrow-left", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcLeft);
            return Results.Ok();
        });

        app.MapGet("/arrow-right", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcRight);
            return Results.Ok();
        });

        app.MapGet("/arrow-up", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcUp);
            return Results.Ok();
        });

        app.MapGet("/arrow-down", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcDown);
            return Results.Ok();
        });

        app.MapGet("/kkey", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcK);
            return Results.Ok();
        });

        app.MapGet("/stop", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcMediaStop);
            return Results.Ok();
        });

        app.MapGet("/volume-up", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcVolumeUp);
            return Results.Ok();
        });

        app.MapGet("/volume-down", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcVolumeDown);
            return Results.Ok();
        });

        app.MapGet("/volume-mute", () =>
        {
            simulator.SimulateKeyPress(KeyCode.VcVolumeMute);
            return Results.Ok();
        });
    }
}
