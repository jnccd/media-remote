using System.Diagnostics;

namespace DesktopApp;

/// <summary>
/// Owns the bundled MediaControlServer process.
///
/// This is the whole reason the wrapper exists: the server is a plain HTTP
/// daemon with no tray or window of its own, so something has to start it at
/// login, keep it alive with the session, and surface its output.
/// </summary>
public sealed class ServerProcess : IDisposable
{
    private Process? _process;
    /// <summary>One line of server output, stdout or stderr, unprefixed.</summary>
    public event Action<string>? LineReceived;

    public event Action<int>? Exited;

    public bool IsRunning => _process is { HasExited: false };

    public void Start()
    {
        if (IsRunning)
            return;

        var program = LocateServer();
        if (program is null)
        {
            LineReceived?.Invoke(
                "Could not find MediaControlServer. Looked at $MEDIA_CONTROL_SERVER and next to "
                    + $"'{Environment.ProcessPath}'. Set MEDIA_CONTROL_SERVER to point at it.");
            return;
        }

        // A source build points at the server's .dll, because the apphost the
        // SDK generates next to it is a native launcher that mixes glibc versions
        // on NixOS and dies before any managed code runs (librt.so.1 from one
        // glibc, libc.so.6 from the SDK's). So start a .dll through the `dotnet`
        // muxer, which nixpkgs patches correctly. The packaged layout ships a
        // native apphost with no .dll extension, which is run directly.
        var (fileName, fileArgs) = program.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? ("dotnet", new[] { program })
            : (program, []);

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            // Both streams are piped so they can be shown in the window and
            // echoed to our own stdout (see App.OnServerLine).
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = WorkingDirectoryFor(program),
        };
        foreach (var arg in fileArgs)
            startInfo.ArgumentList.Add(arg);

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                LineReceived?.Invoke(e.Data);
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                LineReceived?.Invoke(e.Data);
        };
        _process.Exited += (_, _) => Exited?.Invoke(_process?.ExitCode ?? -1);

        LineReceived?.Invoke($"Starting server: {program}");

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// Where to run the server from.
    ///
    /// The packaged layout keeps the server and its Frontend/dist in one
    /// directory, so the server's own location is the right content root. A
    /// source build does not: the server lands in Server/bin/Release/... while
    /// the web UI is built into Frontend/dist, and RegisterStaticFiles.cs looks
    /// for Frontend/dist relative to the content root (and its parent). So
    /// start_desktop_app.sh sets this to the repo's Server directory, where
    /// ../Frontend/dist resolves.
    /// </summary>
    private static string WorkingDirectoryFor(string program)
    {
        var overridden = Environment.GetEnvironmentVariable("MEDIA_CONTROL_SERVER_CWD");
        if (!string.IsNullOrWhiteSpace(overridden) && Directory.Exists(overridden))
            return overridden;

        return Path.GetDirectoryName(program) is { Length: > 0 } dir
            ? dir
            : Environment.CurrentDirectory;
    }

    /// <summary>
    /// The Nix package installs the server next to this executable (a symlink
    /// into the server store path, so its own Frontend/dist and native
    /// libraries stay resolvable). $MEDIA_CONTROL_SERVER overrides that for
    /// development. Mirrors find_bundled_server() in the Tauri wrapper it
    /// replaces.
    /// </summary>
    private static string? LocateServer()
    {
        const string exeName = "MediaControlServer";

        var overridden = Environment.GetEnvironmentVariable("MEDIA_CONTROL_SERVER");
        if (!string.IsNullOrWhiteSpace(overridden) && File.Exists(overridden))
            return overridden;

        if (Path.GetDirectoryName(Environment.ProcessPath) is { Length: > 0 } selfDir)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(selfDir, exeName),
                Path.Combine(selfDir, "server", exeName),
            })
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public void Dispose()
    {
        var process = _process;
        _process = null;
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            // Losing the server on exit is survivable; failing to quit is not.
            Console.Error.WriteLine($"media-control-desktop: could not stop the server: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }
}
