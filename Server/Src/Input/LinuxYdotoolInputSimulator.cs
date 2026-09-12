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
        // `mousemove` reads its two coordinates from -x/-y and is RELATIVE unless
        // --absolute is given. The previous form (--relative --x=5 --y=3) did in
        // fact work, but only by accident: getopt_long matches long options by
        // unambiguous prefix, so --x=5 resolved to xpos and --y=3 to ypos, while
        // the unknown --relative was just warned about and skipped. Nothing
        // guaranteed that, so use the documented short form. mousemove also
        // insists on exactly two values, so both axes are always sent.
        await RunAsync("ydotool", new[] { "mousemove", "-x", dx.ToString(), "-y", dy.ToString() });
    }

    public async Task ClickAsync(MouseButton button)
    {
        // ydotool encodes a button as a HEXADECIMAL number in the low nibble plus
        // an optional press/release bit mask: 0x40 = down, 0x80 = up.
        //
        // The mask is not really optional: a bare `click 1` parses as 0x01, which
        // matches neither 0x40 nor 0x80, so ydotool emits no event whatsoever -
        // every click silently did nothing while still exiting 0. 0xC<n> is
        // down-then-up inside a single invocation (ydotool spaces them with its
        // 25ms --next-delay default), which also avoids leaving a button held
        // down if a second, separate call were ever to fail.
        var buttonBits = button switch
        {
            MouseButton.Left => 0x0,
            MouseButton.Right => 0x1,
            MouseButton.Middle => 0x2,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };
        await RunAsync("ydotool", new[] { "click", $"0x{0xC0 | buttonBits:X2}" });
    }

    public async Task ScrollAsync(int delta)
    {
        // ydotool 1.x has no `scroll` command at all, so the previous
        // "scroll --up/--down" invocation failed with "Unknown command: scroll"
        // and scrolling never worked. The wheel is driven through
        // `mousemove --wheel`, where the y axis is REL_WHEEL and x is REL_HWHEEL.
        // A positive REL_WHEEL scrolls up, which matches this method's contract
        // that delta > 0 scrolls up.
        await RunAsync("ydotool", new[] { "mousemove", "--wheel", "-x", "0", "-y", delta.ToString() });
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
