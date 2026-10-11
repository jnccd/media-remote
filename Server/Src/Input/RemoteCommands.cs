namespace Server.Input;

/// <summary>
/// The closed set of media/navigation actions the remote UI can trigger.
///
/// This enum, <see cref="RemoteCommands"/> and <see cref="RemoteCommandDispatcher"/> are the
/// single source of truth for "which buttons exist". The HTTP endpoints
/// (Endpoints/RegisterMediaControl.cs) and the WebSocket command channel
/// (Endpoints/RegisterInputSocket.cs) both dispatch through the dispatcher, so the two
/// transports cannot drift apart: adding a transport, or moving a button from one to the
/// other, never widens what the server will do. Adding a case here - and nothing else - is
/// how a new button gets added.
/// </summary>
public enum RemoteCommand
{
    Play,
    Next,
    Previous,
    Stop,
    Space,
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown,
    Backspace,
    Escape,
    F11,
    VolumeUp,
    VolumeDown,
    VolumeMute,
}

/// <summary>
/// Wire names for <see cref="RemoteCommand"/>. The names are deliberately the old HTTP route
/// paths ("arrow-left", "volume-up", ...) so a command means exactly the same thing whichever
/// transport carried it. Only names listed here are ever accepted off the wire; anything else
/// is ignored, so an unknown/forged name cannot reach an input backend.
/// </summary>
public static class RemoteCommands
{
    private static readonly Dictionary<string, RemoteCommand> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["play"] = RemoteCommand.Play,
        ["next"] = RemoteCommand.Next,
        ["previous"] = RemoteCommand.Previous,
        ["stop"] = RemoteCommand.Stop,
        ["space"] = RemoteCommand.Space,
        ["arrow-left"] = RemoteCommand.ArrowLeft,
        ["arrow-right"] = RemoteCommand.ArrowRight,
        ["arrow-up"] = RemoteCommand.ArrowUp,
        ["arrow-down"] = RemoteCommand.ArrowDown,
        ["backspace"] = RemoteCommand.Backspace,
        ["escape"] = RemoteCommand.Escape,
        ["f11"] = RemoteCommand.F11,
        ["volume-up"] = RemoteCommand.VolumeUp,
        ["volume-down"] = RemoteCommand.VolumeDown,
        ["volume-mute"] = RemoteCommand.VolumeMute,
    };

    /// <summary>Every action the server accepts, for logging/diagnostics.</summary>
    public static IReadOnlyCollection<string> Names => ByName.Keys;

    public static bool TryParse(string? name, out RemoteCommand command)
    {
        command = default;
        return !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out command);
    }
}

/// <summary>
/// Executes a whitelisted <see cref="RemoteCommand"/> against the platform backends. Media
/// transport goes through <see cref="IMediaController"/> (MPRIS on Linux, media keys on
/// Windows); navigation/volume keys are raw injection through <see cref="IInputSimulator"/>.
/// </summary>
[RegisterImplementation(ServiceRegisterType.Singleton, typeof(RemoteCommandDispatcher))]
public sealed class RemoteCommandDispatcher
{
    private readonly IMediaController _media;
    private readonly IInputSimulator _input;

    public RemoteCommandDispatcher(IMediaController media, IInputSimulator input)
    {
        _media = media;
        _input = input;
    }

    public Task ExecuteAsync(RemoteCommand command) => command switch
    {
        RemoteCommand.Play => _media.PlayPauseAsync(),
        RemoteCommand.Next => _media.NextAsync(),
        RemoteCommand.Previous => _media.PreviousAsync(),
        RemoteCommand.Stop => _media.StopAsync(),
        RemoteCommand.Space => _input.PressAsync(SimKey.Space),
        RemoteCommand.ArrowLeft => _input.PressAsync(SimKey.Left),
        RemoteCommand.ArrowRight => _input.PressAsync(SimKey.Right),
        RemoteCommand.ArrowUp => _input.PressAsync(SimKey.Up),
        RemoteCommand.ArrowDown => _input.PressAsync(SimKey.Down),
        RemoteCommand.Backspace => _input.PressAsync(SimKey.Backspace),
        RemoteCommand.Escape => _input.PressAsync(SimKey.Escape),
        RemoteCommand.F11 => _input.PressAsync(SimKey.F11),
        RemoteCommand.VolumeUp => _input.PressAsync(SimKey.VolumeUp),
        RemoteCommand.VolumeDown => _input.PressAsync(SimKey.VolumeDown),
        RemoteCommand.VolumeMute => _input.PressAsync(SimKey.VolumeMute),
        _ => Task.CompletedTask,
    };
}
