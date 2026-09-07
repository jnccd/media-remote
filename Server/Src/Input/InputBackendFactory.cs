using System.Runtime.InteropServices;

namespace Server.Input;

/// <summary>
/// Chooses the platform-appropriate input/media backend at startup.
/// </summary>
public static class InputBackendFactory
{
    public static IInputSimulator CreateInputSimulator()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsInputSimulator();
        if (OperatingSystem.IsLinux())
            return new LinuxYdotoolInputSimulator();

        throw new PlatformNotSupportedException(
            $"Input simulation is only supported on Windows or Linux, not '{RuntimeInformation.OSDescription}'.");
    }

    public static IMediaController CreateMediaController()
    {
        var inputSimulator = CreateInputSimulator();

        if (OperatingSystem.IsWindows())
            return new WindowsMediaController(inputSimulator);
        if (OperatingSystem.IsLinux())
            return new LinuxMprisMediaController(inputSimulator);

        throw new PlatformNotSupportedException(
            $"Media control is only supported on Windows or Linux, not '{RuntimeInformation.OSDescription}'.");
    }
}
