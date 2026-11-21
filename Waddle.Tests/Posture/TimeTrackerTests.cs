using Waddle.Core;
using Waddle.Wpf;
using System.IO;

namespace Waddle.Tests;

internal sealed class ThrowingDailyTimeStore : IDailyTimeStore
{
  public bool FailReads { get; set; }
  public bool FailWrites { get; set; }

  public int GetSeconds(DateOnly day) => FailReads ? throw new IOException("disk gone") : 0;

  public void SetSeconds(DateOnly day, int seconds)
  {
    if (FailWrites) throw new IOException("disk full");
  }

  public IReadOnlyDictionary<DateOnly, int> GetSecondsRange(DateOnly from, DateOnly to) => new Dictionary<DateOnly, int>();
}

// The posture-specific behaviour is in TimeTrackerPostureTests; this covers starting, stopping, pausing,
// the automatic pauses, saving to the store, and the text the dashboard shows.
[Collection(WpfCollection.Name)]
public sealed class TimeTrackerTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCalibrationStore _calibration = new() { Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 0, 0)) };
  private readonly FakeDailyTimeStore _store = new();
  private readonly RecordingLog _log = new();
  private readonly List<PauseReason> _reasons = new();

  public TimeTrackerTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(() =>
    {
      WpfState.Reset();
      Tracker.Store = _store;
      Tracker.Log = _log;
      Tracker.PauseReasonChanged += reason => _reasons.Add(reason);
    });
  }

  public void Dispose() => _wpf.Invoke(WpfState.Reset);

  private static TimeTracker Tracker => TimeTracker.Instance;

  private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

  private void StartSession()
  {
    AppServices.Use(_calibration);
    Assert.True(Tracker.Start());
  }

  private static void TickAfter(double seconds)
  {
    Private.Set(Tracker, "_lastTick", DateTime.Now - TimeSpan.FromSeconds(seconds));
    Private.Call(Tracker, "Tick");
  }

  // ---- start / stop ----

  [Fact]
  public void BeforeStarting_TheDashboardShowsTheIdleTexts()
  {
    _wpf.Invoke(() =>
    {
      Assert.False(Tracker.IsRunning);
      Assert.Equal("Not running", Tracker.StatusText);
      Assert.Equal("Start timer", Tracker.TimerButtonText);
      Assert.Equal("Pause", Tracker.PauseButtonText);
      Assert.Equal("00:00:00", Tracker.SessionText);
      Assert.False(Tracker.ShowDashboardControls);
    });
  }

  [Fact]
  public void Start_BeginsASession()
  {
    _wpf.Invoke(() =>
    {
      StartSession();

      Assert.True(Tracker.IsRunning);
      Assert.Equal("Timer running", Tracker.StatusText);
      Assert.Equal("Open widget", Tracker.TimerButtonText);
      Assert.True(Tracker.ShowDashboardControls);
      Assert.Contains("Timer started", _log.InfoLines);

      Tracker.IsWidgetOpen = true;                         // the widget has its own Pause / Stop
      Assert.False(Tracker.ShowDashboardControls);
    });
  }

  [Fact]
  public void Start_WhileAlreadyRunning_DoesNotStartASecondSession()
  {
    _wpf.Invoke(() =>
    {
      StartSession();

      Assert.True(Tracker.Start());

      Assert.Single(_log.InfoLines, line => line == "Timer started");
    });
  }

  [Fact]
  public void Stop_SavesTheTimeAndAnnouncesTheEndOfTheSession()
  {
    _wpf.Invoke(() =>
    {
      var ended = 0;
      Tracker.SessionEnded += () => ended++;
      StartSession();
      TickAfter(2);

      Tracker.Stop();

      Assert.False(Tracker.IsRunning);
      Assert.Equal(1, ended);
      Assert.True(_store.Data[Today] >= 1);
      Assert.Contains(_log.InfoLines, line => line.StartsWith("Timer stopped", StringComparison.Ordinal));

      Tracker.Stop();                                      // not running: nothing more happens
      Assert.Equal(1, ended);
    });
  }

  [Fact]
  public void ConfirmAndStop_OnlyStopsARunningTimer()
  {
    _wpf.Invoke(() =>
    {
      Assert.False(Tracker.ConfirmAndStop());

      StartSession();

      Assert.True(Tracker.ConfirmAndStop());
      Assert.False(Tracker.IsRunning);
    });
  }

  // ---- the user's own pause ----

  [Fact]
  public void TogglePause_PausesAndResumesByHand()
  {
    _wpf.Invoke(() =>
    {
      StartSession();

      Tracker.TogglePause();
      Assert.True(Tracker.IsPaused);
      Assert.Equal(PauseReason.User, Tracker.Reason);
      Assert.Equal("Paused", Tracker.StatusText);
      Assert.Equal("Resume", Tracker.PauseButtonText);
      Assert.Contains("Timer paused", _log.InfoLines);

      Tracker.TogglePause();
      Assert.False(Tracker.IsPaused);
      Assert.Equal("Pause", Tracker.PauseButtonText);
      Assert.Contains("Timer resumed", _log.InfoLines);
      Assert.Equal(new[] { PauseReason.User, PauseReason.None }, _reasons);
    });
  }

  [Fact]
  public void TogglePause_WhenNotRunning_DoesNothing()
  {
    _wpf.Invoke(() =>
    {
      Tracker.TogglePause();

      Assert.False(Tracker.IsPaused);
      Assert.Empty(_reasons);
    });
  }

  [Fact]
  public void TogglePause_WhenTheCameraIsOff_AsksToCheckTheCameraAgain()
  {
    _wpf.Invoke(() =>
    {
      var asked = 0;
      Tracker.RetryCameraRequested += () => asked++;
      StartSession();
      Tracker.AutoPause(PauseReason.CameraOff);

      Tracker.TogglePause();

      Assert.Equal(1, asked);
      Assert.Equal(PauseReason.CameraOff, Tracker.Reason);   // still paused until the camera is really back
    });
  }

  [Fact]
  public void TogglePause_WhenTheCameraIsCovered_WaitsForTheCameraToBeUncovered()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(PauseReason.Covered);

      Tracker.TogglePause();

      Assert.Equal(PauseReason.Covered, Tracker.Reason);
    });
  }

  [Fact]
  public void TogglePause_WhenAway_ResumesTheTimer()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(PauseReason.Away);

      Tracker.TogglePause();

      Assert.False(Tracker.IsPaused);
      Assert.Contains("Timer resumed: back from away", _log.InfoLines);
    });
  }

  // ---- automatic pauses ----

  [Fact]
  public void AutoPause_WhenNotRunning_OrForTheUsersOwnReason_IsRefused()
  {
    _wpf.Invoke(() =>
    {
      Assert.False(Tracker.AutoPause(PauseReason.Away));

      StartSession();

      Assert.False(Tracker.AutoPause(PauseReason.User));
      Assert.False(Tracker.AutoPause(PauseReason.None));
    });
  }

  [Theory]
  [InlineData(PauseReason.Away, "Paused: away", "Timer paused: away")]
  [InlineData(PauseReason.Covered, "Paused: camera is covered", "Timer paused: camera covered")]
  [InlineData(PauseReason.CameraOff, "Paused: camera is off", "Timer paused: camera lost")]
  public void AutoPause_PausesAndExplainsWhy(PauseReason reason, string status, string logLine)
  {
    _wpf.Invoke(() =>
    {
      StartSession();

      Assert.True(Tracker.AutoPause(reason));

      Assert.Equal(reason, Tracker.Reason);
      Assert.Equal(status, Tracker.StatusText);
      Assert.Contains(logLine, _log.InfoLines);
      Assert.Equal(new[] { reason }, _reasons);
    });
  }

  [Fact]
  public void AutoPause_ForTheSameReasonAgain_ReportsItIsStillPausedForThatReason()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(PauseReason.Away);

      Assert.True(Tracker.AutoPause(PauseReason.Away));
      Assert.Single(_reasons);
    });
  }

  [Fact]
  public void AutoPause_AMoreUrgentReasonReplacesALessUrgentOne_ButNotTheOtherWayRound()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(PauseReason.Away);

      Assert.True(Tracker.AutoPause(PauseReason.CameraOff));
      Assert.Equal(PauseReason.CameraOff, Tracker.Reason);

      Assert.False(Tracker.AutoPause(PauseReason.Away));
      Assert.Equal(PauseReason.CameraOff, Tracker.Reason);
    });
  }

  [Fact]
  public void AutoPause_NeverOverridesTheUsersOwnPause()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.TogglePause();

      Assert.False(Tracker.AutoPause(PauseReason.Away));
      Assert.Equal(PauseReason.User, Tracker.Reason);
    });
  }

  [Theory]
  [InlineData(PauseReason.Covered)]
  [InlineData(PauseReason.CameraOff)]
  public void AutoResume_UndoesACameraPause(PauseReason reason)
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(reason);

      Tracker.AutoResume();

      Assert.False(Tracker.IsPaused);
      Assert.Contains("Timer resumed: camera back", _log.InfoLines);
    });
  }

  [Fact]
  public void AutoResume_LeavesAwayAndTheUsersPauseAlone()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.AutoPause(PauseReason.Away);
      Tracker.AutoResume();
      Assert.Equal(PauseReason.Away, Tracker.Reason);

      Tracker.ResumeFromAway();
      Tracker.TogglePause();
      Tracker.AutoResume();
      Assert.Equal(PauseReason.User, Tracker.Reason);
    });
  }

  [Fact]
  public void ResumeFromAway_WhenNotAway_DoesNothing()
  {
    _wpf.Invoke(() =>
    {
      StartSession();

      Tracker.ResumeFromAway();

      Assert.Empty(_reasons);
    });
  }

  // ---- counting and saving ----

  [Fact]
  public void Tick_IgnoresBigGaps_LikeALaptopSleeping()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      var before = Private.Get<TimeSpan>(Tracker, "_session");

      TickAfter(60);

      Assert.Equal(before, Private.Get<TimeSpan>(Tracker, "_session"));
    });
  }

  [Fact]
  public void Tick_WhilePaused_DoesNotCountTheTime()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.TogglePause();
      var before = Private.Get<TimeSpan>(Tracker, "_session");

      TickAfter(2);

      Assert.Equal(before, Private.Get<TimeSpan>(Tracker, "_session"));
    });
  }

  [Fact]
  public void Tick_SavesToTheStore_EveryHalfMinute()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Private.Set(Tracker, "_lastSave", DateTime.Now - TimeSpan.FromSeconds(60));

      TickAfter(1);

      Assert.True(_store.Data.ContainsKey(Today));
    });
  }

  [Fact]
  public void SettingTheStore_ForgetsWhatWasRemembered_AndRefreshesTheScreen()
  {
    _wpf.Invoke(() =>
    {
      var changes = 0;
      Tracker.PropertyChanged += (_, _) => changes++;

      Tracker.Store = new FakeDailyTimeStore { Data = { [Today] = 3725 } };

      Assert.True(changes > 0);
      Assert.Equal("1h 02m", Tracker.TodayText);
      Assert.Equal(TimeSpan.FromSeconds(3725), Tracker.Today);
    });
  }

  [Fact]
  public void TodayText_UnderAnHour_ShowsMinutesAndSeconds()
  {
    _wpf.Invoke(() =>
    {
      Tracker.Store = new FakeDailyTimeStore { Data = { [Today] = 125 } };

      Assert.Equal("2m 05s", Tracker.TodayText);
    });
  }

  [Fact]
  public void Today_WithNoStore_IsZero()
  {
    _wpf.Invoke(() =>
    {
      Tracker.Store = null;

      Assert.Equal(TimeSpan.Zero, Tracker.Today);
    });
  }

  [Fact]
  public void WhenTheStoreCannotBeRead_TodayIsZero_AndTheProblemIsLogged()
  {
    _wpf.Invoke(() =>
    {
      Tracker.Store = new ThrowingDailyTimeStore { FailReads = true };

      Assert.Equal(TimeSpan.Zero, Tracker.Today);
      Assert.Contains(_log.InfoLines, line => line.StartsWith("WARNING: could not read daily time", StringComparison.Ordinal));
    });
  }

  [Fact]
  public void WhenTheStoreCannotBeWritten_TheTimerKeepsGoing_AndTheProblemIsLogged()
  {
    _wpf.Invoke(() =>
    {
      Tracker.Store = new ThrowingDailyTimeStore { FailWrites = true };
      StartSession();
      TickAfter(2);

      Tracker.Stop();

      Assert.False(Tracker.IsRunning);
      Assert.Contains(_log.InfoLines, line => line.StartsWith("WARNING: could not save daily time", StringComparison.Ordinal));
    });
  }
}
