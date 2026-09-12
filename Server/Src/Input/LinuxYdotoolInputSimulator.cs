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
    /// <summary>
    /// Gap between the injected key-down and key-up. If this is too small the
    /// compositor can miss the press before the release arrives, which shows up
    /// as a key that "did nothing"; too small in the other direction leaves the
    /// key held and key-repeating. 60ms is enough in practice.
    /// </summary>
    private const int KeyDownToUpDelayMs = 60;

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
        [SimKey.F11] = "87",
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

        // Send the press and the release as SEPARATE ydotool invocations rather
        // than the bare `key <code>` form, which is documented as press+release
        // but in practice drops the release: the key stays down, the kernel
        // key-repeats it, and a volume press turns into the volume climbing
        // forever until another key event arrives.
        //
        // `type` does its own per-character press/release and works, so the
        // events themselves are fine - it is specifically the release of a bare
        // `key` that does not land. The explicit ':1' (down) / ':0' (up) form
        // does, and the gap between the two calls gives the compositor a chance
        // to process the down before the up.
        await RunAsync("ydotool", new[] { "key", $"{code}:1" });
        await Task.Delay(KeyDownToUpDelayMs);
        await RunAsync("ydotool", new[] { "key", $"{code}:0" });
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
