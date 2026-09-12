using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace DesktopApp;

public partial class App : Application
{
    private const int DefaultPort = 7779;

    private readonly List<PosixSignalRegistration> _signalHandlers = [];

    private MainWindow? _window;
    private ServerProcess? _server;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _window = new MainWindow();
            _server = new ServerProcess();

            _server.LineReceived += OnServerLine;
            _server.Exited += code => OnServerLine($"--- server exited with code {code} ---");

            // Deliberately NOT desktop.MainWindow: Avalonia's classic desktop
            // lifetime shows MainWindow as soon as it starts, so assigning it
            // would put a window on screen at login. ShutdownMode is
            // OnExplicitShutdown below, so nothing needs a MainWindow - the app
            // starts in the tray and the window is only an on-demand log view,
            // reachable from the tray's "Show window".
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) =>
            {
                _server.Dispose();
                foreach (var handler in _signalHandlers)
                    handler.Dispose();
            };

            RegisterSignalHandlers();

            // No window at startup: this is launched at login by the autostart,
            // and a log window appearing over whatever the user is doing is
            // noise.
            _server.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Stop the server when this process is asked to go away.
    ///
    /// Without this the server is orphaned: it is a child process and .NET does
    /// not reap children on SIGTERM. Logging out kills the session (and this app)
    /// while the server keeps holding the port, so the next login's server dies
    /// on the port clash and dumps a core file. Handle the signals a session
    /// teardown actually sends.
    /// </summary>
    private void RegisterSignalHandlers()
    {
        foreach (var signal in new[]
                 {
                     PosixSignal.SIGTERM,
                     PosixSignal.SIGINT,
                     PosixSignal.SIGHUP,
                     PosixSignal.SIGQUIT,
                 })
        {
            _signalHandlers.Add(PosixSignalRegistration.Create(signal, context =>
            {
                // Cancel the default kill so the child can be stopped first.
                context.Cancel = true;
                StopServerAndExit();
            }));
        }
    }

    private void StopServerAndExit()
    {
        _server?.Dispose();
        // Shutdown() wants the UI thread, and during a logout the dispatcher may
        // already be gone. The server is stopped by now, so leaving outright is
        // the reliable option.
        Environment.Exit(0);
    }

    /// <summary>
    /// Every server line goes to two places. The window is the obvious one; the
    /// echo to our own stdout is the point - the GUI autostart runs this under
    /// `screen`, so `screen -r gui-media-remote-<user>-<session>` now shows the
    /// server's log instead of nothing at all, and it lands in the launcher's
    /// app.log too.
    /// </summary>
    private void OnServerLine(string line)
    {
        Console.WriteLine(line);
        Dispatcher.UIThread.Post(() => _window?.Append(line));
    }

    /// <summary>The address the server actually serves on; PORT is set by the launcher.</summary>
    internal static string WebUiUrl() =>
        $"http://localhost:{(int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : DefaultPort)}";

    private void OnShowClicked(object? sender, EventArgs e) => _window?.ShowFromTray();

    private void OnOpenWebClicked(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(WebUiUrl()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            OnServerLine($"could not open {WebUiUrl()}: {ex.Message}");
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        // Let the window actually close this time; OnClosing otherwise hides it.
        if (_window is not null)
            _window.CanClose = true;

        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
