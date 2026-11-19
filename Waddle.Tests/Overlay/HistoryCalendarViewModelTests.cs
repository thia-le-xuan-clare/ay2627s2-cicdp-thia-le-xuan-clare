using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

/// <summary>In-memory store so the tests never touch SQLite.</summary>
internal sealed class FakeDailyTimeStore : IDailyTimeStore
{
  public Dictionary<DateOnly, int> Data { get; } = new();

  public int GetSeconds(DateOnly day) => Data.TryGetValue(day, out var s) ? s : 0;
  public void SetSeconds(DateOnly day, int seconds) => Data[day] = seconds;

  public IReadOnlyDictionary<DateOnly, int> GetSecondsRange(DateOnly from, DateOnly to) =>
      Data.Where(kv => kv.Key >= from && kv.Key <= to).ToDictionary(kv => kv.Key, kv => kv.Value);
}

public class HistoryCalendarViewModelTests
{
  // November 2026 starts on a Sunday and ends on a Monday, so it needs 6 week rows.
  private static readonly DateOnly Nov2026 = new(2026, 11, 1);

  private static HistoryCalendarViewModel Make(FakeDailyTimeStore? store = null, DateOnly? month = null) =>
      new(store ?? new FakeDailyTimeStore(), month ?? Nov2026);

  private static CalendarDay Day(HistoryCalendarViewModel vm, int dayOfMonth) =>
      vm.Weeks.SelectMany(w => w.Days).Single(d => d.Date?.Day == dayOfMonth);

  [Fact]
  public void Shows_month_title()
  {
    Assert.Equal("November 2026", Make().MonthTitle);
  }

  [Fact]
  public void November_2026_has_six_weeks_labelled_Wk1_to_Wk6()
  {
    var vm = Make();
    Assert.Equal(6, vm.Weeks.Count);
    Assert.Equal(new[] { "Wk 1", "Wk 2", "Wk 3", "Wk 4", "Wk 5", "Wk 6" }, vm.Weeks.Select(w => w.Label));
  }

  [Fact]
  public void Every_week_has_seven_squares()
  {
    Assert.All(Make().Weeks, w => Assert.Equal(7, w.Days.Count));
  }

  [Fact]
  public void First_of_November_sits_in_the_Sunday_column_of_the_first_row()
  {
    var first = Make().Weeks[0].Days;
    Assert.All(first.Take(6), d => Assert.True(d.IsBlank));   // Mon-Sat are empty
    Assert.Equal(1, first[6].Date?.Day);                      // Sun = column 6
  }

  [Fact]
  public void Second_of_November_starts_a_new_row_on_Monday()
  {
    var second = Make().Weeks[1].Days;
    Assert.Equal(2, second[0].Date?.Day);
  }

  [Fact]
  public void Last_day_lands_on_Monday_of_the_last_row()
  {
    var last = Make().Weeks[5].Days;
    Assert.Equal(30, last[0].Date?.Day);
    Assert.All(last.Skip(1), d => Assert.True(d.IsBlank));
  }

  [Fact]
  public void Every_day_of_the_month_appears_exactly_once()
  {
    var days = Make().Weeks.SelectMany(w => w.Days).Where(d => !d.IsBlank).Select(d => d.Date!.Value.Day);
    Assert.Equal(Enumerable.Range(1, 30), days);
  }

  [Fact]
  public void Day_number_text_matches_the_date()
  {
    Assert.Equal("17", Day(Make(), 17).DayNumber);
  }

  [Fact]
  public void Loads_saved_seconds_from_the_store()
  {
    var store = new FakeDailyTimeStore();
    store.Data[new DateOnly(2026, 11, 10)] = 5400;
    Assert.Equal(5400, Day(Make(store), 10).Seconds);
    Assert.Equal(0, Day(Make(store), 11).Seconds);
  }

  [Theory]
  [InlineData(0, 0)]
  [InlineData(1, 1)]
  [InlineData(1799, 1)]
  [InlineData(1800, 2)]      // 30 min
  [InlineData(3599, 2)]
  [InlineData(3600, 3)]      // 1 h
  [InlineData(7199, 3)]
  [InlineData(7200, 4)]      // 2 h
  [InlineData(14400, 4)]     // 4 h is capped at the darkest colour
  [InlineData(40000, 4)]
  public void Colour_level_matches_time_sat_up(int seconds, int expectedLevel)
  {
    var store = new FakeDailyTimeStore();
    store.Data[new DateOnly(2026, 11, 10)] = seconds;
    Assert.Equal(expectedLevel, Day(Make(store), 10).Level);
  }

  [Theory]
  [InlineData(0, "0s")]
  [InlineData(45, "45s")]
  [InlineData(610, "10m 10s")]
  [InlineData(3600, "1h 00m")]
  [InlineData(11100, "3h 05m")]
  public void Time_format(int seconds, string expected)
  {
    Assert.Equal(expected, HistoryViewModel.Format(seconds));
  }

  [Fact]
  public void Square_with_nothing_recorded_shows_0m()
  {
    Assert.Equal("0m", Day(Make(), 5).TimeText);
  }

  [Fact]
  public void Clicking_a_day_selects_it_and_clicking_again_hides_it()
  {
    var vm = Make();
    var day = Day(vm, 5);

    vm.SelectDayCommand.Execute(day);
    Assert.True(day.IsSelected);

    vm.SelectDayCommand.Execute(day);
    Assert.False(day.IsSelected);
  }

  [Fact]
  public void Only_one_day_is_selected_at_a_time()
  {
    var vm = Make();
    var a = Day(vm, 5);
    var b = Day(vm, 6);

    vm.SelectDayCommand.Execute(a);
    vm.SelectDayCommand.Execute(b);

    Assert.False(a.IsSelected);
    Assert.True(b.IsSelected);
  }

  [Fact]
  public void Clicking_an_empty_square_does_nothing()
  {
    var vm = Make();
    var blank = vm.Weeks[0].Days[0];

    vm.SelectDayCommand.Execute(blank);

    Assert.False(blank.IsSelected);
  }

  [Fact]
  public void Previous_month_changes_the_title_and_layout()
  {
    var vm = Make();
    vm.PrevMonthCommand.Execute(null);

    Assert.Equal("October 2026", vm.MonthTitle);
    Assert.Equal(5, vm.Weeks.Count);                       // October 2026 starts on a Thursday
    Assert.Equal(1, vm.Weeks[0].Days[3].Date?.Day);        // Thu = column 3
  }

  [Fact]
  public void Changing_month_clears_the_selection()
  {
    var vm = Make();
    vm.SelectDayCommand.Execute(Day(vm, 5));

    vm.PrevMonthCommand.Execute(null);

    Assert.All(vm.Weeks.SelectMany(w => w.Days), d => Assert.False(d.IsSelected));
  }

  [Fact]
  public void Can_go_next_when_viewing_a_past_month()
  {
    Assert.True(Make(month: new DateOnly(2025, 1, 1)).CanGoNext);
  }

  // ---- current month: fixed "today" = Tue 17 Nov 2026 and a fake live timer ----

  private static readonly DateOnly FakeToday = new(2026, 11, 17);

  private static HistoryCalendarViewModel MakeCurrent(FakeDailyTimeStore store, Func<int> live) =>
      new(store, month: null, today: () => FakeToday, liveSecondsToday: live);

  [Fact]
  public void Current_month_opens_on_the_fake_today()
  {
    Assert.Equal("November 2026", MakeCurrent(new FakeDailyTimeStore(), () => 0).MonthTitle);
  }

  [Fact]
  public void Current_month_other_days_come_from_the_store_not_the_live_timer()
  {
    var store = new FakeDailyTimeStore();
    store.Data[new DateOnly(2026, 11, 10)] = 5400;
    var vm = MakeCurrent(store, () => 999);

    Assert.Equal(5400, Day(vm, 10).Seconds);
    Assert.Equal(0, Day(vm, 16).Seconds);          // yesterday, nothing saved
  }

  [Fact]
  public void Future_days_in_the_current_month_are_empty()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 999);
    Assert.Equal(0, Day(vm, 25).Seconds);
    Assert.Equal(0, Day(vm, 25).Level);
  }

  [Fact]
  public void Today_uses_the_live_timer_when_it_is_ahead_of_the_saved_value()
  {
    var store = new FakeDailyTimeStore();
    store.Data[FakeToday] = 100;
    Assert.Equal(500, Day(MakeCurrent(store, () => 500), 17).Seconds);
  }

  [Fact]
  public void Today_keeps_the_saved_value_when_it_is_ahead_of_the_live_timer()
  {
    var store = new FakeDailyTimeStore();
    store.Data[FakeToday] = 900;
    Assert.Equal(900, Day(MakeCurrent(store, () => 50), 17).Seconds);
  }

  [Fact]
  public void Refresh_updates_only_todays_square_and_colour()
  {
    int live = 0;
    var store = new FakeDailyTimeStore();
    store.Data[new DateOnly(2026, 11, 10)] = 5400;
    var vm = MakeCurrent(store, () => live);
    Assert.Equal(0, Day(vm, 17).Level);

    live = 3700;                                    // just over 1 hour
    vm.RefreshToday();

    Assert.Equal(3700, Day(vm, 17).Seconds);
    Assert.Equal(3, Day(vm, 17).Level);
    Assert.Equal(5400, Day(vm, 10).Seconds);        // other days untouched
  }

  [Fact]
  public void Refresh_never_makes_today_go_backwards()
  {
    int live = 600;
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => live);

    live = 10;
    vm.RefreshToday();

    Assert.Equal(600, Day(vm, 17).Seconds);
  }

  [Fact]
  public void Cannot_go_to_a_month_after_the_current_one()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);

    Assert.False(vm.CanGoNext);
    vm.NextMonthCommand.Execute(null);

    Assert.Equal("November 2026", vm.MonthTitle);
  }

  [Fact]
  public void Going_back_a_month_allows_going_forward_again()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);

    vm.PrevMonthCommand.Execute(null);
    Assert.True(vm.CanGoNext);

    vm.NextMonthCommand.Execute(null);
    Assert.Equal("November 2026", vm.MonthTitle);
    Assert.False(vm.CanGoNext);
  }

  [Fact]
  public void Live_timer_is_not_applied_to_a_past_month()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 99999);

    vm.PrevMonthCommand.Execute(null);

    Assert.All(vm.Weeks.SelectMany(w => w.Days).Where(d => !d.IsBlank), d => Assert.Equal(0, d.Seconds));
  }

  [Fact]
  public void Clicking_a_day_raises_DaySelectionChanged_with_its_date_and_clicking_again_raises_null()
  {
    var vm = Make();
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;

    vm.SelectDayCommand.Execute(Day(vm, 5));
    vm.SelectDayCommand.Execute(Day(vm, 5));

    Assert.Equal(new DateOnly?[] { new DateOnly(2026, 11, 5), null }, raised);
  }

  // ---- SelectToday: the dashboard starts with today's square already clicked ----

  [Fact]
  public void SelectToday_selects_todays_square_and_raises_DaySelectionChanged_with_today()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;

    vm.SelectToday();

    Assert.True(Day(vm, 17).IsSelected);
    Assert.Equal(new DateOnly?[] { FakeToday }, raised);
  }

  [Fact]
  public void SelectToday_leaves_every_other_day_unselected()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);

    vm.SelectToday();

    var selected = vm.Weeks.SelectMany(w => w.Days).Where(d => d.IsSelected).ToList();
    Assert.Single(selected);
    Assert.Equal(FakeToday, selected[0].Date);
  }

  [Fact]
  public void SelectToday_called_twice_keeps_today_selected_and_only_raises_once()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;

    vm.SelectToday();
    vm.SelectToday();

    Assert.True(Day(vm, 17).IsSelected);
    Assert.Single(raised);
  }

  [Fact]
  public void SelectToday_moves_the_selection_when_another_day_was_clicked()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;
    vm.SelectDayCommand.Execute(Day(vm, 5));

    vm.SelectToday();

    Assert.False(Day(vm, 5).IsSelected);
    Assert.True(Day(vm, 17).IsSelected);
    Assert.Equal(new DateOnly?[] { new DateOnly(2026, 11, 5), FakeToday }, raised);
  }

  [Fact]
  public void SelectToday_does_nothing_when_a_past_month_is_showing()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);
    vm.PrevMonthCommand.Execute(null);
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;

    vm.SelectToday();

    Assert.Empty(raised);
    Assert.DoesNotContain(vm.Weeks.SelectMany(w => w.Days), d => d.IsSelected);
  }

  [Fact]
  public void Clicking_todays_square_after_SelectToday_hides_it_again()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 0);
    var raised = new List<DateOnly?>();
    vm.DaySelectionChanged += raised.Add;
    vm.SelectToday();

    vm.SelectDayCommand.Execute(Day(vm, 17));

    Assert.False(Day(vm, 17).IsSelected);
    Assert.Equal(new DateOnly?[] { FakeToday, null }, raised);
  }

  [Fact]
  public void Selected_today_shows_its_live_time()
  {
    var vm = MakeCurrent(new FakeDailyTimeStore(), () => 3660);

    vm.SelectToday();

    Assert.True(Day(vm, 17).IsSelected);
    Assert.Equal("1h 01m", Day(vm, 17).TimeText);
  }
}
