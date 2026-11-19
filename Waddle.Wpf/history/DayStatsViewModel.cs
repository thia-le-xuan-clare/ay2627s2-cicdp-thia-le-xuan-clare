using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Waddle.Core;

namespace Waddle.Wpf;

/// <summary>One coloured wedge of a pie chart (also used for the legend rows).</summary>
public sealed record PieSlice(string Label, int Count, double Percent, Brush Fill, Geometry Shape)
{
  public string LegendText => $"{Label}: {Count} ({Percent:0}%)";
}

/// <summary>Everything one pie chart shows: title, wedges, and whether there is any data.</summary>
public sealed class PieChartModel
{
  public const double Size = 160;

  public string Title { get; }
  public IReadOnlyList<PieSlice> Slices { get; }
  public int Total { get; }
  public bool HasData => Total > 0;
  public bool IsEmpty => !HasData;

  public PieChartModel(string title, IEnumerable<(string Label, int Count, Brush Fill)> parts)
  {
    Title = title;
    var used = parts.Where(p => p.Count > 0).ToList();
    Total = used.Sum(p => p.Count);

    var slices = new List<PieSlice>();
    double start = -90;                                   // start at 12 o'clock
    foreach (var (label, count, fill) in used)
    {
      double sweep = 360.0 * count / Total;
      slices.Add(new PieSlice(label, count, 100.0 * count / Total, fill, Wedge(start, sweep, used.Count == 1)));
      start -= sweep;                                   // go counter-clockwise (left side first)
    }
    Slices = slices;
  }

  private static Geometry Wedge(double startDeg, double sweepDeg, bool full)
  {
    double c = Size / 2, r = c - 4;
    Geometry g;
    if (full)
    {
      g = new EllipseGeometry(new Point(c, c), r, r);   // an arc can't draw a full 360 degrees
    }
    else
    {
      Point P(double deg) => new(c + r * Math.Cos(deg * Math.PI / 180), c + r * Math.Sin(deg * Math.PI / 180));
      var figure = new PathFigure { StartPoint = new Point(c, c), IsClosed = true };
      figure.Segments.Add(new LineSegment(P(startDeg), true));
      figure.Segments.Add(new ArcSegment(P(startDeg - sweepDeg), new Size(r, r), 0,
                                         sweepDeg > 180, SweepDirection.Counterclockwise, true));
      g = new PathGeometry(new[] { figure });
    }
    g.Freeze();
    return g;
  }
}

/// <summary>
/// The stats block on the dashboard: "You slouched N times" on top, then the banner and overlay pies.
/// Shows today until a calendar day is clicked; un-selecting a day goes back to today.
/// </summary>
public sealed partial class DayStatsViewModel : ObservableObject
{
  private static Brush MakeBrush(string hex)
  {
    var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    b.Freeze();
    return b;
  }

  // orange family so it sits well on the yellow theme: mid = good, dark = ignored, light = dismissed / hotkey
  private static readonly Brush SatUpFill = MakeBrush("#E8801C");
  private static readonly Brush HotkeyFill = MakeBrush("#F2A66B");
  private static readonly Brush IgnoredFill = MakeBrush("#A8420E");
  private static readonly Brush DismissedFill = MakeBrush("#F2A66B");

  private readonly IWarningEventStore _store;
  private readonly Func<DateOnly> _clock;
  private DateOnly? _selected;                             // null = nobody clicked a day, so show today

  [ObservableProperty] private string headline = "";
  [ObservableProperty] private string dateText = "";
  [ObservableProperty] private PieChartModel banner = null!;
  [ObservableProperty] private PieChartModel overlay = null!;

  public DayStatsViewModel(IWarningEventStore store, Func<DateOnly>? today = null)
  {
    _store = store;
    _clock = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
    Refresh();
  }

  public DateOnly ShownDay => _selected ?? _clock();

  /// <summary>A calendar day was clicked (date) or un-clicked (null = back to today).</summary>
  public void ShowDay(DateOnly? day)
  {
    _selected = day;
    Refresh();
  }

  /// <summary>Re-reads the database. Called after a click and every few seconds while today is shown.</summary>
  public void Refresh()
  {
    var day = ShownDay;
    var stats = _store.GetDayStats(day);

    Headline = stats.SlouchCount == 1 ? "You slouched 1 time" : $"You slouched {stats.SlouchCount} times";
    DateText = day == _clock() ? "Today" : day.ToString("dddd d MMMM yyyy");

    Banner = new PieChartModel("Banner", new (string, int, Brush)[]
{
            ("Sat up", stats.Banner.SatUp, SatUpFill),
            ("Dismissed", stats.Banner.Dismissed, DismissedFill),
            ("Ignored", stats.Banner.Ignored, IgnoredFill),
});

    Overlay = new PieChartModel("Overlay", new (string, int, Brush)[]
    {
            ("Sat up", stats.Overlay.SatUp, SatUpFill),
            ("Used hotkey", stats.Overlay.HotkeyExit, HotkeyFill),
            ("Ignored", stats.Overlay.Ignored, IgnoredFill),
    });
  }

  /// <summary>Timer tick: only today's numbers can change, so leave other days alone.</summary>
  public void RefreshIfToday()
  {
    if (_selected is null || _selected == _clock()) Refresh();
  }
}
