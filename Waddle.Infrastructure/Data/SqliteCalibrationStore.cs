using System.Globalization;
using Microsoft.Data.Sqlite;
using Waddle.Core;

namespace Waddle.Infrastructure.Data;

/// Stores the calibration as row 1 of the settings table.
public sealed class SqliteCalibrationStore : ICalibrationStore
{
    private const int CalibrationKey = 1;
    private readonly DatabaseInitializer _db;

    public SqliteCalibrationStore(DatabaseInitializer db) => _db = db;

    public CalibrationData? Load()
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT face_value, calibrated_at FROM settings WHERE settings_key = $key;";
            command.Parameters.AddWithValue("$key", CalibrationKey);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            return new CalibrationData(
                reader.GetDouble(0),
                DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }
        catch (Exception ex) when (ex is SqliteException or FormatException)
        {
            return null;    // unreadable row = treat as "never calibrated"
        }
    }

    public void Save(CalibrationData data)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (settings_key, face_value, calibrated_at)
            VALUES ($key, $ratio, $at)
            ON CONFLICT(settings_key) DO UPDATE SET
                face_value = excluded.face_value,
                calibrated_at = excluded.calibrated_at;
            """;
        command.Parameters.AddWithValue("$key", CalibrationKey);
        command.Parameters.AddWithValue("$ratio", data.FaceRatio);
        command.Parameters.AddWithValue("$at", data.CalibratedAt.ToString("o", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }
}