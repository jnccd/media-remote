namespace Server.Input;

/// <summary>
/// Media transport control. On Linux this is backed by MPRIS-over-D-Bus (the correct,
/// Wayland-native way to control the active media player); on Windows it is backed by
/// simulated media keys. It intentionally does not handle system volume, which is
/// treated as raw key injection via <see cref="IInputSimulator"/>.
/// </summary>
public interface IMediaController : IDisposable
{
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task StopAsync();
}
