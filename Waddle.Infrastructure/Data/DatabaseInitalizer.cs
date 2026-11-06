using Microsoft.Data.Sqlite;

namespace Waddle.Infrastructure.Data;

/// Creates the Waddle database file, its tables, and the fixed lookup values.
/// Needs the Microsoft.Data.Sqlite package in Waddle.Infrastructure.
public class DatabaseInitializer
{
    private readonly string _connectionString;

    // databasePath example (an app data folder, not the project or OneDrive folder):
    //   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waddle", "waddle.db")
    // Make sure the "Waddle" folder exists first with Directory.CreateDirectory.
    public DatabaseInitializer(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // SQLite only checks foreign keys if this is switched on for every connection.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    /// Safe to call every time the app starts: it only creates what is missing.
    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            -- GLOB is pattern checking to see if the date is YYYY-MM-DD for easy showing in history
            CREATE TABLE IF NOT EXISTS daily_history (
                day_id       INTEGER PRIMARY KEY,
                date         TEXT    NOT NULL UNIQUE
                             CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
                seconds_good INTEGER NOT NULL DEFAULT 0 CHECK (seconds_good >= 0)
            );

            CREATE TABLE IF NOT EXISTS warning_level (
                level_id   INTEGER PRIMARY KEY,
                level_name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS warning_outcome (
                outcome_id   INTEGER PRIMARY KEY,
                outcome_name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS warning_events (
                warning_id  INTEGER PRIMARY KEY,
                day_id      INTEGER NOT NULL REFERENCES daily_history(day_id),
                level_id    INTEGER NOT NULL REFERENCES warning_level(level_id),
                outcome_id  INTEGER NOT NULL REFERENCES warning_outcome(outcome_id),
                occurred_at TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                settings_key INTEGER PRIMARY KEY,
                face_value   REAL NOT NULL
            );

            -- Fixed values the app needs
            -- INSERT OR IGNORE means running this again does not create duplicates.
            INSERT OR IGNORE INTO warning_level (level_id, level_name)
                VALUES (1, 'Banner'), (2, 'Overlay');

            INSERT OR IGNORE INTO warning_outcome (outcome_id, outcome_name)
                VALUES (1, 'SatUp'), (2, 'HotkeyExit'), (3, 'Ignored');
            """;
        command.ExecuteNonQuery();
    }
}