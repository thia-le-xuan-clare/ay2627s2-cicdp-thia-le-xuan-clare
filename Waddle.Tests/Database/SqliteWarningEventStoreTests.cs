using Waddle.Core;
using Waddle.Infrastructure.Data;

namespace Waddle.Tests;

public sealed class SqliteWarningEventStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly SqliteWarningEventStore _store;

    public SqliteWarningEventStoreTests()
    {
        _store = new SqliteWarningEventStore(_temp.Db);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Record_CreatesDailyHistoryRowAndStoresBannerOutcomes()
    {
        var day = new DateTime(2026, 11, 13, 14, 30, 0);

        _store.Record(WarningLevel.Banner, WarningOutcome.SatUp, day);
        _store.Record(WarningLevel.Banner, WarningOutcome.Dismissed, day.AddMinutes(5));
        _store.Record(WarningLevel.Banner, WarningOutcome.Ignored, day.AddMinutes(10));

        var summaries = _store.GetDailySummaries(7);
        var bannerRow = Assert.Single(summaries, s => s.Date == "2026-11-13" && s.LevelId == (int)WarningLevel.Banner);

        Assert.Equal(1, bannerRow.SatUpCount);
        Assert.Equal(1, bannerRow.DismissedCount);
        Assert.Equal(1, bannerRow.IgnoredCount);
        Assert.Equal(0, bannerRow.HotkeyExitCount);
    }

    [Fact]
    public void Record_TracksBannerAndOverlaySeparatelyOnSameDay()
    {
        var day = new DateTime(2026, 11, 13, 15, 0, 0);

        _store.Record(WarningLevel.Banner, WarningOutcome.Ignored, day);
        _store.Record(WarningLevel.Overlay, WarningOutcome.SatUp, day.AddSeconds(25));
        _store.Record(WarningLevel.Overlay, WarningOutcome.HotkeyExit, day.AddMinutes(15));

        var summaries = _store.GetDailySummaries(7);

        var bannerRow = Assert.Single(summaries, s => s.Date == "2026-11-13" && s.LevelId == (int)WarningLevel.Banner);
        Assert.Equal(1, bannerRow.IgnoredCount);

        var overlayRow = Assert.Single(summaries, s => s.Date == "2026-11-13" && s.LevelId == (int)WarningLevel.Overlay);
        Assert.Equal(1, overlayRow.SatUpCount);
        Assert.Equal(1, overlayRow.HotkeyExitCount);
    }

    [Fact]
    public void GetDayStats_CountsSlouchesAndOutcomesPerLevelForThatDayOnly()
    {
        var day = new DateTime(2026, 11, 13, 10, 0, 0);
        _store.Record(WarningLevel.Banner, WarningOutcome.Ignored, day);
        _store.Record(WarningLevel.Overlay, WarningOutcome.HotkeyExit, day.AddSeconds(25));
        _store.Record(WarningLevel.Banner, WarningOutcome.SatUp, day.AddMinutes(30));
        _store.Record(WarningLevel.Banner, WarningOutcome.Dismissed, day.AddDays(1));   // another day

        var stats = _store.GetDayStats(DateOnly.FromDateTime(day));

        Assert.Equal(2, stats.SlouchCount);                 // two banners shown that day
        Assert.Equal(1, stats.Banner.Ignored);
        Assert.Equal(1, stats.Banner.SatUp);
        Assert.Equal(0, stats.Banner.Dismissed);
        Assert.Equal(1, stats.Overlay.HotkeyExit);
    }

    [Fact]
    public void GetDayStats_ReturnsZerosWhenNothingHappened()
    {
        var stats = _store.GetDayStats(new DateOnly(2026, 1, 1));

        Assert.Equal(0, stats.SlouchCount);
        Assert.Equal(0, stats.Banner.Total);
        Assert.Equal(0, stats.Overlay.Total);
    }
}
