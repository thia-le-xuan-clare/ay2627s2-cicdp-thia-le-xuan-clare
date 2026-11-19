using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
  private WidgetWindow? _widget;
  private bool _starting;
  private SettingsWindow? _settings;          // the Settings overlay while it is open
  private bool _slouching;                    // so a widget opened while slouching starts with the angry duck
  private readonly DuckAnimator _duck = new();   // the animated duck next to the timer

  public MainWindow(MainViewModel vm)
  {
    InitializeComponent();
    DataContext = vm;

    // dashboard duck: plays the animation that matches the time sat up today
    DuckPanel.DataContext = _duck;
    _duck.Force(TimeTracker.Instance.MoodEmotion);
    _duck.Start();
    TimeTracker.Instance.PropertyChanged += (_, _) => _duck.Force(TimeTracker.Instance.MoodEmotion);

    // history grid, shown on the dashboard
    var calendar = new HistoryCalendarViewModel(App.Services.GetRequiredService<IDailyTimeStore>());
    HistoryPanel.DataContext = calendar;

    // pie charts: today until a calendar day is clicked
    var stats = new DayStatsViewModel(App.Services.GetRequiredService<IWarningEventStore>());
    StatsPanel.DataContext = stats;
    calendar.DaySelectionChanged += stats.ShowDay;

    // start with today's square already picked: done once the window has loaded, so the squares exist on screen
    RoutedEventHandler? pickToday = null;
    pickToday = (_, _) =>
    {
      Loaded -= pickToday;
      calendar.SelectToday();
    };
    Loaded += pickToday;

    // first load only: if the user asked for it, start the posture timer by itself
    RoutedEventHandler? onFirstLoad = null;
    onFirstLoad = async (_, _) =>
    {
      Loaded -= onFirstLoad;
      await AutoStartTimerIfEnabledAsync();
    };
    Loaded += onFirstLoad;

    var statsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
    statsTimer.Tick += (_, _) => stats.RefreshIfToday();     // new slouches show up without clicking
    statsTimer.Start();

    // closing the dashboard ends the app: save the running session, free the camera, exit
    Closed += (_, _) =>
    {
      _duck.Stop();
      TimeTracker.Instance.Stop();
      CameraMonitor.Instance.Shutdown();
      Application.Current.Shutdown();
    };
  }

  // The view model commands set Status; these Click handlers do the window work.
  private async void Start_Click(object sender, RoutedEventArgs e) => await StartSessionAsync();

  private void Recalibrate_Click(object sender, RoutedEventArgs e) => OpenWidget(WidgetMode.Calibrate);

  private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();
  private void Stop_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.ConfirmAndStop();

  // Settings is a full-screen overlay like the calibration one. It is shown with Show(), NOT ShowDialog():
  // a modal dialog locks every other window in the app, which froze the floating widget so its close (X)
  // button couldn't be clicked. The overlay covers the widget instead, and counts in OverlayState so the idle
  // reminder stays away while it is open.
  private void Settings_Click(object sender, RoutedEventArgs e)
  {
    if (_settings is not null) { _settings.Activate(); return; }

    _settings = new SettingsWindow();
    _settings.Closed += (_, _) => _settings = null;
    _settings.Show();
  }

  /// <summary>"Start posture timer automatically on launch": runs once, when the dashboard first finishes loading.</summary>
  private async Task AutoStartTimerIfEnabledAsync()
  {
    if (!StartupManager.IsAutoStartTimerEnabled()) return;
    try
    {
      await StartSessionAsync();      // same path as the Start button: calibration check, camera check, then the timer
    }
    catch (Exception ex)
    {
      App.Services.GetRequiredService<ILogService>().Info($"Auto-start of the posture timer failed: {ex.Message}");
    }
  }

  private void Calibrate_Click(object sender, RoutedEventArgs e) =>
      App.Services.GetRequiredService<CalibrationWindow>().Show();

  // "Try again" on the camera reminder
  private async void Retry_Click(object sender, RoutedEventArgs e)
  {
    if (TimeTracker.Instance.IsRunning || CameraMonitor.Instance.HasHolders)
      await CameraMonitor.Instance.RetryAsync();      // if the timer was auto-paused it resumes by itself
    else
      await StartSessionAsync();                      // the first start failed, so start again
  }

  // The camera is checked BEFORE the timer starts: no camera, no timer.
  // Calibration is checked FIRST, then the camera, then the timer: no calibration, no camera, no timer.
  private async Task StartSessionAsync()
  {
    if (TimeTracker.Instance.IsRunning) { OpenWidget(WidgetMode.Session); return; }
    if (_starting) return;
    _starting = true;
    try
    {
      if (!App.EnsureCalibrated("Calibrate your face size first, then Waddle can start your timer."))
        return;     // skipped: no camera, no timer, no widget

      _widget?.ShowReminderMessage("Checking your camera...");     // only does anything while the reminder is showing
      if (!await CameraMonitor.Instance.AcquireAsync("session"))
      {
        ShowStartFailedBanner();    // the dashboard reminder explains why; the pop-up banner says it too
        return;
      }
      BannerService.Instance.Clear(BannerKind.CameraOff);     // the camera works now: drop any earlier start-failed pop-up
      BannerService.Instance.Clear(BannerKind.Covered);

      if (!TimeTracker.Instance.Start())          // backstop: blocked for any other reason
      {
        CameraMonitor.Instance.Release("session");
        return;
      }

      OpenWidget(WidgetMode.Session);
    }
    finally { _starting = false; }
  }

  /// <summary>The camera check failed before the timer could start: show the pop-up banner with a Try again button.</summary>
  private void ShowStartFailedBanner()
  {
    var camera = CameraMonitor.Instance;
    if (!camera.HasProblem)             // e.g. a double click while the camera was still being checked
    {
      _widget?.ShowReminderMessage(null);
      return;
    }

    // the idle reminder widget is open: say it right there instead of adding a second pop-up
    var onWidget = camera.ProblemIsCovered
        ? "Camera looks covered. Uncover it, then tap Start session."
        : "Camera is off or can't be opened. Turn it on, then tap Start session.";
    if (_widget?.ShowReminderMessage(onWidget) == true) return;

    var banner = BannerService.Instance;
    var (kind, text) = camera.ProblemIsCovered
        ? (BannerKind.Covered, "Camera is covered")
        : (BannerKind.CameraOff, "Camera is disabled");

    banner.ClearAll();                  // a fresh banner each try, even if the last one was dismissed
    banner.Show(kind, text, "Try again", onButtonAsync: StartSessionAsync, nudge: false);
  }

  /// <summary>Called when posture goes bad / good: the widget's duck turns angry (or back).</summary>
  public void SetSlouching(bool slouching)
  {
    _slouching = slouching;
    _widget?.SetSlouching(slouching);
  }

  /// <summary>Called by IdleReminder: pops the widget up with the "start again" message and the sad duck.</summary>
  public void ShowIdleReminder()
  {
    if (TimeTracker.Instance.IsRunning || OverlayState.IsActive) return;
    OpenWidget(WidgetMode.Reminder);
  }

  private void OpenWidget(WidgetMode mode)
  {
    if (_widget == null)
    {
      _widget = new WidgetWindow();           // deliberately NOT owned by this window
      _widget.StartRequested += async () => await StartSessionAsync();   // "Start session" in the idle reminder
      _widget.Closed += (_, _) =>
      {
        _widget = null;
        TimeTracker.Instance.IsWidgetOpen = false;   // dashboard Pause/Stop + camera appear
        TimeTracker.Instance.IsWidgetCollapsed = false;
      };
      TimeTracker.Instance.IsWidgetCollapsed = false;
      TimeTracker.Instance.IsWidgetOpen = true;        // dashboard Pause/Stop + camera hide
      _widget.Show();                         // ShowActivated=false: dashboard stays where it is
    }
    _widget.SetMode(mode);
    if (mode == WidgetMode.Session) _widget.SetSlouching(_slouching);
  }

  private void Row3Grid_SizeChanged(object sender, SizeChangedEventArgs e)
  {
    bool isNarrow = e.NewSize.Width < 820;

    if (isNarrow)
    {
      // Narrow: Pie charts on top (Row 0), Calendar below (Row 1)
      RightCol.Width = new GridLength(0);

      Grid.SetRow(StatsPanel, 0);
      Grid.SetColumn(StatsPanel, 0);

      Grid.SetRow(HistoryPanel, 1);
      Grid.SetColumn(HistoryPanel, 0);
      HistoryPanel.HorizontalAlignment = HorizontalAlignment.Center;
    }
    else
    {
      // Wide: Stats on left (Col 0), Calendar on right (Col 1)
      RightCol.Width = GridLength.Auto;

      Grid.SetRow(StatsPanel, 0);
      Grid.SetColumn(StatsPanel, 0);

      Grid.SetRow(HistoryPanel, 0);
      Grid.SetColumn(HistoryPanel, 1);
      HistoryPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
    }
  }
}
