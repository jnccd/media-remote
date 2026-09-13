using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DesktopApp;

public partial class MainWindow : Window
{
    /// <summary>Keeps the TextBox from growing without bound on a chatty server.</summary>
    private const int MaxLines = 2000;

    private readonly List<string> _lines = [];

    /// <summary>
    /// Bumped by every HideToTray/ShowFromTray; a scheduled hide only fires while
    /// its token is still current. See HideToTray.
    /// </summary>
    private int _hideToken;

    /// <summary>
    /// Set by the tray's Quit so the window really closes. Otherwise closing
    /// hides it - the tray icon is the app's real surface.
    /// </summary>
    public bool CanClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = $"Serving {App.WebUiUrl()}";
    }

    /// <summary>Appends one server line and scrolls to it. Always on the UI thread.</summary>
    public void Append(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines)
            _lines.RemoveRange(0, _lines.Count - MaxLines);

        LogBox.Text = string.Join('\n', _lines);
        // Moving the caret is what makes the TextBox scroll to the newest line.
        LogBox.CaretIndex = LogBox.Text.Length;
    }

    public void ShowFromTray()
    {
        // Cancel any hide still pending from HideToTray, so a restore inside its
        // delay window is not undone a moment later.
        _hideToken++;

        Show();

        // Belt and braces for AvaloniaUI/Avalonia#11850: if the window ever did
        // end up minimized, nudge the renderer back to life. Near-free when it is
        // already running.
        RestartRendering();
        InvalidateVisual();

        Activate();
    }

    /// <summary>
    /// AvaloniaUI/Avalonia#11850: on Linux, setting WindowState back to Normal
    /// after a minimize restores the window but kills it - it paints once, then
    /// stops rendering and ignores input, and only recovers when something forces
    /// a resize (maximizing it "fixes" it, which is the giveaway). Restarting the
    /// renderer is the workaround the Avalonia maintainer gave on that issue.
    ///
    /// In Avalonia 11.3 TopLevel.Renderer, StartRendering and StopRendering are
    /// all internal, so this needs reflection. If a future version renames or
    /// removes it the call quietly does nothing rather than throwing - a frozen
    /// window is a bad outcome, a crash on restore is worse.
    /// </summary>
    private void RestartRendering()
    {
        try
        {
            typeof(TopLevel)
                .GetMethod("StartRendering", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(this, null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"media-control-desktop: could not restart rendering after restore: {ex.Message}");
        }
    }

    /// <summary>
    /// Minimizing means "hide to the tray", the same as closing: the tray icon is
    /// this app's real surface and the window is only an on-demand log view.
    /// ShowFromTray is the way back.
    /// </summary>
    private void HideToTray()
    {
        // Order and timing both matter, and every wrong combination was
        // reproduced on this machine's real stack (XWayland under KWin Wayland),
        // watching what the window manager sees across three minimize cycles:
        //
        //   Hide() then WindowState = Normal: the pending un-minimize re-maps the
        //     window after the hide - a see-through ghost you can still drag.
        //   Hide() while leaving the state Minimized: hides fine, but restoring it
        //     later freezes outright (AvaloniaUI/Avalonia#11850).
        //   Normal then Post(Hide, Background): works for the FIRST minimize, then
        //     ghosts from the second onwards. Background work is starved while the
        //     UI thread is busy, and this window is busy: every line the server
        //     prints posts an Append.
        //   Normal then a timer Hide: hidden on every cycle.
        //
        // So clear the state first, so the un-minimize lands while the window is
        // still up, and hide from a timer rather than a Background post. The
        // window ends up hidden with its state already Normal, which keeps the
        // restore path a plain Show() with no state transition.
        WindowState = WindowState.Normal;

        // The token cancels a pending hide if the tray restores the window first,
        // so it cannot hide the window again from under the user.
        var token = ++_hideToken;
        foreach (var delay in new[] { 500, 1500 })
        {
            DispatcherTimer.RunOnce(
                () =>
                {
                    if (_hideToken == token)
                        Hide();
                },
                TimeSpan.FromMilliseconds(delay));
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!CanClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Minimizing hides to the tray rather than leaving an entry in the taskbar,
    /// so "get out of the way" and "close" do the same thing.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty
            && change.GetNewValue<WindowState>() == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnClearClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _lines.Clear();
        LogBox.Text = string.Empty;
    }
}
