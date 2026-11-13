using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>
/// Pops the "start again" reminder whenever the timer is NOT running and the widget is CLOSED
/// for long enough. It counts from app launch too, and keeps coming back: every time the widget
/// is closed again and the timer is still off, the wait starts over and the reminder shows again.
/// It never shows while a session is running or while the widget (including the reminder) is open.
/// </summary>
public sealed class IdleReminder
{
    public static IdleReminder Instance { get; } = new();

    /// <summary>How long it must stay idle before the reminder appears again. 1 minute for testing; use 30 for real.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>How long after launching the app (with no session started) before the first reminder. 1 minute for testing.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Raised on the UI thread when the idle time is up.</summary>
    public event Action? Due;

    /// <summary>Set once at startup so you can see in the log why the reminder did or didn't appear.</summary>
    public ILogService? Log { get; set; }

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _idleSince;      // when "timer off + widget closed" started
    private bool _first = true;        // no reminder yet and no session yet since launch -> use StartupDelay

    private IdleReminder()
    {
        _poll.Tick += (_, _) => Check();
        _poll.Start();                 // starts as soon as App touches IdleReminder.Instance
    }

    /// <summary>Optional: call when a session ends to begin a fresh idle stretch.</summary>
    public void Arm() => _idleSince = null;

    private void Check()
    {
        var t = TimeTracker.Instance;

        if (t.IsRunning) _first = false;                    // they have used the app: normal delay from now on

        bool idle = !t.IsRunning && !t.IsWidgetOpen;
        if (!idle)
        {
            _idleSince = null;                              // running or widget open: the wait starts over
            return;
        }

        var wait = _first ? StartupDelay : Delay;
        if (_idleSince is null)
        {
            _idleSince = DateTime.Now;
            Log?.Info($"Idle reminder: timer off and widget closed, shows in {wait.TotalMinutes:0.#} min");
        }

        if (DateTime.Now - _idleSince < wait) return;

        _first = false;
        _idleSince = null;                                  // the widget opens now; if they close it, the wait restarts
        Log?.Info("Idle reminder due: showing the widget");
        Due?.Invoke();
    }
}