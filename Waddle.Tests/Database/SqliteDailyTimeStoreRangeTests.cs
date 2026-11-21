using Waddle.Infrastructure.Data;

namespace Waddle.Tests;

public sealed class SqliteDailyTimeStoreRangeTests : IDisposable
{
  private static readonly DateOnly Start = new(2026, 11, 10);

  private readonly TempDatabase _temp = new();
  private readonly SqliteDailyTimeStore _store;

  public SqliteDailyTimeStoreRangeTests()
  {
    _store = new SqliteDailyTimeStore(_temp.Db);
    for (var i = 0; i < 5; i++)
      _store.SetSeconds(Start.AddDays(i), 100 * (i + 1));      // 10th: 100 ... 14th: 500
  }

  public void Dispose() => _temp.Dispose();

  [Fact]
  public void GetSecondsRange_ReturnsEveryDayInsideTheRange_IncludingBothEnds()
  {
    var result = _store.GetSecondsRange(Start.AddDays(1), Start.AddDays(3));

    Assert.Equal(3, result.Count);
    Assert.Equal(200, result[Start.AddDays(1)]);
    Assert.Equal(300, result[Start.AddDays(2)]);
    Assert.Equal(400, result[Start.AddDays(3)]);
  }

  [Fact]
  public void GetSecondsRange_LeavesOutDaysOutsideTheRange()
  {
    var result = _store.GetSecondsRange(Start.AddDays(1), Start.AddDays(3));

    Assert.False(result.ContainsKey(Start));
    Assert.False(result.ContainsKey(Start.AddDays(4)));
  }

  [Fact]
  public void GetSecondsRange_LeavesOutDaysWithNoRow()
  {
    var result = _store.GetSecondsRange(Start.AddDays(3), Start.AddDays(10));

    Assert.Equal(2, result.Count);                           // only the 13th and 14th were ever saved
  }

  [Fact]
  public void GetSecondsRange_WithNothingSaved_ReturnsAnEmptyResult()
  {
    var result = _store.GetSecondsRange(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));

    Assert.Empty(result);
  }

  [Fact]
  public void GetSecondsRange_ForASingleDay_ReturnsThatDay()
  {
    var result = _store.GetSecondsRange(Start.AddDays(2), Start.AddDays(2));

    Assert.Equal(300, Assert.Single(result).Value);
  }
}
