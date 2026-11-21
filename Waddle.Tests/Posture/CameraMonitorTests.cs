using Waddle.Wpf;

namespace Waddle.Tests;

// CameraMonitor decides "covered / lost / away / back" from timestamps. The tests do not wait for the real
// delays: they feed frames straight into OnFrame and move the private "since" timestamps into the past.
[Collection(WpfCollection.Name)]
public sealed class CameraMonitorTests : IDisposable
{
  private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCameraService _camera = new();
  private readonly RecordingLog _log = new();
  private readonly List<string> _events = new();

  public CameraMonitorTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(() =>
    {
      WpfState.Reset();
      ResetMonitor();
      var monitor = Monitor;
      monitor.Camera = _camera;
      monitor.Log = _log;
      monitor.CameraLost += () => _events.Add("lost");
      monitor.CameraCovered += () => _events.Add("covered");
      monitor.CameraRestored += () => _events.Add("restored");
      monitor.AwayStarted += () => _events.Add("away");
      monitor.FaceBack += () => _events.Add("back");
    });
  }

  public void Dispose() => _wpf.Invoke(() =>
  {
    ResetMonitor();
    WpfState.Reset();
  });

  private static CameraMonitor Monitor => CameraMonitor.Instance;

  private static void ResetMonitor()
  {
    var monitor = Monitor;
    foreach (var name in new[] { "CameraLost", "CameraCovered", "CameraRestored", "AwayStarted", "FaceBack" })
      Private.ClearEvent(monitor, name);
    monitor.Shutdown();                                   // drops every holder and stops the camera
    monitor.Camera = null;
    monitor.Log = null;
    Private.SetProperty(monitor, "Problem", null);
  }

  // ---- helpers ----

  private Task<bool> Acquire(string owner) => _wpf.Query(() => Monitor.AcquireAsync(owner));

  private async Task StartSession()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    Assert.True(await Acquire("session").WaitAsync(Patience));
    _events.Clear();
  }

  private void Feed(int faces = 1, double brightness = 200) =>
      _wpf.Invoke(() => Private.Call(Monitor, "OnFrame", FakeCameraService.Frame(faces, 0.3, brightness)));

  /// <summary>Pretends the named timestamp (_darkSince, _faceSince, _noFaceSince) began this long ago.</summary>
  private void Since(string field, TimeSpan ago) =>
      _wpf.Invoke(() => Private.Set(Monitor, field, DateTime.Now - ago));

  private bool Query(Func<CameraMonitor, bool> read) => _wpf.Query(() => read(Monitor));

  // ---- turning the camera on and off ----

  [Fact]
  public async Task Acquire_WithNoCamera_FailsAndSaysWhy()
  {
    _wpf.Invoke(() => Monitor.Camera = null);

    var ok = await Acquire("calibration").WaitAsync(Patience);

    Assert.False(ok);
    Assert.Equal("The camera isn't set up.", _wpf.Query(() => Monitor.Problem));
    Assert.True(Query(m => m.HasProblem));
    Assert.True(Query(m => m.HasBanner));
    Assert.False(Query(m => m.HasHolders));
  }

  [Fact]
  public async Task Acquire_WithAWorkingCamera_TurnsItOnAndShowsTheFrame()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame(faceCount: 1, faceRatio: 0.3));

    var ok = await Acquire("session").WaitAsync(Patience);

    Assert.True(ok);
    Assert.True(Query(m => m.IsOn));
    Assert.True(Query(m => m.HasFrame));
    Assert.True(Query(m => m.HasHolders));
    Assert.False(Query(m => m.HasOverlay));
    Assert.False(Query(m => m.HasBanner));
    Assert.Equal(1, _wpf.Query(() => Monitor.FaceCount));
    Assert.Equal(0.3, _wpf.Query(() => Monitor.FaceRatio), 3);
    Assert.Contains("Face found", _wpf.Query(() => Monitor.FaceText));
    Assert.Contains("Camera on", _log.SecurityLines);
  }

  [Fact]
  public async Task Acquire_WhileAlreadyOn_ReusesTheRunningCamera()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    await Acquire("session").WaitAsync(Patience);

    var ok = await Acquire("calibration").WaitAsync(Patience);

    Assert.True(ok);
    Assert.Equal(1, _camera.StartCalls);
  }

  [Fact]
  public async Task Release_KeepsTheCameraOn_UntilTheLastHolderLeaves()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    await Acquire("session").WaitAsync(Patience);
    await Acquire("calibration").WaitAsync(Patience);

    _wpf.Invoke(() => Monitor.Release("calibration"));
    Assert.True(Query(m => m.IsOn));
    Assert.True(_camera.IsRunning);

    _wpf.Invoke(() => Monitor.Release("session"));
    Assert.False(Query(m => m.IsOn));
    Assert.False(Query(m => m.HasFrame));
    Assert.False(Query(m => m.HasHolders));
    Assert.False(_camera.IsRunning);
    Assert.Equal(0, _wpf.Query(() => Monitor.FaceCount));
    Assert.Contains("Camera off", _log.SecurityLines);
  }

  [Fact]
  public async Task Acquire_WhenTheCameraFailsToOpen_ReportsItAndLeavesTheReminderShowing()
  {
    _camera.FailureOnStart = "boom";

    var ok = await Acquire("session").WaitAsync(Patience);

    Assert.False(ok);
    Assert.Contains("camera is off", _wpf.Query(() => Monitor.Problem));
    Assert.False(Query(m => m.ProblemIsCovered));
    Assert.False(Query(m => m.IsOn));
    Assert.False(Query(m => m.HasHolders));
    Assert.False(Query(m => m.IsChecking));
    Assert.Contains("Camera failed to open", _log.SecurityLines);
  }

  [Fact]
  public async Task Acquire_WhenTheFaceModelIsMissing_ShowsTheModelMessage()
  {
    _camera.FailureOnStart = "Face model not found: haarcascade_frontalface_default.xml";

    var ok = await Acquire("session").WaitAsync(Patience);

    Assert.False(ok);
    Assert.Equal(_camera.FailureOnStart, _wpf.Query(() => Monitor.Problem));
    Assert.Contains("Face model missing", _log.SecurityLines);
  }

  [Fact]
  public async Task Retry_WithoutAnyHolder_DoesNothing()
  {
    var ok = await _wpf.Query(() => Monitor.RetryAsync()).WaitAsync(Patience);

    Assert.False(ok);
    Assert.Equal(0, _camera.StartCalls);
  }

  [Fact]
  public async Task Retry_WithAHolder_StartsTheCameraAgain()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    await Acquire("session").WaitAsync(Patience);

    var ok = await _wpf.Query(() => Monitor.RetryAsync()).WaitAsync(Patience);

    Assert.True(ok);
    Assert.Equal(2, _camera.StartCalls);
  }

  [Fact]
  public async Task Shutdown_DropsEveryHolder_AndStopsTheCamera()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    await Acquire("session").WaitAsync(Patience);

    _wpf.Invoke(() => Monitor.Shutdown());

    Assert.False(Query(m => m.HasHolders));
    Assert.False(Query(m => m.IsOn));
    Assert.False(_camera.IsRunning);
  }

  [Fact]
  public async Task ReplacingTheCamera_MovesTheMonitorOverToTheNewOne()
  {
    var other = new FakeCameraService();
    other.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    _wpf.Invoke(() => Monitor.Camera = other);

    var ok = await Acquire("session").WaitAsync(Patience);

    Assert.True(ok);
    Assert.Equal(1, other.StartCalls);
    Assert.Equal(0, _camera.StartCalls);
  }

  // ---- frames ----

  [Fact]
  public void Frame_BeforeTheCameraWasStarted_IsIgnored()
  {
    Feed();

    Assert.False(Query(m => m.HasFrame));
    Assert.False(Query(m => m.IsOn));
  }

  [Fact]
  public async Task Frame_WithNoFace_SaysSo()
  {
    await StartSession();

    Feed(faces: 0);

    Assert.Equal("No face found", _wpf.Query(() => Monitor.FaceText));
    Assert.Equal(0, _wpf.Query(() => Monitor.FaceCount));
  }

  [Fact]
  public async Task Frame_ThatIsNearlyBlack_CountsAsNoFaces()
  {
    await StartSession();

    Feed(faces: 1, brightness: 5);

    Assert.Equal("Camera looks covered", _wpf.Query(() => Monitor.FaceText));
    Assert.Equal(0, _wpf.Query(() => Monitor.FaceCount));
  }

  // ---- covered ----

  [Fact]
  public async Task Session_LensCoveredForAWhile_RaisesCameraCovered_ThenRestoredWhenAFaceIsBack()
  {
    await StartSession();

    Feed(brightness: 5);
    Assert.Empty(_events);                                  // one black frame is not enough

    Since("_darkSince", TimeSpan.FromSeconds(5));
    Feed(brightness: 5);
    Assert.Equal(new[] { "covered" }, _events);
    Assert.True(Query(m => m.ProblemIsCovered));
    Assert.Contains("Camera covered", _log.SecurityLines);

    Feed();                                                 // uncovered, but the face has not been seen long enough
    Assert.Equal(new[] { "covered" }, _events);

    Since("_faceSince", TimeSpan.FromSeconds(3));
    Feed();
    Assert.Equal(new[] { "covered", "restored" }, _events);
    Assert.Null(_wpf.Query(() => Monitor.Problem));
    Assert.Contains("Camera restored", _log.SecurityLines);
  }

  [Fact]
  public async Task Covered_IsReportedOnlyOnce()
  {
    await StartSession();
    Since("_darkSince", TimeSpan.FromSeconds(5));

    Feed(brightness: 5);
    Feed(brightness: 5);

    Assert.Equal(new[] { "covered" }, _events);
  }

  [Fact]
  public async Task Covered_WithoutASession_IsNotTracked()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    await Acquire("calibration").WaitAsync(Patience);        // only a session watches for covered / away
    Since("_darkSince", TimeSpan.FromSeconds(5));

    Feed(brightness: 5);

    Assert.Empty(_events);
  }

  // ---- away ----

  [Fact]
  public async Task Session_NoFaceForAWhile_RaisesAwayStarted_ThenFaceBack()
  {
    await StartSession();

    Feed(faces: 0);
    Assert.Empty(_events);                                  // just looked away

    Since("_noFaceSince", TimeSpan.FromSeconds(13));
    Feed(faces: 0);
    Assert.Equal(new[] { "away" }, _events);
    Assert.Contains("Away: no face", _log.InfoLines);

    Feed();                                                 // a face, but not for long enough yet
    Assert.Equal(new[] { "away" }, _events);

    Since("_faceSince", TimeSpan.FromSeconds(3));
    Feed();
    Assert.Equal(new[] { "away", "back" }, _events);
    Assert.Contains("Face back", _log.InfoLines);
  }

  [Fact]
  public async Task Away_IsReportedOnlyOnce()
  {
    await StartSession();
    Since("_noFaceSince", TimeSpan.FromSeconds(13));

    Feed(faces: 0);
    Feed(faces: 0);

    Assert.Equal(new[] { "away" }, _events);
  }

  [Fact]
  public async Task ResetAwayWatch_ForgetsThatTheUserWasAway()
  {
    await StartSession();
    Since("_noFaceSince", TimeSpan.FromSeconds(13));
    Feed(faces: 0);

    _wpf.Invoke(() => Monitor.ResetAwayWatch());
    Since("_faceSince", TimeSpan.FromSeconds(3));
    Feed();

    Assert.Equal(new[] { "away" }, _events);                // no "back": the timer was resumed by hand
  }

  // ---- camera lost ----

  [Fact]
  public async Task Session_CameraFailsMidway_RaisesCameraLost_ThenRestoredWhenAFaceIsBack()
  {
    await StartSession();

    _wpf.Invoke(() => Private.Call(Monitor, "OnFailed", "device unplugged"));

    Assert.Equal(new[] { "lost" }, _events);
    Assert.Contains("camera stopped", _wpf.Query(() => Monitor.Problem));
    Assert.False(Query(m => m.IsOn));
    Assert.Contains("Camera lost during session", _log.SecurityLines);

    Since("_faceSince", TimeSpan.FromSeconds(3));
    Feed();
    Assert.Equal(new[] { "lost", "restored" }, _events);
    Assert.True(Query(m => m.IsOn));
  }

  [Fact]
  public async Task Lost_IsReportedOnlyOnce()
  {
    await StartSession();

    _wpf.Invoke(() => Private.Call(Monitor, "OnFailed", "first"));
    _wpf.Invoke(() => Private.Call(Monitor, "OnFailed", "second"));

    Assert.Equal(new[] { "lost" }, _events);
  }

  [Fact]
  public async Task Lost_QuietRetryBringsTheCameraBackWithoutAnyPopup()
  {
    await StartSession();
    _wpf.Invoke(() => Private.Call(Monitor, "OnFailed", "device unplugged"));
    _events.Clear();
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());

    await _wpf.Query(() => (Task)Private.Call(Monitor, "RetryTickAsync")!).WaitAsync(Patience);

    Assert.True(Query(m => m.IsOn));
    Assert.Empty(_events);                                  // still waiting for a face before it is "restored"

    Since("_faceSince", TimeSpan.FromSeconds(3));
    Feed();
    Assert.Equal(new[] { "restored" }, _events);
  }

  [Fact]
  public async Task QuietRetry_DoesNothing_WhenTheCameraWasNeverLost()
  {
    await StartSession();
    var startsBefore = _camera.StartCalls;

    await _wpf.Query(() => (Task)Private.Call(Monitor, "RetryTickAsync")!).WaitAsync(Patience);

    Assert.Equal(startsBefore, _camera.StartCalls);
  }

  [Fact]
  public void Failure_BeforeAnythingStarted_IsIgnored()
  {
    _wpf.Invoke(() => Private.Call(Monitor, "OnFailed", "device unplugged"));

    Assert.Null(_wpf.Query(() => Monitor.Problem));
    Assert.Empty(_events);
  }
}
