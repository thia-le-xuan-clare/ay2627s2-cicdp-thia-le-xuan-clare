using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>How urgent a banner is. A more urgent one replaces a less urgent one. Posture warnings can slot in above these later.</summary>
public enum BannerKind { Away = 1, Covered = 2, CameraOff = 3 }

/// <summary>
/// Shows ONE small banner at a time. It stays hidden while the widget is open and expanded (the widget
/// shows the status itself), but is allowed when the widget is closed or collapsed.
/// Dismiss only hides it; it never resumes the timer.
/// </summary>
public sealed class BannerService
{
    public static BannerService Instance { get; } = new();

    private sealed class Entry
    {
        public BannerKind Kind;
        public string Message = "";
        public string? ButtonText;
        public Action? OnButton;
        public Func<Task>? OnButtonAsync;    // for buttons that take a while (the button shows "Trying..." meanwhile)
        public DateTime ShowAt;              // don't show before this (keeps short camera blips quiet)
        public bool Nudge;                   // remind once more after BannerNudge if dismissed
        public DateTime? NudgeAt;
        public bool Nudged;
        public bool Dismissed;
        public bool LoggedWaiting;
    }

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private BannerWindow? _window;
    private Entry? _current;                 // the banner we want to show
    private Entry? _shown;                   // the banner currently on screen
    private bool _buttonBusy;

    private BannerService()
    {
        _tick.Tick += (_, _) => Refresh();
    }

    /// <summary>Set once at startup. Banner events go to the info log so you can see why a banner did or didn't appear.</summary>
    public ILogService? Log { get; set; }

    /// <summary>
    /// Asks for a banner. Ignored if a banner of the same or higher urgency is already waiting;
    /// otherwise it replaces the current one.
    /// </summary>
    /// <param name="onButton">Quick action (for example Resume).</param>
    /// <param name="onButtonAsync">Slow action (for example Try again). The button shows "Trying..." until it finishes.</param>
    /// <param name="delay">Wait this long before actually showing it (cancelled if Clear is called first).</param>
    /// <param name="nudge">Remind once more after about 10 minutes if it was dismissed and the problem remains.</param>
    public void Show(BannerKind kind, string message, string? buttonText = null, Action? onButton = null,
                     Func<Task>? onButtonAsync = null, TimeSpan? delay = null, bool nudge = true)
    {
        if (_current != null && _current.Kind >= kind) return;

        HideWindow();
        var wait = delay ?? TimeSpan.Zero;
        _current = new Entry
        {
            Kind = kind,
            Message = message,
            ButtonText = buttonText,
            OnButton = onButton,
            OnButtonAsync = onButtonAsync,
            ShowAt = DateTime.Now + wait,
            Nudge = nudge
        };
        Log?.Info(wait > TimeSpan.Zero
            ? $"Banner queued: \"{message}\" (shows in {wait.TotalSeconds:0} s)"
            : $"Banner queued: \"{message}\"");
        _tick.Start();
        Refresh();
    }

    /// <summary>Removes the banner if it is of this kind (the problem is over).</summary>
    public void Clear(BannerKind kind)
    {
        if (_current?.Kind == kind) ClearAll();
    }

    public void ClearAll()
    {
        if (_current != null) Log?.Info("Banner cleared");
        _current = null;
        _tick.Stop();
        HideWindow();
    }

    // Decides, every half second, whether the wanted banner should be on screen right now.
    private void Refresh()
    {
        var e = _current;
        var now = DateTime.Now;

        if (e == null || now < e.ShowAt)
        {
            HideWindow();
            return;
        }

        // an expanded widget shows the status itself; closed or collapsed doesn't
        var tracker = TimeTracker.Instance;
        if (tracker.IsWidgetOpen && !tracker.IsWidgetCollapsed)
        {
            if (!e.LoggedWaiting)
            {
                e.LoggedWaiting = true;
                Log?.Info("Banner waiting: the widget is open (it shows after the widget is closed or collapsed)");
            }
            HideWindow();
            return;
        }

        if (e.Dismissed)
        {
            // dismissed: hidden, except for one reminder after a while if it's still relevant
            if (e.Nudge && !e.Nudged && e.NudgeAt is { } at && now >= at)
            {
                e.Nudged = true;
                e.Dismissed = false;
                Log?.Info("Banner reminder");
            }
            else
            {
                HideWindow();
                return;
            }
        }

        if (_shown == e) return;                                    // already on screen

        _window ??= CreateWindow();
        _window.SetContent(e.Message, e.ButtonText);
        if (!_window.IsVisible) _window.Show();                     // ShowActivated=false: no focus stolen
        _shown = e;
        Log?.Info($"Banner shown: \"{e.Message}\"");
    }

    private BannerWindow CreateWindow()
    {
        var w = new BannerWindow();
        w.ActionClicked += async () => await OnActionAsync();
        w.DismissClicked += () =>
 {
     if (_current != null)
     {
         _current.Dismissed = true;                              // only hides; the timer stays paused
         if (_current.Nudge && !_current.Nudged)
             _current.NudgeAt = DateTime.Now + MonitorTuning.BannerNudge;   // 10 min from now, not from first show
     }
     Log?.Info("Banner dismissed");
     HideWindow();
 };
        return w;
    }

    private async Task OnActionAsync()
    {
        var e = _current;
        if (e == null || _buttonBusy) return;

        if (e.OnButtonAsync is { } run)
        {
            _buttonBusy = true;
            _window?.SetBusy(true);                                 // "Trying..." and greyed out straight away
            try { await run(); }
            catch { /* the camera monitor reports its own problems; never crash the app from a button */ }
            finally
            {
                _buttonBusy = false;
                _window?.SetBusy(false);
            }
        }
        else e.OnButton?.Invoke();
    }

    private void HideWindow()
    {
        if (_window?.IsVisible == true) _window.Hide();
        _shown = null;
    }
}