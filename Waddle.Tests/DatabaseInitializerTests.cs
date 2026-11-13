using Microsoft.Data.Sqlite;

namespace Waddle.Tests;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("daily_history")]
    [InlineData("warning_level")]
    [InlineData("warning_outcome")]
    [InlineData("warning_events")]
    [InlineData("settings")]
    public void Initialize_CreatesTheTable(string table)
    {
        var count = _temp.Scalar<int>($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'");

        Assert.Equal(1, count);
    }

    [Fact]
    public void Initialize_SeedsTheFixedLookupValues()
    {
        Assert.Equal(2, _temp.Scalar<int>("SELECT COUNT(*) FROM warning_level"));
        Assert.Equal(3, _temp.Scalar<int>("SELECT COUNT(*) FROM warning_outcome"));
    }

    [Fact]
    public void Initialize_CalledAgain_DoesNotDuplicateAnything()
    {
        _temp.Db.Initialize();
        _temp.Db.Initialize();      // the app does this on every start

        Assert.Equal(2, _temp.Scalar<int>("SELECT COUNT(*) FROM warning_level"));
        Assert.Equal(3, _temp.Scalar<int>("SELECT COUNT(*) FROM warning_outcome"));
    }

    [Fact]
    public void OpenConnection_TurnsForeignKeysOn()
    {
        Assert.Equal(1, _temp.Scalar<int>("PRAGMA foreign_keys"));
    }

    [Fact]
    public void WarningEvent_ForADayThatDoesNotExist_IsRejected()
    {
        Assert.Throws<SqliteException>(() =>
            _temp.Execute("INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) VALUES (999, 1, 1, '2026-11-12T10:00:00')"));
    }

    [Fact]
    public void DailyHistory_RejectsADateNotInYearMonthDayFormat()
    {
        Assert.Throws<SqliteException>(() =>
            _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('12/11/2026', 10)"));
    }

    [Fact]
    public void DailyHistory_RejectsNegativeSeconds()
    {
        Assert.Throws<SqliteException>(() =>
            _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('2026-11-12', -1)"));
    }

    [Fact]
    public void DailyHistory_RejectsTheSameDateTwice()
    {
        _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('2026-11-12', 10)");

        Assert.Throws<SqliteException>(() =>
            _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('2026-11-12', 20)"));
    }
}
