using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

internal sealed class ExplodingCalibrationStore : ICalibrationStore
{
  public CalibrationData? Load() => throw new InvalidOperationException("boom");

  public void Save(CalibrationData data) { }
}

// The dashboard. IMPORTANT: closing a MainWindow shuts the whole application down (that is what the real app
// wants), which would kill the shared WPF test thread, so these tests only ever Hide() it. Windows the dashboard
// opens (widget, settings, calibration) are closed in Dispose.
[Collection(WpfCollection.Name)]
public sealed class MainWindowTests : IDisposable
{
  private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

  private readonly WpfApplicationFixture _wpf;
  private readonly StartupTestScope _startup = new();
  private readonly FakeCalibrationStore _calibration = new() { Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 0, 0)) };
  private readonly FakeDailyTimeStore _history = new();
  private readonly FakeWarningEventStore _warnings = new();
  private readonly RecordingLog _log = new();
  private readonly FakeCameraService _camera = new();
  private readonly List<MainWindow> _dashboards = new();

  public MainWindowTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(() =>
    {
      WpfState.Reset();
      ResetCamera();
      CameraMonitor.Instance.Camera = _camera;
    });
  }

  public void Dispose()
  {
    _wpf.Invoke(() =>
    {
      foreach (var dashboard in _dashboards) dashboard.Hide();                 // never Close(): see the note above
      foreach (var window in Application.Current.Windows.OfType<Window>()
                   .Where(w => w is WidgetWindow or SettingsWindow or CalibrationWindow)
                   .ToList())
        window.Close();
      ResetCamera();
      WpfState.Reset();
    });
    _startup.Dispose();
  }

  private static TimeTracker Tracker => TimeTracker.Instance;

  private static void ResetCamera()
  {
    var camera = CameraMonitor.Instance;
    camera.Shutdown();
    camera.Camera = null;
    Private.SetProperty(camera, "Problem", null);
  }

  private MainWindow Create(ICalibrationStore? calibration = null)
  {
    ICalibrationStore calibrationStore = calibration ?? _calibration;
    return _wpf.Query(() =>
    {
      var services = new ServiceCollection();
      services.AddSingleton(calibrationStore);
      services.AddSingleton<IDailyTimeStore>(_history);
      services.AddSingleton<IWarningEventStore>(_warnings);
      services.AddSingleton<ILogService>(_log);
      services.AddSingleton(_ => new CalibrationWindow(new CalibrationViewModel(_log, _calibration)));
      typeof(App).GetProperty(nameof(App.Services))!.SetValue(null, services.BuildServiceProvider());

      var window = new MainWindow(new MainViewModel(_log));
      _dashboards.Add(window);
      return window;
    });
  }

  private static T Part<T>(MainWindow w, string name) => (T)w.FindName(name);

  private static void Click(MainWindow w, string handler) =>
      Private.Call(w, handler, null, new RoutedEventArgs());

  private static int Count<T>() where T : Window => Application.Current.Windows.OfType<T>().Count();

  private Task Run(MainWindow window, string method) => _wpf.Query(() => (Task)Private.Call(window, method)!);

  // ---- building the dashboard ----

  [Fact]
  public void Constructor_WiresUpTheViewModelAndThePanels()
  {
    var window = Create();

    _wpf.Invoke(() =>
    {
      Assert.IsType<MainViewModel>(window.DataContext);
      Assert.IsType<HistoryCalendarViewModel>(Part<Border>(window, "HistoryPanel").DataContext);
      Assert.IsType<DayStatsViewModel>(Part<Border>(window, "StatsPanel").DataContext);
      Assert.IsType<DuckAnimator>(Part<Viewbox>(window, "DuckPanel").DataContext);
    });
  }

  [Fact]
  public void TheDuck_FollowsTheTimersMood()
  {
    var window = Create();
    var duck = _wpf.Query(() => Private.Get<DuckAnimator>(window, "_duck"));
    Assert.Equal(DuckEmotion.Sad, _wpf.Query(() => duck.Emotion));            // nothing recorded today

    _wpf.Invoke(() => Tracker.IsBadPosture = true);
    Assert.Equal(DuckEmotion.Angry, _wpf.Query(() => duck.Emotion));

    _wpf.Invoke(() => Tracker.IsBadPosture = false);
  }

  [Fact]
  public async Task ResizingTheWindow_SwitchesBetweenTheNarrowAndWideLayouts()
  {
    var window = Create();
    _wpf.Invoke(() => window.Show());

    _wpf.Invoke(() => window.Width = 800);
    Assert.True(await _wpf.WaitUntilAsync(() => Grid.GetRow(Part<Border>(window, "HistoryPanel")) == 1, 5000), "never went narrow");
    Assert.Equal(new GridLength(0), _wpf.Query(() => Part<ColumnDefinition>(window, "RightCol").Width));
    Assert.Equal(HorizontalAlignment.Center, _wpf.Query(() => Part<Border>(window, "HistoryPanel").HorizontalAlignment));

    _wpf.Invoke(() => window.Width = 1300);
    Assert.True(await _wpf.WaitUntilAsync(() => Grid.GetRow(Part<Border>(window, "HistoryPanel")) == 0, 5000), "never went wide");
    Assert.Equal(GridLength.Auto, _wpf.Query(() => Part<ColumnDefinition>(window, "RightCol").Width));
    Assert.Equal(1, _wpf.Query(() => Grid.GetColumn(Part<Border>(window, "HistoryPanel"))));
  }

  // ---- starting a session ----

  [Fact]
  public async Task StartingASession_WithAWorkingCamera_StartsTheTimerAndOpensTheWidget()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    var window = Create();

    await Run(window, "StartSessionAsync").WaitAsync(Patience);

    Assert.True(_wpf.Query(() => Tracker.IsRunning));
    Assert.True(_wpf.Query(() => Tracker.IsWidgetOpen));
    Assert.Equal(1, _wpf.Query(Count<WidgetWindow>));

    await Run(window, "StartSessionAsync").WaitAsync(Patience);              // already running: just shows the widget
    Assert.Equal(1, _wpf.Query(Count<WidgetWindow>));
  }

  [Fact]
  public async Task StartingASession_WithNoCamera_DoesNotStartTheTimer()
  {
    _wpf.Invoke(() => CameraMonitor.Instance.Camera = null);
    var window = Create();

    await Run(window, "StartSessionAsync").WaitAsync(Patience);

    Assert.False(_wpf.Query(() => Tracker.IsRunning));
    Assert.Equal(0, _wpf.Query(Count<WidgetWindow>));
    Assert.Equal("The camera isn't set up.", _wpf.Query(() => CameraMonitor.Instance.Problem));
  }

  [Fact]
  public async Task StartButton_StartsASession()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    var window = Create();

    _wpf.Invoke(() => Click(window, "Start_Click"));

    Assert.True(await _wpf.WaitUntilAsync(() => Tracker.IsRunning, 8000), "the timer never started");
  }

  [Fact]
  public async Task TryAgain_WhenNothingIsRunning_StartsASession()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    var window = Create();

    _wpf.Invoke(() => Click(window, "Retry_Click"));

    Assert.True(await _wpf.WaitUntilAsync(() => Tracker.IsRunning, 8000), "the timer never started");
  }

  [Fact]
  public void TryAgain_WhileRunning_JustAsksTheCameraToRetry()
  {
    var window = Create();
    _wpf.Invoke(() =>
    {
      Assert.True(Tracker.Start());

      Click(window, "Retry_Click");

      Assert.True(Tracker.IsRunning);
    });
  }

  // ---- auto-start ----

  [Fact]
  public async Task AutoStart_WhenTurnedOff_DoesNothing()
  {
    var window = Create();

    await Run(window, "AutoStartTimerIfEnabledAsync").WaitAsync(Patience);

    Assert.False(_wpf.Query(() => Tracker.IsRunning));
  }

  [Fact]
  public async Task AutoStart_WhenTurnedOn_StartsTheTimer()
  {
    StartupManager.SetAutoStartTimer(true);
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    var window = Create();

    await Run(window, "AutoStartTimerIfEnabledAsync").WaitAsync(Patience);

    Assert.True(_wpf.Query(() => Tracker.IsRunning));
  }

  [Fact]
  public async Task AutoStart_WhenSomethingGoesWrong_LogsItInsteadOfCrashing()
  {
    StartupManager.SetAutoStartTimer(true);
    var window = Create(new ExplodingCalibrationStore());

    await Run(window, "AutoStartTimerIfEnabledAsync").WaitAsync(Patience);

    Assert.Contains(_log.InfoLines, line => line.StartsWith("Auto-start of the posture timer failed: boom", StringComparison.Ordinal));
    Assert.False(_wpf.Query(() => Tracker.IsRunning));
  }

  // ---- the idle reminder and the duck ----

  [Fact]
  public void IdleReminder_WhenNothingIsRunning_OpensTheReminderWidget()
  {
    var window = Create();

    _wpf.Invoke(() => window.ShowIdleReminder());

    Assert.Equal(1, _wpf.Query(Count<WidgetWindow>));
    var widget = _wpf.Query(() => Application.Current.Windows.OfType<WidgetWindow>().Single());
    Assert.Equal(WidgetMode.Reminder, _wpf.Query(() => Private.Get<WidgetMode>(widget, "_mode")));
    Assert.True(_wpf.Query(() => Tracker.IsWidgetOpen));

    _wpf.Invoke(() => window.ShowIdleReminder());                              // already open: no second widget
    Assert.Equal(1, _wpf.Query(Count<WidgetWindow>));
  }

  [Fact]
  public void IdleReminder_WhileATimerIsRunning_StaysAway()
  {
    var window = Create();
    _wpf.Invoke(() =>
    {
      Assert.True(Tracker.Start());

      window.ShowIdleReminder();

      Assert.Equal(0, Count<WidgetWindow>());
    });
  }

  [Fact]
  public void IdleReminder_WhileAnOverlayIsOpen_StaysAway()
  {
    var window = Create();
    _wpf.Invoke(() =>
    {
      OverlayState.Enter();
      try
      {
        window.ShowIdleReminder();

        Assert.Equal(0, Count<WidgetWindow>());
      }
      finally { OverlayState.Exit(); }
    });
  }

  [Fact]
  public async Task Slouching_TurnsTheWidgetsDuckAngry_EvenIfItOpensLater()
  {
    _camera.FrameOnStart = _wpf.Query(() => FakeCameraService.Frame());
    var window = Create();
    _wpf.Invoke(() => window.SetSlouching(true));                              // no widget yet

    await Run(window, "StartSessionAsync").WaitAsync(Patience);

    var widget = _wpf.Query(() => Application.Current.Windows.OfType<WidgetWindow>().Single());
    var duck = _wpf.Query(() => Private.Get<DuckAnimator>(widget, "_duck"));
    Assert.Equal(DuckEmotion.Angry, _wpf.Query(() => duck.Emotion));

    _wpf.Invoke(() => window.SetSlouching(false));
    Assert.NotEqual(DuckEmotion.Angry, _wpf.Query(() => duck.Emotion));
  }

  // ---- buttons ----

  [Fact]
  public void PauseAndStopButtons_ControlTheSharedTimer()
  {
    var window = Create();
    _wpf.Invoke(() =>
    {
      Assert.True(Tracker.Start());

      Click(window, "Pause_Click");
      Assert.True(Tracker.IsPaused);

      Click(window, "Pause_Click");
      Assert.False(Tracker.IsPaused);

      Click(window, "Stop_Click");
      Assert.False(Tracker.IsRunning);
    });
  }

  [Fact]
  public void RecalibrateButton_OpensTheWidgetInCalibrateMode()
  {
    var window = Create();

    _wpf.Invoke(() => Click(window, "Recalibrate_Click"));

    var widget = _wpf.Query(() => Application.Current.Windows.OfType<WidgetWindow>().Single());
    Assert.Equal(WidgetMode.Calibrate, _wpf.Query(() => Private.Get<WidgetMode>(widget, "_mode")));
  }

  [Fact]
  public void CalibrateButton_OpensTheCalibrationOverlay()
  {
    var window = Create();

    _wpf.Invoke(() => Click(window, "Calibrate_Click"));

    Assert.Equal(1, _wpf.Query(Count<CalibrationWindow>));
  }

  [Fact]
  public void SettingsButton_OpensOneSettingsOverlay_AndReopensAfterItIsClosed()
  {
    var window = Create();

    _wpf.Invoke(() => Click(window, "Settings_Click"));
    _wpf.Invoke(() => Click(window, "Settings_Click"));                        // already open: it is brought forward
    Assert.Equal(1, _wpf.Query(Count<SettingsWindow>));

    _wpf.Invoke(() => Application.Current.Windows.OfType<SettingsWindow>().Single().Close());
    Assert.Equal(0, _wpf.Query(Count<SettingsWindow>));

    _wpf.Invoke(() => Click(window, "Settings_Click"));
    Assert.Equal(1, _wpf.Query(Count<SettingsWindow>));
  }
}
