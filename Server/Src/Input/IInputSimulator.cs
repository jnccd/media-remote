namespace Server.Input;

/// <summary>
/// Raw input injection (keyboard + mouse), independent of any media/volume semantics.
/// Used for directional navigation (arrows, space, backspace) and, on platforms without
/// a higher-level media backend, as a fallback for media/volume keys. Mouse support is
/// part of the seam from the start so it can be used later without an API change.
/// </summary>
public interface IInputSimulator : IDisposable
{
    Task PressAsync(SimKey key);

    /// <summary>Types arbitrary text character-by-character (general keyboard input).</summary>
    Task TypeTextAsync(string text);

    /// <summary>Moves the pointer by <paramref name="dx"/>/<paramref name="dy"/> pixels (relative).</summary>
    Task MoveMouseAsync(int dx, int dy);

    Task ClickAsync(MouseButton button);

    /// <summary><paramref name="delta"/> greater than 0 scrolls up, less than 0 scrolls down.</summary>
    Task ScrollAsync(int delta);
}
