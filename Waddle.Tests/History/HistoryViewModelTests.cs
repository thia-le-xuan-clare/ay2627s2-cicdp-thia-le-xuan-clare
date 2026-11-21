using System.Globalization;
using Waddle.Wpf;

namespace Waddle.Tests;

// The older, simpler history grid (HistoryViewModel). It reads today's live value from TimeTracker, so it runs
// on the WPF thread with the other singleton tests.
[Collection(WpfCollection.Name)]
public sealed class HistoryViewModelTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly FakeDailyTimeStore _store = new();

  public HistoryViewModelTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
  }

  public void Dispose() => _wpf.Invoke(WpfState.Reset);

  private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

  private static DateOnly ThisMonth => new(Today.Year, Today.Month, 1);

  private HistoryViewModel Create() => _wpf.Query(() => new HistoryViewModel(_store));

  private static DayCell CellFor(HistoryViewModel vm, DateOnly day) => vm.Cells.Single(c => c.Date == day);

  [Fact]
  public void StartsOnTheCurrentMonth()
  {
    var vm = Create();

    Assert.Equal(DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture), vm.MonthTitle);
    Assert.False(vm.CanGoNext);
    Assert.Equal("Click a day to see how long you sat up.", vm.DetailText);
  }

  [Fact]
  public void Grid_HasSevenRowsOfWeeks_AndOneCellPerDayOfTheMonth()
  {
    var vm = Create();

    Assert.Equal(7 * vm.WeekCount, vm.Cells.Count);
    Assert.Equal(DateTime.DaysInMonth(ThisMonth.Year, ThisMonth.Month), vm.Cells.Count(c => !c.IsBlank));
    Assert.All(vm.Cells.Where(c => c.IsBlank), c => Assert.Equal("", c.Tooltip));
  }

  [Fact]
  public void Cells_AreColouredByHowLongTheUserSatUp()
  {
    var previous = ThisMonth.AddMonths(-1);
    _store.Data[previous.AddDays(1)] = 1;                  // anything at all: level 1
    _store.Data[previous.AddDays(2)] = 30 * 60;            // 30 minutes: level 2
    _store.Data[previous.AddDays(3)] = 60 * 60;            // 1 hour: level 3
    _store.Data[previous.AddDays(4)] = 2 * 3600;           // 2 hours: level 4
    _store.Data[previous.AddDays(5)] = 20 * 3600;          // far more: still level 4
    var vm = Create();

    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));

    Assert.Equal(0, CellFor(vm, previous).Level);          // nothing recorded
    Assert.Equal(1, CellFor(vm, previous.AddDays(1)).Level);
    Assert.Equal(2, CellFor(vm, previous.AddDays(2)).Level);
    Assert.Equal(3, CellFor(vm, previous.AddDays(3)).Level);
    Assert.Equal(4, CellFor(vm, previous.AddDays(4)).Level);
    Assert.Equal(4, CellFor(vm, previous.AddDays(5)).Level);
  }

  [Fact]
  public void TodaysCell_UsesTheLiveTimer_WhenItIsAheadOfTheSavedValue()
  {
    _wpf.Invoke(() => TimeTracker.Instance.Store = new FakeDailyTimeStore { Data = { [Today] = 120 } });

    var vm = Create();

    Assert.Equal(120, CellFor(vm, Today).Seconds);
  }

  [Fact]
  public void PreviousAndNextMonth_MoveTheGrid_AndNextStopsAtTheCurrentMonth()
  {
    var vm = Create();
    var thisMonthTitle = vm.MonthTitle;

    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));
    Assert.NotEqual(thisMonthTitle, vm.MonthTitle);
    Assert.True(vm.CanGoNext);

    _wpf.Invoke(() => vm.NextMonthCommand.Execute(null));
    Assert.Equal(thisMonthTitle, vm.MonthTitle);
    Assert.False(vm.CanGoNext);

    _wpf.Invoke(() => vm.NextMonthCommand.Execute(null));  // already on the current month: nothing happens
    Assert.Equal(thisMonthTitle, vm.MonthTitle);
  }

  [Fact]
  public void SelectingADay_ShowsHowLongTheUserSatUp()
  {
    var previous = ThisMonth.AddMonths(-1);
    _store.Data[previous.AddDays(2)] = 30 * 60;
    var vm = Create();
    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));
    var cell = CellFor(vm, previous.AddDays(2));

    _wpf.Invoke(() => vm.SelectDayCommand.Execute(cell));

    Assert.True(cell.IsSelected);
    Assert.Contains("sat up for 30m 00s", vm.DetailText);
  }

  [Fact]
  public void SelectingADayWithNothingRecorded_SaysSo()
  {
    var vm = Create();
    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));
    var cell = CellFor(vm, ThisMonth.AddMonths(-1));

    _wpf.Invoke(() => vm.SelectDayCommand.Execute(cell));

    Assert.Contains("nothing recorded", vm.DetailText);
  }

  [Fact]
  public void SelectingAnotherDay_UnselectsThePreviousOne()
  {
    var vm = Create();
    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));
    var first = CellFor(vm, ThisMonth.AddMonths(-1));
    var second = CellFor(vm, ThisMonth.AddMonths(-1).AddDays(1));

    _wpf.Invoke(() =>
    {
      vm.SelectDayCommand.Execute(first);
      vm.SelectDayCommand.Execute(second);
    });

    Assert.False(first.IsSelected);
    Assert.True(second.IsSelected);
  }

  [Fact]
  public void SelectingABlankSquareOrNothing_ChangesNothing()
  {
    var vm = Create();
    var blank = vm.Cells.FirstOrDefault(c => c.IsBlank);
    var before = vm.DetailText;

    _wpf.Invoke(() =>
    {
      vm.SelectDayCommand.Execute(null);
      if (blank is not null) vm.SelectDayCommand.Execute(blank);
    });

    Assert.Equal(before, vm.DetailText);
  }

  [Fact]
  public void ChangingMonth_ClearsTheSelection()
  {
    var vm = Create();
    var cell = CellFor(vm, Today);
    _wpf.Invoke(() => vm.SelectDayCommand.Execute(cell));

    _wpf.Invoke(() => vm.PrevMonthCommand.Execute(null));

    Assert.Equal("Click a day to see how long you sat up.", vm.DetailText);
  }

  [Fact]
  public void DayCell_TooltipNamesTheDayAndTheTime()
  {
    var cell = new DayCell { Date = new DateOnly(2026, 11, 13), Seconds = 5 };

    Assert.Contains(": 5s", cell.Tooltip);
    Assert.False(cell.IsBlank);
    Assert.True(new DayCell().IsBlank);
  }

  [Theory]
  [InlineData(0, "0s")]
  [InlineData(5, "5s")]
  [InlineData(65, "1m 05s")]
  [InlineData(1800, "30m 00s")]
  [InlineData(3725, "1h 02m")]
  public void Format_ShowsSecondsMinutesOrHours(int seconds, string expected)
  {
    Assert.Equal(expected, HistoryViewModel.Format(seconds));
  }
}
