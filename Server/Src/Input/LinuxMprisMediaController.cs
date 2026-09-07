using Tmds.DBus;

namespace Server.Input;

/// <summary>MPRIS Player interface (org.mpris.MediaPlayer2.Player).</summary>
[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IMediaPlayer : IDBusObject
{
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task StopAsync();
    Task PlayAsync();
    Task PauseAsync();
    Task<string> GetPlaybackStatusAsync();
    Task<double> GetVolumeAsync();
    Task SetVolumeAsync(double value);
}

/// <summary>org.freedesktop.DBus, used to enumerate bus names and find the active player.</summary>
[DBusInterface("org.freedesktop.DBus")]
public interface IFreedesktopDbus : IDBusObject
{
    Task<string[]> ListNamesAsync();
}

/// <summary>
/// Linux media-transport backend. Controls the active media player over MPRIS (D-Bus),
/// which is the correct approach on Wayland (instead of injecting media keys, which only
/// reaches XWayland apps). If no MPRIS player is available, falls back to key injection.
/// </summary>
public sealed class LinuxMprisMediaController : IMediaController
{
    private const string MediaPlayerPrefix = "org.mpris.MediaPlayer2.";
    private static readonly ObjectPath PlayerPath = new("/org/mpris/MediaPlayer2");
    private static readonly ObjectPath DbusPath = new("/org/freedesktop/DBus");

    private readonly IInputSimulator _fallback;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Connection? _connection;

    public LinuxMprisMediaController(IInputSimulator fallback)
    {
        _fallback = fallback;
    }

    public async Task PlayPauseAsync() =>
        await SendAsync(player => player.PlayPauseAsync(), SimKey.PlayPause);

    public async Task NextAsync() =>
        await SendAsync(player => player.NextAsync(), SimKey.Next);

    public async Task PreviousAsync() =>
        await SendAsync(player => player.PreviousAsync(), SimKey.Previous);

    public async Task StopAsync() =>
        await SendAsync(player => player.StopAsync(), SimKey.Stop);

    public void Dispose() => _connection?.Dispose();

    private async Task SendAsync(
        Func<IMediaPlayer, Task> action,
        SimKey fallbackKey)
    {
        var player = await GetActivePlayerAsync();
        if (player is not null)
        {
            try
            {
                await action(player);
                return;
            }
            catch
            {
                // Fall through to key injection if the player erred.
            }
        }

        await _fallback.PressAsync(fallbackKey);
    }

    private async Task<IMediaPlayer?> GetActivePlayerAsync()
    {
        try
        {
            var connection = await GetConnectionAsync();
            var dbus = connection.CreateProxy<IFreedesktopDbus>("org.freedesktop.DBus", DbusPath);
            var names = await dbus.ListNamesAsync();

            var players = names
                .Where(n => n.StartsWith(MediaPlayerPrefix, StringComparison.Ordinal)
                            && n.Length > MediaPlayerPrefix.Length)
                .ToList();
            if (players.Count == 0)
                return null;

            // Prefer a player that reports "Playing"; otherwise use the first responsive one.
            IMediaPlayer? first = null;
            foreach (var name in players)
            {
                var proxy = connection.CreateProxy<IMediaPlayer>(name, PlayerPath);
                if (first is null)
                    first = proxy;

                try
                {
                    var status = await proxy.GetPlaybackStatusAsync();
                    if (status == "Playing")
                        return proxy;
                }
                catch
                {
                    // Skip players that don't answer introspection.
                }
            }

            return first;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Connection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        await _initLock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                var connection = new Connection(Address.Session);
                await connection.ConnectAsync();
                _connection = connection;
            }
            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
