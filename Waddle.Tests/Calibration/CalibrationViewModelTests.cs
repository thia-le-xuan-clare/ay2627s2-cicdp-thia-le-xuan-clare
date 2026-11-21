using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

// CameraMonitor is a singleton that owns the real camera, so these tests give it a FakeCameraService and set
// what it "sees" (IsOn, FaceCount, FaceRatio) directly. Everything touching it runs on the WPF thread.
[Collection(WpfCollection.Name)]
public sealed class CalibrationViewModelTests : IDisposable
{
  private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCalibrationStore _store = new();
  private readonly RecordingLog _log = new();
  private readonly FakeCameraService _camera = new();
  private CalibrationViewModel? _vm;

  public CalibrationViewModelTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;

    // A start left running by an earlier test keeps CameraMonitor "busy" for up to 8 seconds, and while it is busy
    // every new start returns false straight away. Wait for it to finish BEFORE wiring up this test's fake camera
    // and log, so its late "Camera failed to open" line can't land in this test's log.
    var until = DateTime.UtcNow.AddSeconds(12);
    while (_wpf.Query(() => Private.Get<bool>(CameraMonitor.Instance, "_busy")) && DateTime.UtcNow < until)
      Thread.Sleep(50);

    Assert.False(
        _wpf.Query(() => Private.Get<bool>(CameraMonitor.Instance, "_busy")),
        "CameraMonitor was still busy from an earlier test.");

    _wpf.Invoke(() =>
    {
      WpfState.Reset();
      CameraMonitor.Instance.Camera = _camera;
      CameraMonitor.Instance.Log = _log;
    });
  }

  public void Dispose() => _wpf.Invoke(() =>
  {
    _vm?.Dispose();
    var monitor = CameraMonitor.Instance;
    monitor.Shutdown();
    monitor.Camera = null;
    monitor.Log = null;
    Private.SetProperty(monitor, "Problem", null);
    WpfState.Reset();
  });

  private CalibrationViewModel Create() => _wpf.Query(() => _vm = new CalibrationViewModel(_log, _store));

  private static void Sees(bool cameraOn, int faces, double ratio = 0.3)
  {
    var monitor = CameraMonitor.Instance;
    Private.SetProperty(monitor, "IsOn", cameraOn);
    Private.SetProperty(monitor, "FaceCount", faces);
    Private.SetProperty(monitor, "FaceRatio", ratio);
  }

  /// <summary>Makes the monitor tell its listeners something changed (the real one does this after every frame).</summary>
  private static void MonitorChanged() => Private.Call(CameraMonitor.Instance, "Notify");

  // ---- opening ----

  [Fact]
  public void FirstRun_GoesStraightToTheLiveCameraScreen()
  {
    var vm = Create();

    Assert.True(vm.IsFirstRun);
    Assert.Equal(CalibrationStep.Live, vm.Step);
    Assert.True(vm.IsLive);
    Assert.False(vm.IsAlreadyCalibrated);
    Assert.False(vm.IsCounting);
    Assert.Equal("Welcome to Waddle!", vm.TitleText);
    Assert.Equal("Let's calibrate your face size. Please sit up straight.", vm.SubtitleText);
  }

  [Fact]
  public void WhenCalibratedBefore_ShowsTheAlreadyCalibratedScreen()
  {
    _store.Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 30, 0));

    var vm = Create();

    Assert.False(vm.IsFirstRun);
    Assert.Equal(CalibrationStep.AlreadyCalibrated, vm.Step);
    Assert.True(vm.IsAlreadyCalibrated);
    Assert.False(vm.IsLive);
    Assert.Equal("Please sit up straight", vm.TitleText);
    Assert.Contains("Last calibrated", vm.LastCalibratedText);
    Assert.Contains("2026", vm.LastCalibratedText);
  }

  [Fact]
  public async Task StartAsync_WhenAlreadyCalibrated_LeavesTheCameraAlone()
  {
    _store.Saved = new CalibrationData(0.25, DateTime.Now);
    var vm = Create();

    await _wpf.Query(() => vm.StartAsync()).WaitAsync(Patience);

    Assert.Equal(0, _camera.StartCalls);
    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
    Assert.Empty(_log.SecurityLines);
  }

  [Fact]
  public async Task StartAsync_OnFirstRun_TurnsTheCameraOnAndShowsWhatItSees()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame(faceCount: 1));
    var vm = Create();

    await _wpf.Query(() => vm.StartAsync()).WaitAsync(Patience);

    Assert.Equal(1, _camera.StartCalls);
    Assert.True(_wpf.Query(() => CameraMonitor.Instance.IsOn));
    Assert.True(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
    Assert.Equal("Face detected. ", vm.StatusTitle);
    Assert.Equal("Please sit up straight then press Scan.", vm.StatusDetail);
    Assert.True(vm.ScanCommand.CanExecute(null));
    Assert.Equal("Scan", CalibrationViewModel.ScanText);
    Assert.Contains("Camera access requested for calibration", _log.SecurityLines);
    Assert.Contains("Camera on", _log.SecurityLines);
  }

  [Fact]
  public async Task StartAsync_WhenTheCameraCantOpen_ShowsTheProblemAndOffersTryAgain()
  {
    _camera.FailureOnStart = "boom";
    var vm = Create();

    await _wpf.Query(() => vm.StartAsync()).WaitAsync(Patience);

    Assert.Contains("camera is off", vm.StatusTitle);
    Assert.Equal("Try again", CalibrationViewModel.ScanText);
    Assert.True(vm.ScanCommand.CanExecute(null));          // the same button becomes "Try again"
    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
  }

  [Fact]
  public async Task TryAgain_WhenTheCameraIsOff_StartsTheCameraAgain()
  {
    _camera.FailureOnStart = "boom";
    var vm = Create();
    await _wpf.Query(() => vm.StartAsync()).WaitAsync(Patience);

    _camera.FailureOnStart = null;
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame(faceCount: 1));
    await _wpf.Query(() => vm.ScanCommand.ExecuteAsync(null)).WaitAsync(Patience);

    Assert.Equal(2, _camera.StartCalls);
    Assert.Equal(CalibrationStep.Live, vm.Step);             // pressing "Try again" does not start a countdown
    Assert.Equal("Face detected. ", vm.StatusTitle);
  }

  [Fact]
  public async Task Rescan_FromTheAlreadyCalibratedScreen_ShowsTheLiveCameraAndStartsIt()
  {
    _store.Saved = new CalibrationData(0.25, DateTime.Now);
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame(faceCount: 1));
    var vm = Create();
    var changed = new List<string?>();
    vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    await _wpf.Query(() => vm.RescanCommand.ExecuteAsync(null)).WaitAsync(Patience);

    Assert.Equal(CalibrationStep.Live, vm.Step);
    Assert.False(vm.IsAlreadyCalibrated);
    Assert.True(vm.IsLive);
    Assert.Contains(nameof(CalibrationViewModel.IsAlreadyCalibrated), changed);
    Assert.Equal(1, _camera.StartCalls);
  }

  // ---- the status line ----

  [Theory]
  [InlineData(true, 0, null, false, "Face is not detected")]
  [InlineData(true, 1, null, false, "Face detected. ")]
  [InlineData(true, 2, null, false, "More than one face detected")]
  [InlineData(false, 0, null, false, "Starting camera...")]
  [InlineData(false, 0, null, true, "Checking your camera...")]
  [InlineData(false, 0, "Camera is broken", false, "Camera is broken")]
  public void StatusTitle_FollowsWhatTheCameraMonitorReports(bool cameraOn, int faces, string? problem, bool checking, string expected)
  {
    var vm = Create();

    _wpf.Invoke(() =>
    {
      Sees(cameraOn, faces);
      Private.SetProperty(CameraMonitor.Instance, "Problem", problem);
      Private.SetProperty(CameraMonitor.Instance, "IsChecking", checking);
      MonitorChanged();
    });

    Assert.Equal(expected, vm.StatusTitle);
  }

  [Fact]
  public void WhenTheMonitorChanges_TheScanButtonTextIsRefreshed()
  {
    var vm = Create();
    var changed = new List<string?>();
    vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    _wpf.Invoke(() =>
    {
      Sees(cameraOn: true, faces: 1);
      MonitorChanged();
    });

    Assert.Contains(nameof(CalibrationViewModel.ScanText), changed);
  }

  [Fact]
  public void StatusLine_IsLeftAloneOnceCalibrationIsDone()
  {
    var vm = Create();
    _wpf.Invoke(() =>
    {
      Sees(cameraOn: true, faces: 1);
      MonitorChanged();
    });

    vm.Step = CalibrationStep.Done;
    vm.StatusTitle = "Calibrated!";
    _wpf.Invoke(() =>
    {
      Sees(cameraOn: true, faces: 2);
      MonitorChanged();
    });

    Assert.Equal("Calibrated!", vm.StatusTitle);
  }

  // ---- when Scan is allowed ----

  [Theory]
  [InlineData(true, 1, null, false, true)]                  // exactly one face: go
  [InlineData(true, 0, null, false, false)]                 // nobody there
  [InlineData(true, 2, null, false, false)]                 // two people
  [InlineData(true, 1, null, true, false)]                  // still checking the camera
  [InlineData(false, 0, null, false, false)]                // camera still starting
  [InlineData(false, 0, "Camera is broken", false, true)]   // camera has a problem: the button is "Try again"
  public void CanScan_OnlyWithOneFace_OrWhenTheButtonIsTryAgain(bool cameraOn, int faces, string? problem, bool checking, bool allowed)
  {
    var vm = Create();

    _wpf.Invoke(() =>
    {
      Sees(cameraOn, faces);
      Private.SetProperty(CameraMonitor.Instance, "Problem", problem);
      Private.SetProperty(CameraMonitor.Instance, "IsChecking", checking);
    });

    Assert.Equal(allowed, vm.ScanCommand.CanExecute(null));
  }

  [Fact]
  public void CanScan_IsFalseOnTheAlreadyCalibratedScreen()
  {
    _store.Saved = new CalibrationData(0.25, DateTime.Now);
    var vm = Create();
    _wpf.Invoke(() => Sees(cameraOn: true, faces: 1));

    Assert.False(vm.ScanCommand.CanExecute(null));
  }

  // ---- scanning ----

  [Fact]
  public async Task Scan_WithOneFace_SavesTheAverageFaceSizeAndClosesTheWindow()
  {
    var vm = Create();
    var closed = false;
    _wpf.Invoke(() =>
    {
      vm.CloseRequested += () => closed = true;
      Sees(cameraOn: true, faces: 1, ratio: 0.3);
    });

    var scan = _wpf.Query(() => vm.ScanCommand.ExecuteAsync(null));
    await scan.WaitAsync(Patience);

    Assert.NotNull(_store.Saved);
    Assert.Equal(0.3, _store.Saved.FaceRatio, 3);
    Assert.Equal(CalibrationStep.Done, vm.Step);
    Assert.Equal("Calibrated!", vm.StatusTitle);
    Assert.True(closed);
    Assert.Contains(_log.InfoLines, line => line.StartsWith("Calibration saved", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Scan_ShowsTheCountdown_WhileItHoldsStill()
  {
    var vm = Create();
    _wpf.Invoke(() => Sees(cameraOn: true, faces: 1));

    var scan = _wpf.Query(() => vm.ScanCommand.ExecuteAsync(null));
    await Task.Delay(300);

    Assert.Equal(CalibrationStep.Counting, vm.Step);
    Assert.True(vm.IsCounting);
    Assert.Equal(3, vm.Countdown);
    Assert.Equal("Hold still...", vm.StatusTitle);

    await scan.WaitAsync(Patience);
  }

  [Theory]
  [InlineData(0, "Face is not detected")]
  [InlineData(2, "More than one face detected")]
  public async Task Scan_StopsAndSavesNothing_WhenTheFaceCountChangesDuringTheCountdown(int facesNow, string expectedStatus)
  {
    var vm = Create();
    var closed = false;
    _wpf.Invoke(() =>
    {
      vm.CloseRequested += () => closed = true;
      Sees(cameraOn: true, faces: 1);
    });

    var scan = _wpf.Query(() => vm.ScanCommand.ExecuteAsync(null));
    await Task.Delay(300);
    _wpf.Invoke(() => Sees(cameraOn: true, faces: facesNow));
    await scan.WaitAsync(Patience);

    Assert.Null(_store.Saved);
    Assert.False(closed);
    Assert.Equal(CalibrationStep.Live, vm.Step);
    Assert.Equal(expectedStatus, vm.StatusTitle);
  }

  [Fact]
  public async Task Scan_SavesNothing_IfTheWindowIsClosedDuringTheCountdown()
  {
    var vm = Create();
    var closed = false;
    _wpf.Invoke(() =>
    {
      vm.CloseRequested += () => closed = true;
      Sees(cameraOn: true, faces: 1);
    });

    var scan = _wpf.Query(() => vm.ScanCommand.ExecuteAsync(null));
    await Task.Delay(300);
    _wpf.Invoke(() => vm.Dispose());
    await scan.WaitAsync(Patience);

    Assert.Null(_store.Saved);
    Assert.False(closed);
  }

  // ---- leaving ----

  [Fact]
  public void Exit_AsksTheWindowToClose()
  {
    var vm = Create();
    var closed = false;
    vm.CloseRequested += () => closed = true;

    vm.ExitCommand.Execute(null);

    Assert.True(closed);
  }

  [Fact]
  public async Task Dispose_GivesTheCameraBack_AndStopsListeningToIt()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame(faceCount: 1));
    var vm = Create();
    await _wpf.Query(() => vm.StartAsync()).WaitAsync(Patience);
    Assert.True(_wpf.Query(() => CameraMonitor.Instance.HasHolders));

    _wpf.Invoke(() => vm.Dispose());

    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
    Assert.False(_wpf.Query(() => CameraMonitor.Instance.IsOn));
    Assert.False(_camera.IsRunning);
    Assert.Contains("Camera off", _log.SecurityLines);

    var title = vm.StatusTitle;
    _wpf.Invoke(() =>
    {
      Sees(cameraOn: true, faces: 2);
      MonitorChanged();
    });
    Assert.Equal(title, vm.StatusTitle);                    // no longer listening
  }

  [Fact]
  public void Dispose_CanBeCalledTwice()
  {
    var vm = Create();

    _wpf.Invoke(() =>
    {
      vm.Dispose();
      vm.Dispose();
    });

    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
  }
}
