namespace Server.Input;

/// <summary>
/// Windows media-transport backend. Media keys (play/pause, next, previous, stop) are
/// injected as raw key presses via <see cref="IInputSimulator"/>, matching how Windows
/// media keys drive the active media session.
/// </summary>
public sealed class WindowsMediaController : IMediaController
{
    private readonly IInputSimulator _simulator;

    public WindowsMediaController(IInputSimulator simulator)
    {
        _simulator = simulator;
    }

    public Task PlayPauseAsync() => _simulator.PressAsync(SimKey.PlayPause);
    public Task NextAsync() => _simulator.PressAsync(SimKey.Next);
    public Task PreviousAsync() => _simulator.PressAsync(SimKey.Previous);
    public Task StopAsync() => _simulator.PressAsync(SimKey.Stop);

    public void Dispose() { }
}
