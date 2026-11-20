using System.IO;
using Microsoft.Data.Sqlite;
using Waddle.Infrastructure;
using Waddle.Infrastructure.Data;

namespace Waddle.Tests.Security;

/// <summary>
/// Security-focused tests: hostile or tampered input should be rejected or neutralised.
/// </summary>
public sealed class SecurityTests : IDisposable
{
  private readonly TempDatabase _temp = new();
  private readonly string _logDir = Path.Combine(Path.GetTempPath(), $"waddle-sec-{Guid.NewGuid():N}");

  public void Dispose()
  {
    _temp.Dispose();
    if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true);
  }

  // ---- Log injection: a message must never be able to forge extra log lines ----

  [Fact]
  public void Security_Log_NewlinesInMessage_CannotForgeExtraLogLines()
  {
    var log = new FileLogService(_logDir);

    log.Security("camera on\r\n2099-01-01 00:00:00.000 [SECURITY] Camera off");

    var lines = File.ReadAllLines(Path.Combine(_logDir, "security.log"));
    Assert.Single(lines);
  }

  [Fact]
  public void Info_Log_NewlinesInMessage_CannotForgeExtraLogLines()
  {
    var log = new FileLogService(_logDir);

    log.Info("hello\nfake line");

    Assert.Single(File.ReadAllLines(Path.Combine(_logDir, "info.log")));
  }

  // ---- SQL injection: hostile text must be stored/rejected as data, never executed ----

  [Fact]
  public void Database_RejectsInjectionStyleDate_AndKeepsAllTables()
  {
    Assert.ThrowsAny<SqliteException>(() =>
        _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('2026-01-01''; DROP TABLE settings;--', 0);"));

    // Tables are all still there
    Assert.Equal(1, _temp.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'settings';"));
    Assert.Equal(1, _temp.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'daily_history';"));
  }

  [Fact]
  public void CalibrationStore_Load_WhenRowHasBeenTamperedWith_ReturnsNullInsteadOfCrashing()
  {
    _temp.Execute("INSERT INTO settings (settings_key, face_value, calibrated_at) VALUES (1, 0.5, '<script>not a date</script>');");
    var store = new SqliteCalibrationStore(_temp.Db);

    var error = Record.Exception(() => store.Load());

    Assert.Null(error);
    Assert.Null(store.Load());
  }

  // ---- Data integrity: the schema itself should refuse bad data (defence in depth) ----

  [Theory]
  [InlineData("0")]
  [InlineData("-1")]
  [InlineData("5.0")]
  public void Database_RejectsCalibrationOutsideValidRange(string faceValue)
  {
    Assert.ThrowsAny<SqliteException>(() =>
        _temp.Execute($"INSERT INTO settings (settings_key, face_value, calibrated_at) VALUES (1, {faceValue}, '2026-01-01');"));
  }

  [Fact]
  public void Database_RejectsNegativeSecondsGood()
  {
    Assert.ThrowsAny<SqliteException>(() =>
        _temp.Execute("INSERT INTO daily_history (date, seconds_good) VALUES ('2026-01-01', -5);"));
  }

  [Fact]
  public void Database_RejectsWarningEventForNonexistentDay()
  {
    Assert.ThrowsAny<SqliteException>(() =>
        _temp.Execute("INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) VALUES (999, 1, 1, '2026-01-01T00:00:00');"));
  }
}
