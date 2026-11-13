using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>Every posture number in one place. Change them here.</summary>
public static class PostureTuning
{
    /// <summary>Face width this many times the calibrated width = leaning in. 1.20 = 20% bigger. Lower it to be stricter.</summary>
    public const double SlouchFactor = 1.20;
    /// <summary>Back under this counts as sitting up. A bit lower than SlouchFactor so it doesn't flicker on the edge.</summary>
    public const double RecoverFactor = 1.08;
    /// <summary>The face must stay too big this long before we warn (so a quick lean in to read something is fine).</summary>
    public static readonly TimeSpan SlouchAfter = TimeSpan.FromSeconds(10);
    /// <summary>The face must stay normal this long before the warning clears.</summary>
    public static readonly TimeSpan SitUpAfter = TimeSpan.FromSeconds(1);
    /// <summary>How long the person gets to sit up once the warning shows.</summary>
    public static readonly TimeSpan Countdown = TimeSpan.FromSeconds(20);
}

/// <summary>
/// Compares the live face width with the calibrated one while a session is running. A face that stays
/// noticeably bigger than at calibration means the person is leaning toward the screen (slouching).
/// Raises events; App decides what to show (banner, angry duck).
/// </summary>
public sealed class PostureMonitor
{
    public static PostureMonitor Instance { get; } = new();

    /// <summary>Set once at startup (where the calibrated face size is kept).</summary>
    public ICalibrationStore? Store { get; set; }
    /// <summary>Set once at startup so the log shows when posture warnings start and end.</summary>
    public ILogService? Log { get; set; }

    public bool IsSlouching { get; private set; }

    /// <summary>The face has been too big for long enough: show the warning.</summary>
    public event Action? SlouchStarted;
    /// <summary>Seconds left of the countdown (20 down to 0). Raised whenever the number changes.</summary>
    public event Action<int>? CountdownTick;
    /// <summary>The countdown reached 0 and the person is still slouching. Hook a consequence here later.</summary>
    public event Action? CountdownExpired;
    /// <summary>They sat up, or the session stopped / paused: clear the warning.</summary>
    public event Action? SlouchEnded;
    /// <summary>Same as SlouchEnded, passing the reason ("sat up" or "session not active").</summary>
    public event Action<string>? SlouchEndedWithReason;

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private double? _baseline;                 // calibrated face width (share of frame), loaded when tracking begins
    private double _smooth;                    // face width averaged over a couple of seconds; 0 = no sample yet
    private DateTime? _bigSince, _normalSince, _slouchStart;
    private int _lastLeft = -1;
    private bool _expired;

    private PostureMonitor()
    {
        _poll.Tick += (_, _) => Check();
        _poll.Start();                         // starts as soon as App touches PostureMonitor.Instance
    }

    /// <summary>Clears the cached baseline so the updated calibration is loaded on the next check.</summary>
    public void ReloadBaseline()
    {
        _baseline = null;
        _smooth = 0;
        _bigSince = _normalSince = null;
    }

    private void Check()
    {
        var timer = TimeTracker.Instance;
        var camera = CameraMonitor.Instance;

        // only while a session is actually counting and the camera is working
        if (Store is null || !timer.IsRunning || timer.IsPaused || !camera.IsOn)
        {
            Reset();
            return;
        }

        if (_baseline is null)
        {
            var saved = Store.Load();
            if (saved is null || saved.FaceRatio <= 0) return;      // never calibrated: nothing to compare with
            _baseline = saved.FaceRatio;
            Log?.Info($"Posture: watching, calibrated face is {_baseline:P0} of frame");
        }

        if (camera.FaceCount < 1 || camera.FaceRatio <= 0) return;  // no face this moment (away is handled by the timer)

        _smooth = _smooth == 0 ? camera.FaceRatio : _smooth * 0.7 + camera.FaceRatio * 0.3;   // smooth out frame-to-frame jitter
        double ratio = _smooth / _baseline.Value;                   // 1.0 = same as calibration
        var now = DateTime.Now;

        if (!IsSlouching)
        {
            if (ratio < PostureTuning.SlouchFactor) { _bigSince = null; return; }

            _bigSince ??= now;
            if (now - _bigSince.Value >= PostureTuning.SlouchAfter) StartSlouch(now, ratio);
            return;
        }

        // already slouching: run the countdown, and watch for sitting up
        UpdateCountdown(now);

        if (ratio <= PostureTuning.RecoverFactor)
        {
            _normalSince ??= now;
            if (now - _normalSince.Value >= PostureTuning.SitUpAfter) EndSlouch("sat up");
        }
        else _normalSince = null;
    }

    private void StartSlouch(DateTime now, double ratio)
    {
        IsSlouching = true;
        _slouchStart = now;
        _bigSince = _normalSince = null;
        _expired = false;
        _lastLeft = -1;
        Log?.Info($"Posture: slouching (face is {ratio:P0} of calibrated size)");
        SlouchStarted?.Invoke();
        UpdateCountdown(now);
    }

    private void UpdateCountdown(DateTime now)
    {
        if (_slouchStart is not { } start) return;
        int left = (int)Math.Ceiling((PostureTuning.Countdown - (now - start)).TotalSeconds);
        if (left < 0) left = 0;
        if (left == _lastLeft) return;

        _lastLeft = left;
        CountdownTick?.Invoke(left);
        if (left == 0 && !_expired)
        {
            _expired = true;
            Log?.Info("Posture: countdown finished, still slouching");
            CountdownExpired?.Invoke();
        }
    }

    private void EndSlouch(string why)
    {
        if (!IsSlouching) return;
        IsSlouching = false;
        _slouchStart = null;
        _bigSince = _normalSince = null;
        Log?.Info($"Posture: warning cleared ({why})");
        SlouchEnded?.Invoke();
        SlouchEndedWithReason?.Invoke(why);
    }

    // session stopped / paused / camera off: forget everything (the calibration is read again next time)
    private void Reset()
    {
        EndSlouch("session not active");
        _baseline = null;
        _smooth = 0;
        _bigSince = _normalSince = null;
    }
}
