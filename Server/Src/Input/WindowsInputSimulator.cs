using System.Runtime.InteropServices;

namespace Server.Input;

/// <summary>
/// Windows backend for raw input injection. Uses <c>SendInput</c> from user32 so media
/// keys (VK_MEDIA_*) are delivered reliably, and supports mouse move/click/wheel.
/// </summary>
public sealed class WindowsInputSimulator : IInputSimulator
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint INPUT_MOUSE = 0;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    // Windows virtual-key codes for the keys we supply as a fallback path.
    private static readonly IReadOnlyDictionary<SimKey, ushort> VkMap = new Dictionary<SimKey, ushort>
    {
        [SimKey.Space] = 0x20,
        [SimKey.Left] = 0x25,
        [SimKey.Right] = 0x27,
        [SimKey.Up] = 0x26,
        [SimKey.Down] = 0x28,
        [SimKey.Backspace] = 0x08,
        [SimKey.VolumeUp] = 0xAF,
        [SimKey.VolumeDown] = 0xAE,
        [SimKey.VolumeMute] = 0xAD,
        [SimKey.PlayPause] = 0xB3,
        [SimKey.Next] = 0xB0,
        [SimKey.Previous] = 0xB1,
        [SimKey.Stop] = 0xB2,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public async Task PressAsync(SimKey key)
    {
        if (!VkMap.TryGetValue(key, out var vk))
            throw new NotSupportedException($"SimKey {key} is not supported on Windows.");

        await Task.Run(() =>
        {
            SendKey(vk, keyUp: false);
            Thread.Sleep(20);
            SendKey(vk, keyUp: true);
        });
    }

    public async Task TypeTextAsync(string text)
    {
        await Task.Run(() =>
        {
            foreach (var ch in text)
            {
                SendUnicode(ch, keyUp: false);
                Thread.Sleep(6);
                SendUnicode(ch, keyUp: true);
            }
        });
    }

    public async Task MoveMouseAsync(int dx, int dy)
    {
        await Task.Run(() =>
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = dx,
                        dy = dy,
                        dwFlags = MOUSEEVENTF_MOVE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
            SendOnly(input);
        });
    }

    public async Task ClickAsync(MouseButton button)
    {
        var (down, up) = button switch
        {
            MouseButton.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        await Task.Run(() =>
        {
            SendMouseFlag(down);
            Thread.Sleep(20);
            SendMouseFlag(up);
        });
    }

    public async Task ScrollAsync(int delta)
    {
        await Task.Run(() =>
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        mouseData = unchecked((uint)delta),
                        dwFlags = MOUSEEVENTF_WHEEL,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
            SendOnly(input);
        });
    }

    public void Dispose() { }

    private static void SendKey(ushort vk, bool keyUp)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        SendOnly(input);
    }

    private static void SendUnicode(char ch, bool keyUp)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = (keyUp ? KEYEVENTF_KEYUP : 0) | KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        SendOnly(input);
    }

    private static void SendMouseFlag(uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        SendOnly(input);
    }

    private static void SendOnly(INPUT input)
    {
        uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent == 0)
            throw new InvalidOperationException(
                $"SendInput failed (Win32 error {Marshal.GetLastWin32Error()}).");
    }
}
