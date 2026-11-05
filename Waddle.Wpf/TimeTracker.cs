using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>
/// One shared timer for the whole app. The dashboard and the widget both read from this,
/// so they can never disagree. Daily totals are saved to %AppData%\Waddle\daily-time.json
/// and survive restarts. (No WPF types except DispatcherTimer, so it can move to Waddle.Core later.)
/// </summary>
public sealed class TimeTracker : INotifyPropertyChanged
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Waddle", "daily-time.json");

    public static TimeTracker Instance { get; } = new();

    private readonly Dictionary<string, double> _daily;                  // "yyyy-MM-dd" -> seconds
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick;
    private DateTime _lastSave = DateTime.MinValue;
    private TimeSpan _session = TimeSpan.Zero;

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
        _daily = Load();
        _timer.Tick += (_, _) => Tick();
    }

    // ---------- what the UI binds to ----------
    public TimeSpan Today => TimeSpan.FromSeconds(_daily.GetValueOrDefault(Key(DateTime.Now)));
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
    private void Tick()
    {
        var now = DateTime.Now;
        var delta = now - _lastTick;
        _lastTick = now;

        // ignore gaps (laptop sleep, debugger pause) so they don't count as sitting time
        if (!IsRunning || IsPaused || delta > TimeSpan.FromSeconds(5)) { Changed(); return; }

        _session += delta;
        var key = Key(now);
        _daily[key] = _daily.GetValueOrDefault(key) + delta.TotalSeconds;

        if (now - _lastSave > TimeSpan.FromSeconds(30)) Save();     // don't lose much if the app crashes
        Changed();
    }

    private static string Key(DateTime d) => d.ToString("yyyy-MM-dd");

    private void Save()
    {
        _lastSave = DateTime.Now;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_daily));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // best-effort: keep the timer running, but leave a trace
            Log?.Info($"WARNING: could not save daily time to {FilePath}: {ex.Message}");
        }
    }

    private static Dictionary<string, double> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); // refresh all
}