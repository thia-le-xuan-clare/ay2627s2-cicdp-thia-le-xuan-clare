using Waddle.Core;

namespace Waddle.Infrastructure.Data;

public class SqliteWarningEventStore : IWarningEventStore
{
    private readonly DatabaseInitializer _db;

    public SqliteWarningEventStore(DatabaseInitializer db)
    {
        _db = db;
    }

    public void Record(WarningLevel level, WarningOutcome outcome, DateTime occurredAt)
    {
        using var connection = _db.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var dateText = occurredAt.ToString("yyyy-MM-dd");
        var timestampText = occurredAt.ToString("O");

        // Ensure today's row exists in daily_history so the foreign key succeeds
        using (var ensureDay = connection.CreateCommand())
        {
            ensureDay.Transaction = transaction;
            ensureDay.CommandText = """
                INSERT OR IGNORE INTO daily_history (date, seconds_good)
                VALUES ($date, 0);
                """;
            ensureDay.Parameters.AddWithValue("$date", dateText);
            ensureDay.ExecuteNonQuery();
        }

        // Insert into warning_events
        using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.Transaction = transaction;
            insertEvent.CommandText = """
                INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at)
                SELECT day_id, $levelId, $outcomeId, $occurredAt
                FROM daily_history
                WHERE date = $date;
                """;
            insertEvent.Parameters.AddWithValue("$levelId", (int)level);
            insertEvent.Parameters.AddWithValue("$outcomeId", (int)outcome);
            insertEvent.Parameters.AddWithValue("$occurredAt", timestampText);
            insertEvent.Parameters.AddWithValue("$date", dateText);
            insertEvent.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // Ready for your future dashboard
    public IReadOnlyList<DailyWarningSummary> GetDailySummaries(int lastNDays = 7)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                d.date,
                COALESCE(w.level_id, 0) AS level_id,
                SUM(CASE WHEN w.outcome_id = 1 THEN 1 ELSE 0 END) AS sat_up,
                SUM(CASE WHEN w.outcome_id = 2 THEN 1 ELSE 0 END) AS hotkey_exit,
                SUM(CASE WHEN w.outcome_id = 3 THEN 1 ELSE 0 END) AS ignored,
                SUM(CASE WHEN w.outcome_id = 4 THEN 1 ELSE 0 END) AS dismissed
            FROM daily_history d
            LEFT JOIN warning_events w ON w.day_id = d.day_id
            GROUP BY d.day_id, d.date, w.level_id
            ORDER BY d.date DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", lastNDays * 2);

        var results = new List<DailyWarningSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DailyWarningSummary(
                Date: reader.GetString(0),
                LevelId: reader.GetInt32(1),
                SatUpCount: reader.GetInt32(2),
                HotkeyExitCount: reader.GetInt32(3),
                IgnoredCount: reader.GetInt32(4),
                DismissedCount: reader.GetInt32(5)
            ));
        }

        return results;
    }
}
