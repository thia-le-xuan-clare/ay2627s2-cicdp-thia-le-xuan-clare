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

public interface IWarningEventStore
{
    void Record(WarningLevel level, WarningOutcome outcome, DateTime occurredAt);
    IReadOnlyList<DailyWarningSummary> GetDailySummaries(int lastNDays = 7);
}
