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
        app.MapPost("/play", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPlay);
            return Results.Ok();
        });

        app.MapPost("/next", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaNext);
            return Results.Ok();
        });

        app.MapPost("/previous", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaPrevious);
            return Results.Ok();
        });

        app.MapPost("/arrow-left", () =>
        {
            SimulateFullKeyPress(KeyCode.VcLeft);
            return Results.Ok();
        });

        app.MapPost("/arrow-right", () =>
        {
            SimulateFullKeyPress(KeyCode.VcRight);
            return Results.Ok();
        });

        app.MapPost("/arrow-up", () =>
        {
            SimulateFullKeyPress(KeyCode.VcUp);
            return Results.Ok();
        });

        app.MapPost("/arrow-down", () =>
        {
            SimulateFullKeyPress(KeyCode.VcDown);
            return Results.Ok();
        });

        app.MapPost("/space", () =>
        {
            SimulateFullKeyPress(KeyCode.VcSpace);
            return Results.Ok();
        });

        app.MapPost("/stop", () =>
        {
            SimulateFullKeyPress(KeyCode.VcMediaStop);
            return Results.Ok();
        });

        app.MapPost("/volume-up", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeUp);
            return Results.Ok();
        });

        app.MapPost("/volume-down", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeDown);
            return Results.Ok();
        });

        app.MapPost("/volume-mute", () =>
        {
            SimulateFullKeyPress(KeyCode.VcVolumeMute);
            return Results.Ok();
        });
    }
}
