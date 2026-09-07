namespace Server.Input;

/// <summary>
/// Logical keys the remote can inject. These are intentionally abstract (not tied to
/// a specific OS virtual-key or evdev code) so each platform backend can map them.
/// Media/volume keys are included so an injector can be used as a fallback when a
/// higher-level backend (e.g. MPRIS) is unavailable.
/// </summary>
public enum SimKey
{
    Space,
    Left,
    Right,
    Up,
    Down,
    Backspace,
    Escape,
    VolumeUp,
    VolumeDown,
    VolumeMute,
    PlayPause,
    Next,
    Previous,
    Stop,
}
