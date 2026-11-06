using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

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

    private bool _widgetOpen;
    /// <summary>Set by MainWindow when the widget opens/closes.</summary>
    public bool IsWidgetOpen
    {
        get => _widgetOpen;
        set { _widgetOpen = value; Changed(); }
    }

    /// <summary>Dashboard shows its own Pause/Stop only while running with the widget closed.</summary>
    public bool ShowDashboardControls => IsRunning && !_widgetOpen;

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

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
    public string StatusText => !IsRunning ? "Not running" : IsPaused ? "Paused" : "Timer running";

    // ---------- actions ----------
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        IsPaused = false;
        _session = TimeSpan.Zero;
        _lastTick = DateTime.Now;
        _timer.Start();
        Log?.Info("Timer started");
        Changed();
    }

    public void TogglePause()
    {
        if (!IsRunning) return;
        Tick();                       // settle time up to this moment
        IsPaused = !IsPaused;
        _lastTick = DateTime.Now;
        Save();
        Log?.Info(IsPaused ? "Timer paused" : "Timer resumed");
        Changed();
    }

    public void Stop()
    {
        if (!IsRunning) return;
        Tick();
        IsRunning = false;
        IsPaused = false;
        _timer.Stop();
        Save();
        Log?.Info($"Timer stopped. Session {SessionText}, today {TodayText}");
        Changed();
    }

    // ---------- internals ----------
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