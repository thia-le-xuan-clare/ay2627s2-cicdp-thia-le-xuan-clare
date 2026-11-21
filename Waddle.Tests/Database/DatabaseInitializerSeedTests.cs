using System.IO;

namespace Waddle.Tests;

// Seed() reads a .sql file from <test output>/Database. That file may or may not be copied next to the tests,
// so each test puts it the way it needs it and the original (if any) is restored afterwards.
public sealed class DatabaseInitializerSeedTests : IDisposable
{
  private readonly TempDatabase _temp = new();
  private readonly string _seedDirectory = Path.Combine(AppContext.BaseDirectory, "Database");
  private readonly string _seedFile;
  private readonly string _backupFile;
  private readonly bool _hadOriginal;

  public DatabaseInitializerSeedTests()
  {
    _seedFile = Path.Combine(_seedDirectory, "seed_oct_2026.sql");
    _backupFile = _seedFile + ".test-backup";
    _hadOriginal = File.Exists(_seedFile);
    if (_hadOriginal) File.Move(_seedFile, _backupFile, overwrite: true);
  }

  public void Dispose()
  {
    if (File.Exists(_seedFile)) File.Delete(_seedFile);
    if (_hadOriginal) File.Move(_backupFile, _seedFile, overwrite: true);
    _temp.Dispose();
  }

  [Fact]
  public void Seed_WhenTheSeedFileIsMissing_SaysWhereItLooked()
  {
    var error = Assert.Throws<FileNotFoundException>(() => _temp.Db.Seed());

    Assert.Contains("seed_oct_2026.sql", error.Message);
  }

  [Fact]
  public void Seed_RunsTheSqlInTheSeedFile()
  {
    Directory.CreateDirectory(_seedDirectory);
    File.WriteAllText(_seedFile, "INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-01', 60);");

    _temp.Db.Seed();

    Assert.Equal(60, _temp.Scalar<int>("SELECT seconds_good FROM daily_history WHERE date = '2026-10-01'"));
  }
}
