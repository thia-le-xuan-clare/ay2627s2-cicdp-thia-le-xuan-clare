using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class TimeTrackerPostureTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCalibrationStore _store = new() { Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 0, 0)) };

  public TimeTrackerPostureTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
  }

  public void Dispose() => _wpf.Invoke(() =>
  {
    WpfState.Reset();                // Stop() also clears the bad-posture flag
    AppServices.Clear();
  });

  private static TimeTracker Tracker => TimeTracker.Instance;

  private static TimeSpan Session() => Private.Get<TimeSpan>(Tracker, "_session");

  private void StartSession()
  {
    AppServices.Use(_store);
    Assert.True(Tracker.Start());
  }

  /// <summary>Pretends the given number of seconds went by since the last tick, then ticks once.</summary>
  private static void TickAfter(double seconds)
  {
    Private.Set(Tracker, "_lastTick", DateTime.Now - TimeSpan.FromSeconds(seconds));
    Private.Call(Tracker, "Tick");
  }

  [Fact]
  public void BadPosture_ChangesTheStatusText_AndGoingBackRestoresIt()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Assert.Equal("Timer running", Tracker.StatusText);

      Tracker.IsBadPosture = true;
      Assert.Equal("Bad posture: sit up straight", Tracker.StatusText);

      Tracker.IsBadPosture = false;
      Assert.Equal("Timer running", Tracker.StatusText);
    });
  }

  [Fact]
  public void Tick_WithGoodPosture_CountsTheTime()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      var before = Session();

      TickAfter(2);

      Assert.True(Session() - before >= TimeSpan.FromSeconds(1.9));
    });
  }

  [Fact]
  public void Tick_WhileSlouching_DoesNotCountTheTime()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.IsBadPosture = true;
      var held = Session();

      TickAfter(2);

      Assert.Equal(held, Session());
    });
  }

  [Fact]
  public void Tick_AfterSittingUp_CountsTheTimeAgain()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.IsBadPosture = true;
      Tracker.IsBadPosture = false;
      var before = Session();

      TickAfter(2);

      Assert.True(Session() - before >= TimeSpan.FromSeconds(1.9));
    });
  }

  [Fact]
  public void Stop_ClearsTheBadPostureFlag_SoTheNextSessionStartsClean()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.IsBadPosture = true;
      Tracker.Stop();

      StartSession();

      Assert.Equal("Timer running", Tracker.StatusText);
    });
  }

  [Fact]
  public void StatusText_WhenThePersonPausedItThemselves_StillSaysPaused()
  {
    _wpf.Invoke(() =>
    {
      StartSession();
      Tracker.TogglePause();

      Tracker.IsBadPosture = true;

      Assert.Equal("Paused", Tracker.StatusText);
    });
  }
}
