using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>
/// Every timing number for camera watching and the banner, in one place. Change them here.
/// </summary>
public static class MonitorTuning
{
    /// <summary>No face for this long = the person is away.</summary>
    public static readonly TimeSpan AwayAfter = TimeSpan.FromSeconds(12);
    /// <summary>A face must be seen for this long before we call it "back" (also used to resume after covered / camera off).</summary>
    public static readonly TimeSpan FaceBackAfter = TimeSpan.FromSeconds(2.5);
    /// <summary>Nearly black frames for this long = the lens is covered.</summary>
    public static readonly TimeSpan CoveredAfter = TimeSpan.FromSeconds(3);
    /// <summary>Average frame brightness (0-255) below this counts as "black". Raise it if a cover isn't noticed, lower it for dim rooms.</summary>
    public const double CoveredBrightness = 20;
    /// <summary>No frames for this long = the camera is off or gone.</summary>
    public static readonly TimeSpan FramesLostAfter = TimeSpan.FromSeconds(4);
    /// <summary>While the camera is off, quietly try to open it again this often.</summary>
    public static readonly TimeSpan CameraRetryEvery = TimeSpan.FromSeconds(10);
    /// <summary>The "camera is off" banner only appears after the camera has been off this long (so meetings stay quiet).</summary>
    public static readonly TimeSpan CameraOffBannerDelay = TimeSpan.FromSeconds(30);
    /// <summary>A dismissed banner reminds you once more after this long if you're still paused.</summary>
    public static readonly TimeSpan BannerNudge = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Owns the camera for the whole app. The dashboard and the widget only DISPLAY what is here.
/// The camera is on while at least one "holder" needs it ("session", "calibration").
///
/// During a session it also watches for three situations and raises an event for each:
///   camera off (no frames), camera covered (frames but nearly black), away (camera fine, no face).
/// </summary>
public sealed class CameraMonitor : INotifyPropertyChanged
{
    public static CameraMonitor Instance { get; } = new();

    private const string NoCameraMessage =
        "Your camera is off or can't be opened. Turn it on, then press Try again. The timer won't run without it.";
    private const string StoppedMessage =
        "Your camera stopped. The timer is paused. Turn it on and Waddle will carry on, or press Try again.";
    private const string CoveredAtStartMessage =
        "Your camera looks covered. Uncover it, then press Try again. The timer won't run without it.";
    private const string CoveredMessage =
        "Your camera looks covered. The timer is paused. Uncover it and it will carry on once it sees you.";

    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private readonly HashSet<string> _holders = new();
    private readonly DispatcherTimer _watchdog = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _retry = new() { Interval = MonitorTuning.CameraRetryEvery };
    private ICameraService? _camera;
    private TaskCompletionSource<bool>? _firstFrame;
    private DateTime _lastFrame;
    private bool _started;     // camera thread has been started (and not stopped by us)
    private bool _busy;       // a start/retry is in progress

    // ---- session watching (only meaningful while _armed) ----
    private bool _armed;       // a session has begun: the first normal frame was seen
    private bool _lost;        // camera went away during the session; waiting for it to return AND a face
    private bool _covered;     // lens covered; waiting for normal frames AND a face
    private bool _away;        // no face for a while
    private bool _frameDark;   // the latest frame was nearly black
    private bool _sawDark;     // a dark frame arrived while waiting for the first normal frame
    private DateTime? _darkSince, _faceSince, _noFaceSince;

    private CameraMonitor()
    {
        _watchdog.Tick += (_, _) =>
        {
            // frames stopped arriving even though no error was raised (camera hung / unplugged)
            if (_started && IsOn && DateTime.Now - _lastFrame > MonitorTuning.FramesLostAfter)
                HandleGone(StoppedMessage);
        };
        _retry.Tick += async (_, _) => await RetryTickAsync();
    }

    public ILogService? Log { get; set; }

    /// <summary>Camera disappeared during a session (timer should pause).</summary>
    public event Action? CameraLost;
    /// <summary>Lens has been covered for a few seconds during a session (timer should pause).</summary>
    public event Action? CameraCovered;
    /// <summary>After CameraLost or CameraCovered: frames are normal again and a face has been seen (timer can resume).</summary>
    public event Action? CameraRestored;
    /// <summary>Camera is fine but no face for a while.</summary>
    public event Action? AwayStarted;
    /// <summary>A face is back after AwayStarted.</summary>
    public event Action? FaceBack;

    public ICameraService? Camera
    {
        get => _camera;
        set
        {
            if (_camera != null) { _camera.FrameReady -= OnFrameBg; _camera.Failed -= OnFailedBg; }
            _camera = value;
            if (_camera != null) { _camera.FrameReady += OnFrameBg; _camera.Failed += OnFailedBg; }
        }
    }

    // ---------- what the UI binds to ----------
    public ImageSource? Frame { get; private set; }
    public string FaceText { get; private set; } = "";

    public int FaceCount { get; private set; }
    public double FaceRatio { get; private set; }

    public string? Problem { get; private set; }
    public bool IsOn { get; private set; }
    public bool IsChecking { get; private set; }
    public bool HasFrame => Frame != null;
    public bool HasProblem => Problem != null;
    public bool HasHolders => _holders.Count > 0;
    public string? OverlayText => Problem ?? (HasFrame ? null : "Starting camera...");
    public bool HasOverlay => OverlayText != null;
    public string? Banner => Problem ?? (IsChecking ? "Checking your camera..." : null);
    public bool HasBanner => Banner != null;

    // ---------- use / release ----------
    /// <summary>
    /// Turns the camera on and waits for the first NORMAL frame (not a black one).
    /// False = camera not usable (Problem says why), so the timer shouldn't start.
    /// </summary>
    public async Task<bool> AcquireAsync(string owner)
    {
        _holders.Add(owner);

        bool ok;
        if (IsOn && !_frameDark) ok = true;
        else ok = await StartAndWaitAsync(silent: false);

        if (!ok) { Release(owner, keepProblem: true); return false; }   // leave the reminder showing
        if (owner == "session") ArmSession();
        return true;
    }

    public void Release(string owner, bool keepProblem = false)
    {
        _holders.Remove(owner);
        if (owner == "session") ResetTracking();
        if (_holders.Count > 0) return;
        StopCamera();
        if (!keepProblem) Problem = null;
        Notify();
    }

    /// <summary>Try the camera again (used by the Try again buttons).</summary>
    public async Task<bool> RetryAsync() => _holders.Count > 0 && await StartAndWaitAsync(silent: false);

    public void Shutdown()
    {
        _holders.Clear();
        StopCamera();
        Notify();
    }

    // ---------- internals ----------
    /// <summary>
    /// The timer is running again (resumed any way at all): start watching for "away" from scratch.
    /// Without this, pressing Resume while still away left the away flag stuck on and it never paused again.
    /// </summary>
    public void ResetAwayWatch()
    {
        _away = false;
        _noFaceSince = null;
    }

    private void ArmSession()
    {
        ResetTracking();
        _armed = true;
    }

    private void ResetTracking()
    {
        _armed = _lost = _covered = _away = _frameDark = false;
        _darkSince = _faceSince = _noFaceSince = null;
        _retry.Stop();
    }

    /// <param name="silent">
    /// True for the background retry while the camera is off: no "Checking..." text, no problem
    /// changes, and no security-log spam if it fails again.
    /// </param>
    private async Task<bool> StartAndWaitAsync(bool silent)
    {
        if (_camera is null)
        {
            if (!silent) { Problem = "The camera isn't set up."; Notify(); }
            return false;
        }
        if (_busy) return false;
        _busy = true;

        if (!silent)
        {
            IsChecking = true;
            Problem = null;
            Notify();
        }
        try
        {
            await Task.Run(_camera.Stop);                   // fresh start (an old thread may be dead or hung); off the UI thread so buttons don't freeze
            var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _firstFrame = first;
            _sawDark = false;
            _started = true;
            _lastFrame = DateTime.Now;
            _camera.Start();
            _watchdog.Start();

            // "ready" = the first NORMAL-looking frame. Some webcams send black frames while they warm up,
            // so dark frames are simply waited out.
            var winner = await Task.WhenAny(first.Task, Task.Delay(8000));
            if (winner == first.Task && first.Task.Result)
            {
                Log?.Security("Camera on");
                return true;
            }

            if (!silent)
            {
                if (Problem is null && _sawDark)
                {
                    Problem = _armed ? CoveredMessage : CoveredAtStartMessage;
                    Log?.Security("Camera covered (no usable picture)");
                }
                else
                {
                    Problem ??= NoCameraMessage;
                    if (Problem == NoCameraMessage) Log?.Security("Camera failed to open");
                }
            }
            await StopHardwareAsync();
            return false;
        }
        finally
        {
            _busy = false;
            IsChecking = false;
            _firstFrame = null;
            Notify();
        }
    }

    // every few seconds while the camera is off during a session: quietly try to open it
    private async Task RetryTickAsync()
    {
        if (!_lost || !_armed || IsOn || _busy || !_holders.Contains("session")) return;
        try { await StartAndWaitAsync(silent: true); }
        catch (Exception ex) { Log?.Info($"WARNING: camera retry failed: {ex.Message}"); }
    }

    // same as StopHardware, but the (possibly slow) camera stop runs off the UI thread
    private async Task StopHardwareAsync()
    {
        _started = false;
        _watchdog.Stop();
        IsOn = false;
        Frame = null;
        var cam = _camera;
        if (cam != null) await Task.Run(cam.Stop);
    }

    private void StopHardware()
    {
        _started = false;
        _watchdog.Stop();
        _camera?.Stop();
        IsOn = false;
        Frame = null;
    }

    private void StopCamera()
    {
        if (_started) Log?.Security("Camera off");
        StopHardware();
        FaceText = "";
        FaceCount = 0;
        FaceRatio = 0;
        ResetTracking();
    }

    // camera thread -> UI thread
    private void OnFrameBg(FaceFrame f) =>
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () => OnFrame(f));

    private void OnFailedBg(string message) =>
        _dispatcher.BeginInvoke(() => OnFailed(message));

    private void OnFrame(FaceFrame f)
    {
        if (!_started) return;                              // late frame after we stopped
        using var ms = new MemoryStream(f.Jpeg);
        var image = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        image.Freeze();

        var now = DateTime.Now;
        bool dark = f.Brightness < MonitorTuning.CoveredBrightness;
        _frameDark = dark;

        Frame = image;
        FaceCount = dark ? 0 : f.FaceCount;
        FaceRatio = f.FaceRatio;
        FaceText = dark ? "Camera looks covered" : f.FaceFound ? $"Face found: {f.FaceRatio:P0} of frame" : "No face found";
        _lastFrame = now;
        IsOn = true;
        Problem = _covered ? CoveredMessage : null;
        Notify();

        if (!dark) _firstFrame?.TrySetResult(true);
        else if (_firstFrame != null) _sawDark = true;

        TrackState(f, now);
    }

    /// <summary>Works out covered / away / back from each frame. Only runs once a session has begun.</summary>
    private void TrackState(FaceFrame f, DateTime now)
    {
        if (!_armed || !_holders.Contains("session")) return;

        bool dark = f.Brightness < MonitorTuning.CoveredBrightness;

        if (dark)
        {
            _darkSince ??= now;
            _faceSince = null;                              // black frames say nothing about faces
            _noFaceSince = null;
            if (!_covered && !_lost && now - _darkSince.Value >= MonitorTuning.CoveredAfter)
            {
                _covered = true;
                _away = false;
                Problem = CoveredMessage;
                Notify();
                Log?.Security("Camera covered");
                CameraCovered?.Invoke();
            }
            return;
        }

        _darkSince = null;

        if (f.FaceFound)
        {
            _noFaceSince = null;
            _faceSince ??= now;
            if (now - _faceSince.Value < MonitorTuning.FaceBackAfter) return;

            if (_covered || _lost)
            {
                // normal frames AND a face for a moment: the camera really is back
                _covered = false;
                _lost = false;
                _retry.Stop();
                Problem = null;
                Notify();
                Log?.Security("Camera restored");
                CameraRestored?.Invoke();
            }
            else if (_away)
            {
                _away = false;
                Log?.Info("Face back");
                FaceBack?.Invoke();
            }
        }
        else
        {
            _faceSince = null;
            if (_covered || _lost) return;                  // still waiting for a face, not "away"
            _noFaceSince ??= now;
            if (!_away && now - _noFaceSince.Value >= MonitorTuning.AwayAfter)
            {
                _away = true;
                Log?.Info("Away: no face");
                AwayStarted?.Invoke();
            }
        }
    }

    private void OnFailed(string message)
    {
        if (!_started) return;
        // model-file problems already say exactly what is wrong; camera problems get a friendly reminder
        bool modelProblem = message.StartsWith("Face model");
        if (modelProblem) Log?.Security("Face model missing");
        HandleGone(modelProblem ? message : IsOn ? StoppedMessage : NoCameraMessage);
    }

    private void HandleGone(string problem)
    {
        bool wasOn = IsOn;
        IsOn = false;
        Frame = null;
        Problem = problem;
        Notify();

        _firstFrame?.TrySetResult(false);
        if (wasOn && _armed && !_lost && _holders.Contains("session"))
        {
            _lost = true;
            _covered = false;
            _away = false;
            _darkSince = _faceSince = _noFaceSince = null;
            Log?.Security("Camera lost during session");
            _retry.Start();                                 // quietly try again every few seconds
            CameraLost?.Invoke();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}