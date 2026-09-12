using Avalonia;

namespace DesktopApp;

internal static class Program
{
    // Avalonia wants an entry point on an STA thread; a no-op on Linux, required
    // by the Win32 backend.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Referenced by name by the Avalonia tooling.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
