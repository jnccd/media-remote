using System.Diagnostics;

namespace Server.Input;

/// <summary>
/// Linux backend for raw input injection. Uses the <c>ydotool</c> CLI, which injects at the
/// kernel level through <c>uinput</c> (not X11/XTest), so it works on Wayland sessions
/// (including KDE Plasma). Requires the <c>ydotoold</c> daemon running and the calling user
/// to be in the <c>input</c> group with the <c>uinput</c> kernel module loaded.
/// </summary>
public sealed class LinuxYdotoolInputSimulator : IInputSimulator
{
    // evdev keycodes from <linux/input-event-codes.h>.
    private static readonly IReadOnlyDictionary<SimKey, string> Keycodes = new Dictionary<SimKey, string>
    {
        [SimKey.Space] = "57",
        [SimKey.Left] = "105",
        [SimKey.Right] = "106",
        [SimKey.Up] = "103",
        [SimKey.Down] = "108",
        [SimKey.Backspace] = "14",
        [SimKey.Escape] = "1",
        [SimKey.VolumeUp] = "115",
        [SimKey.VolumeDown] = "114",
        [SimKey.VolumeMute] = "113",
        [SimKey.PlayPause] = "164",
        [SimKey.Next] = "163",
        [SimKey.Previous] = "165",
        [SimKey.Stop] = "128",
    };

    public async Task PressAsync(SimKey key)
    {
        var code = Keycodes[key];
        // ydotool 'key' does a press + release by default. The ':1'/':0' form is used for
        // chords/holds; a bare keycode is a single press+release.
        await RunAsync("ydotool", new[] { "key", code });
    }

    public async Task TypeTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await RunAsync("ydotool", new[] { "type", text });
    }

    public async Task MoveMouseAsync(int dx, int dy)
    {
        await RunAsync("ydotool", new[] { "mousemove", "--relative", $"--x={dx}", $"--y={dy}" });
    }

    public async Task ClickAsync(MouseButton button)
    {
        var buttonNum = button switch
        {
            MouseButton.Left => "1",
            MouseButton.Middle => "2",
            MouseButton.Right => "3",
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };
        await RunAsync("ydotool", new[] { "click", buttonNum });
    }

    public async Task ScrollAsync(int delta)
    {
        // delta > 0 => scroll up, delta < 0 => scroll down.
        var dir = delta >= 0 ? "--up" : "--down";
        await RunAsync("ydotool", new[] { "scroll", dir, Math.Abs(delta).ToString() });
    }

    public void Dispose() { }

    private static async Task RunAsync(string fileName, string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");

        // Drain output so the process never blocks on a full pipe.
        var stdout = proc.StandardOutput.ReadToEndAsync();
        var stderr = proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} exited with code {proc.ExitCode}: {stderr.Result.Trim()}");
    }
}
