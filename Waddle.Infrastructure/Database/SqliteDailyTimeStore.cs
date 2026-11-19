using Waddle.Core;

namespace Waddle.Infrastructure.Data;

/// Stores daily time in the existing daily_history table (date + seconds_good).
public sealed class SqliteDailyTimeStore : IDailyTimeStore
{
  private readonly DatabaseInitializer _db;

  public SqliteDailyTimeStore(DatabaseInitializer db) => _db = db;

  public int GetSeconds(DateOnly day)
  {
    using var connection = _db.OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT seconds_good FROM daily_history WHERE date = $date;";
    command.Parameters.AddWithValue("$date", Key(day));
    var result = command.ExecuteScalar();
    return result is null or DBNull ? 0 : Convert.ToInt32(result);
  }

  public void SetSeconds(DateOnly day, int seconds)
  {
    using var connection = _db.OpenConnection();
    using var command = connection.CreateCommand();
    // UPSERT, not REPLACE: REPLACE would delete the row and give it a new day_id,
    // which would break warning_events.day_id that points at it.
    command.CommandText = """
            INSERT INTO daily_history (date, seconds_good) VALUES ($date, $seconds)
            ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
            """;
    command.Parameters.AddWithValue("$date", Key(day));
    command.Parameters.AddWithValue("$seconds", Math.Max(0, seconds));
    command.ExecuteNonQuery();
  }

  public IReadOnlyDictionary<DateOnly, int> GetSecondsRange(DateOnly from, DateOnly to)
  {
    var result = new Dictionary<DateOnly, int>();
    using var connection = _db.OpenConnection();
    using var command = connection.CreateCommand();
    // yyyy-MM-dd strings sort in date order, so BETWEEN works
    command.CommandText = "SELECT date, seconds_good FROM daily_history WHERE date BETWEEN $from AND $to;";
    command.Parameters.AddWithValue("$from", Key(from));
    command.Parameters.AddWithValue("$to", Key(to));
    using var reader = command.ExecuteReader();
    while (reader.Read())
      result[DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd")] = reader.GetInt32(1);
    return result;
  }

  private static string Key(DateOnly day) => day.ToString("yyyy-MM-dd");
}
