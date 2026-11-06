using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>
/// Why the timer is paused. A bigger number is more urgent: a more urgent reason replaces a less
/// urgent one, never the other way round. The user's own pause always wins over automatic ones.
/// </summary>
public enum PauseReason { None = 0, User = 1, Away = 2, Covered = 3, CameraOff = 4 }

/// <summary>
/// One shared timer for the whole app. The dashboard and the widget both read from this,
/// so they can never disagree. Daily totals are saved through IDailyTimeStore (SQLite).
/// </summary>
public sealed class TimeTracker : INotifyPropertyChanged
{
    public static TimeTracker Instance { get; } = new();

    private readonly Dictionary<DateOnly, double> _cache = new();   // day -> seconds (in memory)
    private readonly HashSet<DateOnly> _dirty = new();              // days changed since last save
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick;
    private DateTime _lastSave = DateTime.MinValue;
    private TimeSpan _session = TimeSpan.Zero;

    private IDailyTimeStore? _store;
    /// <summary>Set once at startup (before the timer is used).</summary>
    public IDailyTimeStore? Store
    {
        get => _store;
        set { _store = value; _cache.Clear(); _dirty.Clear(); Changed(); }
    }

    /// <summary>Set once at startup (TimeTracker.Instance.Log = logService).</summary>
    public ILogService? Log { get; set; }

    private PauseReason _reason = PauseReason.None;
    /// <summary>Why the timer is paused right now (None = not paused).</summary>
    public PauseReason Reason => _reason;

    /// <summary>Raised whenever a session ends (Stop button, widget, closing the app).</summary>
    public event Action? SessionEnded;
    /// <summary>Raised when Resume is pressed while paused because the camera is off.</summary>
    public event Action? RetryCameraRequested;
    /// <summary>Raised whenever the pause reason changes (including back to None).</summary>
    public event Action<PauseReason>? PauseReasonChanged;

    private bool _widgetOpen;
    /// <summary>Set by MainWindow when the widget opens/closes.</summary>
    public bool IsWidgetOpen
    {
        get => _widgetOpen;
        set { _widgetOpen = value; Changed(); }
    }

    private bool _widgetCollapsed;
    /// <summary>
    /// Set by the widget when it is shrunk to its small collapsed form. A collapsed widget can't show
    /// the status, so banners are allowed again while it is collapsed.
    /// </summary>
    public bool IsWidgetCollapsed
    {
        get => _widgetCollapsed;
        set { _widgetCollapsed = value; Changed(); }
    }

    /// <summary>Dashboard shows its own Pause/Stop only while running with the widget closed.</summary>
    public bool ShowDashboardControls => IsRunning && !_widgetOpen;

    public bool IsRunning { get; private set; }
    public bool IsPaused => _reason != PauseReason.None;

    private TimeTracker()
    {
        _timer.Tick += (_, _) => Tick();
    }

    // ---------- what the UI binds to ----------
    public TimeSpan Today => TimeSpan.FromSeconds(SecondsFor(DateOnly.FromDateTime(DateTime.Now)));
    public string TodayText => Today.TotalHours >= 1
        ? $"{(int)Today.TotalHours}h {Today.Minutes:00}m"
        : $"{Today.Minutes}m {Today.Seconds:00}s";
    public string SessionText => _session.ToString(@"hh\:mm\:ss");
    public string TimerButtonText => IsRunning ? "Open widget" : "Start timer";
    public string PauseButtonText => IsPaused ? "Resume" : "Pause";
    public string StatusText => !IsRunning ? "Not running" : _reason switch
    {
        PauseReason.User => "Paused",
        PauseReason.Away => "Paused: away",
        PauseReason.Covered => "Paused: camera is covered",
        PauseReason.CameraOff => "Paused: camera is off",
        _ => "Timer running"
    };

    // ---------- actions ----------
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        SetReason(PauseReason.None);
        _session = TimeSpan.Zero;
        _lastTick = DateTime.Now;
        _timer.Start();
        Log?.Info("Timer started");
        Changed();
    }

    /// <summary>The Pause / Resume button. What it does depends on why the timer is paused.</summary>
    public void TogglePause()
    {
        if (!IsRunning) return;

        switch (_reason)
        {
            case PauseReason.CameraOff:
                RetryCameraRequested?.Invoke();             // "Resume" = check the camera again
                return;
            case PauseReason.Covered:
                return;                                     // resumes by itself once uncovered and your face is seen
            case PauseReason.Away:
                ResumeFromAway();
                return;
        }

        Tick();                                             // settle time up to this moment
        SetReason(_reason == PauseReason.None ? PauseReason.User : PauseReason.None);
        _lastTick = DateTime.Now;
        Save();
        Log?.Info(IsPaused ? "Timer paused" : "Timer resumed");
        Changed();
    }

    /// <summary>
    /// What the Stop buttons call: asks "are you sure?" first. Returns true if the timer was stopped.
    /// (Closing the app still calls Stop() directly, with no question.)
    /// </summary>
    public bool ConfirmAndStop()
    {
        if (!IsRunning) return false;
        var answer = MessageBox.Show("Stop the timer? Your time so far is saved.", "Waddle",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return false;
        Stop();
        return true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        Tick();
        IsRunning = false;
        SetReason(PauseReason.None);
        _timer.Stop();
        Save();
        Log?.Info($"Timer stopped. Session {SessionText}, today {TodayText}");
        Changed();
        SessionEnded?.Invoke();
    }

    /// <summary>
    /// Pauses because of the camera or because the person left. Never overrides the user's own pause,
    /// and a less urgent reason never replaces a more urgent one.
    /// Returns true if the timer is now paused for exactly this reason.
    /// </summary>
    public bool AutoPause(PauseReason reason = PauseReason.CameraOff)
    {
        if (!IsRunning || reason <= PauseReason.User) return false;
        if (_reason == PauseReason.User) return false;           // the user's own pause wins
        if (_reason >= reason) return _reason == reason;         // same pause, or a more urgent one is already active

        if (_reason == PauseReason.None)
        {
            Tick();                                              // settle time up to this moment
            _lastTick = DateTime.Now;
        }
        SetReason(reason);
        Save();
        Log?.Info(reason switch
        {
            PauseReason.Away => "Timer paused: away",
            PauseReason.Covered => "Timer paused: camera covered",
            _ => "Timer paused: camera lost"
        });
        Changed();
        return true;
    }

    /// <summary>Camera is back (frames normal and a face seen). Only undoes a camera pause, never the user's or away.</summary>
    public void AutoResume()
    {
        if (_reason != PauseReason.CameraOff && _reason != PauseReason.Covered) return;
        Resume("camera back");
    }

    /// <summary>The person pressed Resume after being away.</summary>
    public void ResumeFromAway()
    {
        if (_reason != PauseReason.Away) return;
        Resume("back from away");
    }

    // ---------- internals ----------
    private void Resume(string why)
    {
        SetReason(PauseReason.None);
        _lastTick = DateTime.Now;
        Log?.Info($"Timer resumed: {why}");
        Changed();
    }

    private void SetReason(PauseReason reason)
    {
        if (_reason == reason) return;
        _reason = reason;
        PauseReasonChanged?.Invoke(reason);
    }

    // Seconds for a day: from memory if we have it, otherwise read once from the database.
    private double SecondsFor(DateOnly day)
    {
        if (_cache.TryGetValue(day, out var seconds)) return seconds;
        if (_store is null) return 0;                    // not wired yet; don't cache a fake 0
        try { seconds = _store.GetSeconds(day); }
        catch (Exception ex) { Log?.Info($"WARNING: could not read daily time: {ex.Message}"); return 0; }
        _cache[day] = seconds;
        return seconds;
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var delta = now - _lastTick;
        _lastTick = now;

        // ignore gaps (laptop sleep, debugger pause) so they don't count as sitting time
        if (!IsRunning || IsPaused || delta > TimeSpan.FromSeconds(5)) { Changed(); return; }

        _session += delta;
        var day = DateOnly.FromDateTime(now);
        _cache[day] = SecondsFor(day) + delta.TotalSeconds;
        _dirty.Add(day);

        if (now - _lastSave > TimeSpan.FromSeconds(30)) Save();     // don't lose much if the app crashes
        Changed();
    }

    private void Save()
    {
        _lastSave = DateTime.Now;
        if (_store is null) return;
        foreach (var day in new List<DateOnly>(_dirty))
        {
            try
            {
                _store.SetSeconds(day, (int)Math.Floor(_cache[day]));
                _dirty.Remove(day);
            }
            catch (Exception ex)
            {
                // best-effort: keep the timer running, try again next save
                Log?.Info($"WARNING: could not save daily time for {day:yyyy-MM-dd}: {ex.Message}");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); // refresh all
}