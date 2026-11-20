using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>One square in the grid. Date is null for the empty squares before the 1st / after the last day.</summary>
public sealed partial class DayCell : ObservableObject
{
  public DateOnly? Date { get; init; }
  public int Seconds { get; init; }
  public int Level { get; init; }                  // 0-4, picks the colour (H0-H4)
  public bool IsBlank => Date is null;
  public string Tooltip => Date is { } d ? $"{d:ddd d MMM}: {HistoryViewModel.Format(Seconds)}" : "";

  [ObservableProperty] private bool isSelected;
}

public sealed partial class HistoryViewModel : ObservableObject
{
  // Change these to taste: seconds sat up needed to reach each colour level.
  private static readonly int[] LevelStarts = { 1, 30 * 60, 60 * 60, 2 * 3600, 4 * 3600 }; // 0 = nothing recorded

  private readonly IDailyTimeStore _store;
  private DateOnly _month;                         // always the 1st of the month shown
  private DayCell? _selected;

  public ObservableCollection<DayCell> Cells { get; } = new();

  [ObservableProperty] private string monthTitle = "";
  [ObservableProperty] private int weekCount = 5;
  [ObservableProperty] private bool canGoNext;
  [ObservableProperty] private string detailText = "Click a day to see how long you sat up.";

  public HistoryViewModel(IDailyTimeStore store)
  {
    _store = store;
    var today = DateOnly.FromDateTime(DateTime.Now);
    _month = new DateOnly(today.Year, today.Month, 1);
    Load();
  }

  [RelayCommand]
  private void PrevMonth() { _month = _month.AddMonths(-1); Load(); }

  [RelayCommand]
  private void NextMonth() { if (CanGoNext) { _month = _month.AddMonths(1); Load(); } }

  [RelayCommand]
  private void SelectDay(DayCell? cell)
  {
    if (cell is null || cell.IsBlank) return;
    if (_selected != null) _selected.IsSelected = false;
    cell.IsSelected = true;
    _selected = cell;
    DetailText = cell.Seconds == 0
        ? $"{cell.Date:dddd d MMMM yyyy}: nothing recorded"
        : $"{cell.Date:dddd d MMMM yyyy}: sat up for {Format(cell.Seconds)}";
  }

  private void Load()
  {
    var today = DateOnly.FromDateTime(DateTime.Now);
    var days = DateTime.DaysInMonth(_month.Year, _month.Month);
    var data = _store.GetSecondsRange(_month, _month.AddDays(days - 1));

    int offset = ((int)_month.DayOfWeek + 6) % 7;        // Monday = row 0
    WeekCount = (offset + days + 6) / 7;
    MonthTitle = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    CanGoNext = _month < new DateOnly(today.Year, today.Month, 1);

    _selected = null;
    DetailText = "Click a day to see how long you sat up.";
    Cells.Clear();

    // UniformGrid fills row by row, so go weekday-by-weekday to get weeks as columns like GitHub
    for (int row = 0; row < 7; row++)
      for (int col = 0; col < WeekCount; col++)
      {
        int index = col * 7 + row - offset;
        if (index < 0 || index >= days) { Cells.Add(new DayCell()); continue; }

        var date = _month.AddDays(index);
        data.TryGetValue(date, out var secs);
        // today's saved value can be up to 30 s behind, so use the live timer
        if (date == today) secs = Math.Max(secs, (int)TimeTracker.Instance.Today.TotalSeconds);

        Cells.Add(new DayCell { Date = date, Seconds = secs, Level = LevelFor(secs) });
      }
  }

  private static int LevelFor(int seconds)
  {
    int level = 0;
    for (int i = 0; i < LevelStarts.Length; i++)
      if (seconds >= LevelStarts[i]) level = i + 1;
    return Math.Min(level, 4);
  }

  public static string Format(int seconds)
  {
    var t = TimeSpan.FromSeconds(seconds);
    if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
    if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:00}s";
    return $"{t.Seconds}s";
  }
}
