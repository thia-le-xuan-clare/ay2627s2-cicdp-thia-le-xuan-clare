using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class PostureMonitorTests : IDisposable
{
    private const double Baseline = 0.25;               // calibrated face fills 25% of the frame
    private const double BigFace = Baseline * 1.4;      // 40% bigger than calibration: clearly leaning in

    private readonly WpfApplicationFixture _wpf;
    private readonly FakeCalibrationStore _store = new() { Saved = new CalibrationData(Baseline, new DateTime(2026, 11, 13, 9, 0, 0)) };
    private readonly RecordingLog _log = new();
    private readonly List<string> _events = new();

    public PostureMonitorTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(() =>
        {
            WpfState.Reset();
            Posture.Store = _store;
            Posture.Log = _log;
            Posture.SlouchStarted += () => _events.Add("started");
            Posture.CountdownTick += seconds => _events.Add($"tick:{seconds}");
            Posture.CountdownExpired += () => _events.Add("expired");
            Posture.SlouchEnded += () => _events.Add("ended");
        });
    }

    public void Dispose() => _wpf.Invoke(WpfState.Reset);

    private static PostureMonitor Posture => PostureMonitor.Instance;

    // ---- helpers (all of them run on the WPF thread, inside _wpf.Invoke) ----

    private void StartSession()
    {
        AppServices.Use(_store);
        Assert.True(TimeTracker.Instance.Start());
        _store.LoadCalls = 0;       // Start() looked at the calibration too; only count what PostureMonitor loads
    }

    private static void Face(double ratio, int count = 1)
    {
        var camera = CameraMonitor.Instance;
        Private.SetProperty(camera, "IsOn", true);
        Private.SetProperty(camera, "FaceCount", count);
        Private.SetProperty(camera, "FaceRatio", ratio);
    }

    private static void Check() => Private.Call(Posture, "Check");

    private static void LeanedInFor(TimeSpan time) => Private.Set(Posture, "_bigSince", DateTime.Now - time);

    /// <summary>A running session, a big face for longer than SlouchAfter, and one Check: the warning is now showing.</summary>
    private void Slouch()
    {
        StartSession();
        Face(BigFace);
        Check();                                                    // loads the calibration, starts the "too big" clock
        LeanedInFor(PostureTuning.SlouchAfter + TimeSpan.FromSeconds(1));
        Check();
        Assert.True(Posture.IsSlouching);
    }

    // ---------- tuning numbers ----------

    [Fact]
    public void Tuning_RecoverFactorIsBelowSlouchFactor_SoTheWarningDoesNotFlicker()
    {
        Assert.True(PostureTuning.SlouchFactor > 1.0);
        Assert.True(PostureTuning.RecoverFactor > 1.0);
        Assert.True(PostureTuning.RecoverFactor < PostureTuning.SlouchFactor);
    }

    // ---------- nothing happens unless a session is really counting ----------

    [Fact]
    public void Check_WhenNoSessionIsRunning_DoesNothing()
    {
        _wpf.Invoke(() =>
        {
            Face(BigFace);
            LeanedInFor(TimeSpan.FromMinutes(1));

            Check();

            Assert.False(Posture.IsSlouching);
            Assert.Equal(0, _store.LoadCalls);
        });

        Assert.Empty(_events);
    }

    [Fact]
    public void Check_WhenNeverCalibrated_NeverWarns()
    {
        _wpf.Invoke(() =>
        {
            StartSession();
            _store.Saved = null;                            // calibrated for Start(), "never calibrated" for the posture check
            Face(BigFace);
            LeanedInFor(TimeSpan.FromMinutes(1));

            Check();

            Assert.False(Posture.IsSlouching);
            Assert.Equal(1, _store.LoadCalls);
        });

        Assert.Empty(_events);
    }

    // ---------- deciding that someone is leaning in ----------

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.19)]
    public void Check_FaceBelowTheSlouchFactor_NeverWarns_EvenAfterALongTime(double timesCalibrated)
    {
        _wpf.Invoke(() =>
        {
            StartSession();
            Face(Baseline * timesCalibrated);
            LeanedInFor(TimeSpan.FromMinutes(5));

            Check();

            Assert.False(Posture.IsSlouching);
            Assert.Null(Private.Get<DateTime?>(Posture, "_bigSince"));      // dropping under the line resets the clock
        });

        Assert.Empty(_events);
    }

    [Theory]
    [InlineData(1.21)]
    [InlineData(2.00)]
    public void Check_FaceAboveTheSlouchFactor_ForLongEnough_StartsTheWarningAndTheCountdown(double timesCalibrated)
    {
        _wpf.Invoke(() =>
        {
            StartSession();
            Face(Baseline * timesCalibrated);
            Check();
            LeanedInFor(PostureTuning.SlouchAfter + TimeSpan.FromSeconds(1));

            Check();

            Assert.True(Posture.IsSlouching);
        });

        var expectedStart = (int)PostureTuning.Countdown.TotalSeconds;
        Assert.Equal(new[] { "started", $"tick:{expectedStart}" }, _events);
        Assert.Contains(_log.InfoLines, l => l.StartsWith("Posture: slouching"));
    }

    [Fact]
    public void Check_FaceBigButNotForLongEnough_DoesNotWarnYet()
    {
        _wpf.Invoke(() =>
        {
            StartSession();
            Face(BigFace);
            Check();
            LeanedInFor(PostureTuning.SlouchAfter - TimeSpan.FromSeconds(2));

            Check();

            Assert.False(Posture.IsSlouching);
        });

        Assert.Empty(_events);
    }

    // ---------- the countdown ----------

    [Fact]
    public void Check_WhileSlouching_TheCountdownTicksDownAsTimePasses()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            Private.Set(Posture, "_slouchStart", DateTime.Now - TimeSpan.FromSeconds(5.5));

            Check();
        });

        Assert.Equal("tick:15", _events.Last());
    }

    [Fact]
    public void Check_WhenTheCountdownReachesZero_RaisesExpiredExactlyOnce()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            Private.Set(Posture, "_slouchStart", DateTime.Now - PostureTuning.Countdown - TimeSpan.FromSeconds(1));

            Check();
            Check();
        });

        Assert.Equal(1, _events.Count(e => e == "expired"));
        Assert.Equal("tick:0", _events[^2]);
        Assert.Equal("expired", _events[^1]);
        Assert.Contains("Posture: countdown finished, still slouching", _log.InfoLines);
    }

    // ---------- sitting up again ----------

    [Fact]
    public void Check_SittingUpForLongEnough_ClearsTheWarning()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            Face(Baseline);
            Private.Set(Posture, "_smooth", Baseline);      // skip the smoothing lag
            Private.Set(Posture, "_normalSince", DateTime.Now - PostureTuning.SitUpAfter - TimeSpan.FromSeconds(1));

            Check();

            Assert.False(Posture.IsSlouching);
        });

        Assert.Equal("ended", _events.Last());
        Assert.Contains("Posture: warning cleared (sat up)", _log.InfoLines);
    }

    [Fact]
    public void Check_BetweenTheRecoverAndSlouchFactors_KeepsTheWarning()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            var inBetween = Baseline * 1.10;                // under 1.20 but still over 1.08
            Face(inBetween);
            Private.Set(Posture, "_smooth", inBetween);
            Private.Set(Posture, "_normalSince", DateTime.Now - TimeSpan.FromMinutes(1));

            Check();

            Assert.True(Posture.IsSlouching);
        });

        Assert.DoesNotContain("ended", _events);
    }

    // ---------- the session ends while the warning is up ----------

    [Fact]
    public void Check_WhenTheSessionStopsWhileSlouching_ClearsTheWarning()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            TimeTracker.Instance.Stop();

            Check();

            Assert.False(Posture.IsSlouching);
        });

        Assert.Equal("ended", _events.Last());
        Assert.Contains("Posture: warning cleared (session not active)", _log.InfoLines);
    }

    [Fact]
    public void Check_WhenTheTimerIsPausedWhileSlouching_ClearsTheWarning()
    {
        _wpf.Invoke(() =>
        {
            Slouch();
            TimeTracker.Instance.TogglePause();

            Check();

            Assert.False(Posture.IsSlouching);
        });

        Assert.Equal("ended", _events.Last());
    }

}
