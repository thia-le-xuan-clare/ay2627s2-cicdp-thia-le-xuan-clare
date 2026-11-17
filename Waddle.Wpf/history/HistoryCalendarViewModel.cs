using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>One square in the calendar. Date is null for the empty squares before the 1st / after the last day.</summary>
public sealed partial class CalendarDay : ObservableObject
{
    public DateOnly? Date { get; }
    public bool IsBlank => Date is null;
    public string DayNumber => Date is { } d ? d.Day.ToString() : "";
    public string TimeText => Seconds == 0 ? "0m" : HistoryViewModel.Format(Seconds);
    public string Tooltip => Date is { } d ? $"{d:ddd d MMM}: {HistoryViewModel.Format(Seconds)}" : "";
    public ICommand? SelectCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText))]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private int seconds;

    [ObservableProperty] private int level;          // 0-4, picks the colour (H0-H4)
    [ObservableProperty] private bool isSelected;    // true = show the time inside the square

    public CalendarDay(DateOnly? date, int seconds, int level, ICommand? selectCommand)
    {
        Date = date;
        this.seconds = seconds;
        this.level = level;
        SelectCommand = selectCommand;
    }
}

/// <summary>One row of the calendar = one week (Mon to Sun).</summary>
public sealed class CalendarWeek
{
    public string Label { get; init; } = "";         // "Wk 1", "Wk 2"...
    public List<CalendarDay> Days { get; init; } = new();
}

/// <summary>Month calendar for the dashboard: days across the top, one row per week, click a day to see its time.</summary>
public sealed partial class HistoryCalendarViewModel : ObservableObject
{
    // seconds sat up needed to reach each colour level (same as the History window)
    private static readonly int[] LevelStarts = { 1, 30 * 60, 60 * 60, 2 * 3600, 4 * 3600 };

    private readonly IDailyTimeStore _store;
    private readonly Func<DateOnly> _clock;              // what "today" is (the tests pass a fixed date)
    private readonly Func<int> _liveSecondsToday;        // seconds on the running timer (the tests pass a fake)
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateOnly _month;                         // always the 1st of the month shown
    private CalendarDay? _selected;
    private CalendarDay? _todayCell;

    public ObservableCollection<CalendarWeek> Weeks { get; } = new();

    [ObservableProperty] private string monthTitle = "";
    [ObservableProperty] private bool canGoNext;

    /// <param name="month">Which month to open on. Leave out for the current month.</param>
    /// <param name="today">Leave out in the app. Tests pass a fixed date.</param>
    /// <param name="liveSecondsToday">Leave out in the app. Tests pass a fake timer.</param>
    public HistoryCalendarViewModel(IDailyTimeStore store, DateOnly? month = null,
                                    Func<DateOnly>? today = null, Func<int>? liveSecondsToday = null)
    {
        _store = store;
        _clock = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        _liveSecondsToday = liveSecondsToday ?? (() => (int)TimeTracker.Instance.Today.TotalSeconds);
        var start = month ?? _clock();
        _month = new DateOnly(start.Year, start.Month, 1);
        Load();

        _timer.Tick += (_, _) => RefreshToday();
        _timer.Start();
    }

    [RelayCommand]
    private void PrevMonth() { _month = _month.AddMonths(-1); Load(); }

    [RelayCommand]
    private void NextMonth() { if (CanGoNext) { _month = _month.AddMonths(1); Load(); } }

    [RelayCommand]
    private void SelectDay(CalendarDay? day)
    {
        if (day is null || day.IsBlank) return;
        bool wasSelected = day.IsSelected;
        if (_selected != null) _selected.IsSelected = false;
        day.IsSelected = !wasSelected;               // click again to hide
        _selected = day.IsSelected ? day : null;
    }

    private void Load()
    {
        var today = _clock();
        int days = DateTime.DaysInMonth(_month.Year, _month.Month);
        var data = _store.GetSecondsRange(_month, _month.AddDays(days - 1));

        int offset = ((int)_month.DayOfWeek + 6) % 7;        // Monday = column 0
        int weekCount = (offset + days + 6) / 7;
        MonthTitle = _month.ToString("MMMM yyyy");
        CanGoNext = _month < new DateOnly(today.Year, today.Month, 1);

        _selected = null;
        _todayCell = null;
        Weeks.Clear();

        for (int w = 0; w < weekCount; w++)
        {
            var week = new CalendarWeek { Label = $"Wk {w + 1}" };
            for (int c = 0; c < 7; c++)
            {
                int index = w * 7 + c - offset;
                if (index < 0 || index >= days)
                {
                    week.Days.Add(new CalendarDay(null, 0, 0, null));
                    continue;
                }

                var date = _month.AddDays(index);
                data.TryGetValue(date, out var secs);
                // today's saved value can be up to 30 s behind, so use the live timer
                if (date == today) secs = Math.Max(secs, _liveSecondsToday());

                var cell = new CalendarDay(date, secs, LevelFor(secs), SelectDayCommand);
                if (date == today) _todayCell = cell;
                week.Days.Add(cell);
            }
            Weeks.Add(week);
        }
    }

    // keeps today's square counting up while the timer runs (called every second)
    public void RefreshToday()
    {
        if (_todayCell is null) return;
        int live = _liveSecondsToday();
        if (live > _todayCell.Seconds)
        {
            _todayCell.Seconds = live;
            _todayCell.Level = LevelFor(live);
        }
    }

    private static int LevelFor(int seconds)
    {
        int level = 0;
        for (int i = 0; i < LevelStarts.Length; i++)
            if (seconds >= LevelStarts[i]) level = i + 1;
        return Math.Min(level, 4);
    }
}