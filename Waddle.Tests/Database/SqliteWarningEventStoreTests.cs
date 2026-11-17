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
}
