using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

internal sealed class FakeWarningEventStore : IWarningEventStore
{
  public Dictionary<DateOnly, DayWarningStats> Days { get; } = new();

  public void Record(WarningLevel level, WarningOutcome outcome, DateTime occurredAt) { }
  public IReadOnlyList<DailyWarningSummary> GetDailySummaries(int lastNDays = 7) => Array.Empty<DailyWarningSummary>();
  public DayWarningStats GetDayStats(DateOnly day) =>
      Days.TryGetValue(day, out var s) ? s : DayWarningStats.Empty(day);
}

public class DayStatsViewModelTests
{
  private static readonly DateOnly Today = new(2026, 11, 18);
  private static readonly DateOnly Earlier = new(2026, 11, 10);

  private static (DayStatsViewModel vm, FakeWarningEventStore store) Make()
  {
    var store = new FakeWarningEventStore();
    store.Days[Today] = new DayWarningStats(Today, 5,
        new LevelOutcomeCounts(SatUp: 2, HotkeyExit: 0, Ignored: 3, Dismissed: 0),
        new LevelOutcomeCounts(SatUp: 0, HotkeyExit: 1, Ignored: 2, Dismissed: 0));
    store.Days[Earlier] = new DayWarningStats(Earlier, 1,
        new LevelOutcomeCounts(0, 0, 0, 1), new LevelOutcomeCounts(0, 0, 0, 0));
    return (new DayStatsViewModel(store, () => Today), store);
  }

  [Fact]
  public void Shows_today_when_no_day_was_clicked()
  {
    var (vm, _) = Make();

    Assert.Equal(Today, vm.ShownDay);
    Assert.Equal("Today", vm.DateText);
    Assert.Equal("You slouched 5 times", vm.Headline);
  }

  [Fact]
  public void Banner_and_overlay_pies_show_their_own_outcomes()
  {
    var (vm, _) = Make();

    Assert.Equal(new[] { "Sat up", "Ignored" }, vm.Banner.Slices.Select(s => s.Label));
    Assert.Equal(new[] { 2, 3 }, vm.Banner.Slices.Select(s => s.Count));
    Assert.Equal(new[] { "Used hotkey", "Ignored" }, vm.Overlay.Slices.Select(s => s.Label));
    Assert.Equal(new[] { 1, 2 }, vm.Overlay.Slices.Select(s => s.Count));
  }

  [Fact]
  public void Clicking_a_day_switches_to_that_day()
  {
    var (vm, _) = Make();

    vm.ShowDay(Earlier);

    Assert.Equal(Earlier, vm.ShownDay);
    Assert.Equal("You slouched 1 time", vm.Headline);
    Assert.Equal("Dismissed", Assert.Single(vm.Banner.Slices).Label);
    Assert.True(vm.Overlay.IsEmpty);
  }

  [Fact]
  public void Clicking_the_day_again_goes_back_to_today()
  {
    var (vm, _) = Make();
    vm.ShowDay(Earlier);

    vm.ShowDay(null);

    Assert.Equal(Today, vm.ShownDay);
    Assert.Equal("You slouched 5 times", vm.Headline);
  }

  [Fact]
  public void Day_with_no_events_shows_zero_and_empty_pies()
  {
    var (vm, _) = Make();

    vm.ShowDay(new DateOnly(2026, 11, 2));

    Assert.Equal("You slouched 0 times", vm.Headline);
    Assert.True(vm.Banner.IsEmpty);
    Assert.True(vm.Overlay.IsEmpty);
  }

  [Fact]
  public void Percentages_add_up_to_100()
  {
    var (vm, _) = Make();

    Assert.Equal(100, vm.Banner.Slices.Sum(s => s.Percent), 5);
  }

  [Fact]
  public void RefreshIfToday_picks_up_new_events_but_leaves_other_days_alone()
  {
    var (vm, store) = Make();
    store.Days[Today] = store.Days[Today] with { SlouchCount = 6 };

    vm.RefreshIfToday();
    Assert.Equal("You slouched 6 times", vm.Headline);

    vm.ShowDay(Earlier);
    store.Days[Earlier] = store.Days[Earlier] with { SlouchCount = 9 };
    vm.RefreshIfToday();
    Assert.Equal("You slouched 1 time", vm.Headline);
  }
}
