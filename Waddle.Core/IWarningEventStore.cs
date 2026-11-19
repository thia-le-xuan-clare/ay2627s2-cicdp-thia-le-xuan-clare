namespace Waddle.Core;

public enum WarningLevel
{
  Banner = 1,
  Overlay = 2
}

public enum WarningOutcome
{
  SatUp = 1,
  HotkeyExit = 2,
  Ignored = 3,
  Dismissed = 4
}

public sealed record DailyWarningSummary(
    string Date,
    int LevelId,
    int SatUpCount,
    int HotkeyExitCount,
    int IgnoredCount,
    int DismissedCount
);

/// <summary>Outcome counts for one warning level (banner or overlay) on one day.</summary>
public sealed record LevelOutcomeCounts(int SatUp, int HotkeyExit, int Ignored, int Dismissed)
{
  public int Total => SatUp + HotkeyExit + Ignored + Dismissed;
}

/// <summary>Everything the stats pie charts need for one day.
/// SlouchCount = number of banners shown, because every slouch starts with a banner.</summary>
public sealed record DayWarningStats(DateOnly Day, int SlouchCount, LevelOutcomeCounts Banner, LevelOutcomeCounts Overlay)
{
  public static DayWarningStats Empty(DateOnly day) =>
      new(day, 0, new LevelOutcomeCounts(0, 0, 0, 0), new LevelOutcomeCounts(0, 0, 0, 0));
}

public interface IWarningEventStore
{
  void Record(WarningLevel level, WarningOutcome outcome, DateTime occurredAt);
  IReadOnlyList<DailyWarningSummary> GetDailySummaries(int lastNDays = 7);

  /// <summary>Counts for one day. Returns all zeros if nothing happened that day.</summary>
  DayWarningStats GetDayStats(DateOnly day);
}
