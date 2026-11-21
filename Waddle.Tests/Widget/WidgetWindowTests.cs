using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

// The floating timer widget. It remembers its place and size in static fields, so every test forgets them first
// and last; otherwise one test's window would decide where the next one opens.
[Collection(WpfCollection.Name)]
public sealed class WidgetWindowTests : IDisposable
{
  private static readonly string[] SavedPlaceFields = { "_left", "_top", "_width", "_height", "_lastBig" };

  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCalibrationStore _calibration = new() { Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 0, 0)) };

  public WidgetWindowTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(() =>
    {
      WpfState.Reset();
      ResetCamera();
    });
    ForgetSavedPlace();
  }

  public void Dispose()
  {
    _wpf.Invoke(() =>
    {
      ResetCamera();
      WpfState.Reset();
    });
    ForgetSavedPlace();
  }

  private static void ForgetSavedPlace()
  {
    foreach (var name in SavedPlaceFields)
      typeof(WidgetWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
  }

  private static void RememberPlace(string field, object value) =>
      typeof(WidgetWindow).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);

  private static void ResetCamera()
  {
    var camera = CameraMonitor.Instance;
    camera.Shutdown();
    camera.Camera = null;
    Private.SetProperty(camera, "Problem", null);
  }

  /// <summary>Runs the body on the WPF thread with a widget that is created but not shown.</summary>
  private void WithWidget(Action<WidgetWindow, Func<bool>> test) => _wpf.Invoke(() =>
  {
    var window = new WidgetWindow();
    var closed = false;
    window.Closed += (_, _) => closed = true;
    try { test(window, () => closed); }
    finally { if (!closed) window.Close(); }
  });

  private static T Part<T>(WidgetWindow w, string name) => (T)w.FindName(name);

  private static void Click(WidgetWindow w, string handler, object? sender = null) =>
      Private.Call(w, handler, sender, new RoutedEventArgs());

  private void StartSession()
  {
    AppServices.Use(_calibration);
    Assert.True(TimeTracker.Instance.Start());
  }

  // ---- opening ----

  [Fact]
  public void FirstTime_OpensBig_WithTheCameraRowShowing()
  {
    WithWidget((w, _) =>
    {
      Assert.Equal(300.0, w.Width);
      Assert.Equal(370.0, w.Height);
      Assert.Equal("Make smaller", Part<Button>(w, "SizeButton").ToolTip);
      Assert.Equal(Visibility.Collapsed, Part<Viewbox>(w, "DuckPanel").Visibility);
      Assert.Equal(new GridLength(1, GridUnitType.Star), Part<RowDefinition>(w, "CameraRow").Height);
    });
  }

  [Fact]
  public void FirstTime_OpensInTheBottomRightOfTheWorkArea()
  {
    WithWidget((w, _) =>
    {
      var area = SystemParameters.WorkArea;

      Assert.Equal(area.Right - w.Width - 16, w.Left);
      Assert.Equal(area.Bottom - w.Height - 16, w.Top);
    });
  }

  [Fact]
  public void WhenTheSavedPlaceIsOffScreen_FallsBackToTheBottomRight()
  {
    RememberPlace("_left", -99999.0);                       // e.g. the monitor it was on was unplugged
    RememberPlace("_top", -99999.0);

    WithWidget((w, _) =>
    {
      var area = SystemParameters.WorkArea;

      Assert.Equal(area.Right - w.Width - 16, w.Left);
      Assert.Equal(area.Bottom - w.Height - 16, w.Top);
    });
  }

  [Fact]
  public void ClosingAShownWidget_MakesTheNextOneOpenWhereItWas_AtTheSameSize()
  {
    var (left, top) = _wpf.Query(() =>
    {
      var first = new WidgetWindow();
      first.Show();
      first.Left = 123;
      first.Top = 87;
      Click(first, "Size_Click");                            // collapse it
      var place = (first.Left, first.Top);
      first.Close();
      return place;
    });

    _wpf.Invoke(() =>
    {
      var second = new WidgetWindow();
      try
      {
        Assert.Equal(left, second.Left);
        Assert.Equal(top, second.Top);
        Assert.Equal("Make bigger", Part<Button>(second, "SizeButton").ToolTip);
        Assert.Equal(Visibility.Visible, Part<Viewbox>(second, "DuckPanel").Visibility);
      }
      finally { second.Close(); }
    });
  }

  // ---- modes ----

  [Fact]
  public void ReminderMode_ShowsTheMessageAndTheStartButton_InTheSmallForm()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Reminder);

      Assert.Equal(Visibility.Visible, Part<TextBlock>(w, "ReminderText").Visibility);
      Assert.Equal(Visibility.Visible, Part<Button>(w, "StartButton").Visibility);
      Assert.Equal(Visibility.Collapsed, Part<StackPanel>(w, "SessionPanel").Visibility);
      Assert.Equal(Visibility.Collapsed, Part<Grid>(w, "Controls").Visibility);
      Assert.Equal(Visibility.Visible, Part<Viewbox>(w, "DuckPanel").Visibility);
      Assert.Equal(260.0, w.Width);
      Assert.Equal(180.0, w.Height);
    });
  }

  [Fact]
  public void LeavingReminderMode_GoesBackToTheSizeItHadBefore()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Reminder);

      w.SetMode(WidgetMode.Session);

      Assert.Equal(Visibility.Visible, Part<StackPanel>(w, "SessionPanel").Visibility);
      Assert.Equal(Visibility.Collapsed, Part<TextBlock>(w, "ReminderText").Visibility);
      Assert.Equal(Visibility.Collapsed, Part<Button>(w, "StartButton").Visibility);
      Assert.Equal(300.0, w.Width);
      Assert.Equal(370.0, w.Height);
    });
  }

  [Fact]
  public void ShowReminderMessage_OnlyWorksWhileTheReminderIsShowing()
  {
    WithWidget((w, _) =>
    {
      var reminder = Part<TextBlock>(w, "ReminderText");
      var original = reminder.Text;
      Assert.False(w.ShowReminderMessage("Checking your camera..."));

      w.SetMode(WidgetMode.Reminder);
      Assert.True(w.ShowReminderMessage("Checking your camera..."));
      Assert.Equal("Checking your camera...", reminder.Text);

      Assert.True(w.ShowReminderMessage(null));
      Assert.Equal(original, reminder.Text);

      w.ShowReminderMessage("Camera is off");
      w.SetMode(WidgetMode.Reminder);                        // a fresh reminder never keeps an old message
      Assert.Equal(original, reminder.Text);
    });
  }

  [Fact]
  public void CalibrateMode_ShowsTheCalibrationText_AndMakesTheWidgetBig()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Reminder);

      w.SetMode(WidgetMode.Calibrate);

      Assert.Equal(Visibility.Visible, Part<TextBlock>(w, "CalText").Visibility);
      Assert.Equal(Visibility.Collapsed, Part<StackPanel>(w, "SessionPanel").Visibility);
      Assert.Equal(300.0, w.Width);
    });
  }

  [Fact]
  public async Task CalibrateMode_BorrowsTheCamera_AndGivesItBackWhenTheWidgetCloses()
  {
    var camera = new FakeCameraService();
    WidgetWindow? widget = null;
    _wpf.Invoke(() =>
    {
      camera.FrameOnStart = FakeCameraService.Frame();
      CameraMonitor.Instance.Camera = camera;
      widget = new WidgetWindow();
      widget.SetMode(WidgetMode.Calibrate);
    });

    Assert.True(await _wpf.WaitUntilAsync(() => CameraMonitor.Instance.IsOn), "camera never turned on");
    Assert.True(_wpf.Query(() => CameraMonitor.Instance.HasHolders));

    _wpf.Invoke(() => widget!.Close());

    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
    Assert.False(camera.IsRunning);
  }

  [Fact]
  public void SessionMode_GivesBackTheCalibrationCamera()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Calibrate);

      w.SetMode(WidgetMode.Session);

      Assert.False(CameraMonitor.Instance.HasHolders);
    });
  }

  // ---- duck ----

  [Fact]
  public void SetSlouching_TurnsTheDuckAngry_AndSittingUpCalmsIt()
  {
    WithWidget((w, _) =>
    {
      var duck = Private.Get<DuckAnimator>(w, "_duck");

      w.SetSlouching(true);
      Assert.Equal(DuckEmotion.Angry, duck.Emotion);

      w.SetSlouching(false);
      Assert.NotEqual(DuckEmotion.Angry, duck.Emotion);
    });
  }

  [Fact]
  public void ReminderMode_MakesTheDuckSad()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Reminder);

      Assert.Equal(DuckEmotion.Sad, Private.Get<DuckAnimator>(w, "_duck").Emotion);
    });
  }

  // ---- buttons ----

  [Fact]
  public void SizeButton_TogglesBetweenBigAndSmall_InSessionMode()
  {
    WithWidget((w, _) =>
    {
      Click(w, "Size_Click");
      Assert.Equal(260.0, w.Width);
      Assert.Equal(180.0, w.Height);
      Assert.Equal("Make bigger", Part<Button>(w, "SizeButton").ToolTip);

      Click(w, "Size_Click");
      Assert.Equal(300.0, w.Width);
      Assert.Equal("Make smaller", Part<Button>(w, "SizeButton").ToolTip);
    });
  }

  [Fact]
  public void SizeButton_DoesNothing_OutsideSessionMode()
  {
    WithWidget((w, _) =>
    {
      w.SetMode(WidgetMode.Reminder);

      Click(w, "Size_Click");

      Assert.Equal(260.0, w.Width);                          // the reminder needs the duck, so it stays small
    });
  }

  [Fact]
  public void CloseButton_ClosesTheWidget_ButTheTimerKeepsRunning()
  {
    WithWidget((w, closed) =>
    {
      StartSession();

      Click(w, "Close_Click");

      Assert.True(closed());
      Assert.True(TimeTracker.Instance.IsRunning);
    });
  }

  [Fact]
  public void PauseButton_PausesAndResumesTheSharedTimer()
  {
    WithWidget((w, _) =>
    {
      StartSession();

      Click(w, "Pause_Click");
      Assert.True(TimeTracker.Instance.IsPaused);

      Click(w, "Pause_Click");
      Assert.False(TimeTracker.Instance.IsPaused);
    });
  }

  [Fact]
  public void StartButton_RaisesStartRequested()
  {
    WithWidget((w, _) =>
    {
      var raised = 0;
      w.StartRequested += () => raised++;

      Click(w, "Start_Click");

      Assert.Equal(1, raised);
    });
  }

  [Fact]
  public void StopButton_AsksToConfirmFirst_ThenStopsTheTimerAndClosesOnTheSecondClick()
  {
    WithWidget((w, closed) =>
    {
      StartSession();
      var button = new Button { Content = "Stop" };

      Click(w, "Stop_Click", button);
      Assert.Equal("Confirm stop?", button.Content);
      Assert.True(TimeTracker.Instance.IsRunning);
      Assert.False(closed());

      Click(w, "Stop_Click", button);
      Assert.Equal("Stop", button.Content);
      Assert.False(TimeTracker.Instance.IsRunning);
      Assert.True(closed());
    });
  }

  [Fact]
  public async Task StopButton_GoesBackToNormal_IfNotConfirmedWithinThreeSeconds()
  {
    WidgetWindow? widget = null;
    Button? button = null;
    _wpf.Invoke(() =>
    {
      StartSession();
      button = new Button { Content = "Stop" };
      widget = new WidgetWindow();
      Click(widget, "Stop_Click", button);
    });
    Assert.Equal("Confirm stop?", _wpf.Query(() => button!.Content));

    Assert.True(await _wpf.WaitUntilAsync(() => (string)button!.Content == "Stop", 6000), "the button never went back");

    Assert.True(_wpf.Query(() => TimeTracker.Instance.IsRunning));
    _wpf.Invoke(() => widget!.Close());
  }

  [Fact]
  public void StopHandler_WithoutAButton_StopsTheTimerAndCloses()
  {
    WithWidget((w, closed) =>
    {
      StartSession();

      Click(w, "Stop_Click");

      Assert.False(TimeTracker.Instance.IsRunning);
      Assert.True(closed());
    });
  }

  [Fact]
  public void TryAgain_InSessionMode_AsksTheCameraToRetry_ButInCalibrateModeAcquiresIt()
  {
    WithWidget((w, _) =>
    {
      Click(w, "Retry_Click");                               // nothing holds the camera: nothing to retry
      Assert.Null(CameraMonitor.Instance.Problem);

      w.SetMode(WidgetMode.Calibrate);
      Private.SetProperty(CameraMonitor.Instance, "Problem", null);
      Click(w, "Retry_Click");

      Assert.Equal("The camera isn't set up.", CameraMonitor.Instance.Problem);   // no camera plugged into the test
    });
  }
}
