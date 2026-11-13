using Waddle.Infrastructure.Data;

namespace Waddle.Tests;

public sealed class SqliteDailyTimeStoreTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    private readonly TempDatabase _temp = new();
    private readonly SqliteDailyTimeStore _store;

    public SqliteDailyTimeStoreTests() => _store = new SqliteDailyTimeStore(_temp.Db);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void GetSeconds_ForADayWithNoRow_ReturnsZero()
    {
        Assert.Equal(0, _store.GetSeconds(Today));
    }

    [Fact]
    public void SetSeconds_ThenGetSeconds_ReturnsTheSavedValue()
    {
        _store.SetSeconds(Today, 1234);

        Assert.Equal(1234, _store.GetSeconds(Today));
    }

    [Fact]
    public void SetSeconds_Twice_UpdatesTheSameRow()
    {
        _store.SetSeconds(Today, 100);
        var firstId = _temp.Scalar<long>("SELECT day_id FROM daily_history WHERE date = '2026-11-12'");

        _store.SetSeconds(Today, 250);
        var secondId = _temp.Scalar<long>("SELECT day_id FROM daily_history WHERE date = '2026-11-12'");

        // the upsert must keep day_id, because warning_events points at it
        Assert.Equal(firstId, secondId);
        Assert.Equal(250, _store.GetSeconds(Today));
        Assert.Equal(1, _temp.Scalar<int>("SELECT COUNT(*) FROM daily_history"));
    }

    [Fact]
    public void SetSeconds_WithANegativeNumber_StoresZero()
    {
        _store.SetSeconds(Today, -50);

        Assert.Equal(0, _store.GetSeconds(Today));
    }

    [Fact]
    public void DifferentDays_AreStoredSeparately()
    {
        var yesterday = Today.AddDays(-1);

        _store.SetSeconds(Today, 300);
        _store.SetSeconds(yesterday, 900);

        Assert.Equal(300, _store.GetSeconds(Today));
        Assert.Equal(900, _store.GetSeconds(yesterday));
    }

    [Fact]
    public void SetSeconds_KeepsTheRowWhenAWarningEventPointsAtIt()
    {
        _store.SetSeconds(Today, 100);
        var dayId = _temp.Scalar<long>("SELECT day_id FROM daily_history WHERE date = '2026-11-12'");
        _temp.Execute($"INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) VALUES ({dayId}, 1, 1, '2026-11-12T10:00:00')");

        _store.SetSeconds(Today, 500);     // would break the foreign key if it deleted and re-inserted the row

        Assert.Equal(500, _store.GetSeconds(Today));
        Assert.Equal(1, _temp.Scalar<int>("SELECT COUNT(*) FROM warning_events"));
    }
}
