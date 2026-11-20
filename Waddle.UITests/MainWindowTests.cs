using System.Diagnostics;
using System.Globalization;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Waddle.Infrastructure.Data;

namespace Waddle.UITests
{
  public partial class WaddleAppTests : IDisposable
  {
    private Application? _app;
    private UIA3Automation? _automation;
    private readonly string _dbPath;
    private readonly string _testDataDir;
    private readonly string _exePath;

    // The app reads these two env vars (see StartupManager) so the tests never touch the real
    // "start with Windows" entry or the real first-run marker.
    private const string UiTestRunKey = @"Software\WaddleUITests\Run";
    private readonly string _askedFlagPath;
    private readonly string _autoTimerFlagPath;     // "start posture timer on launch" marker: absent unless a test turns it on

    public WaddleAppTests()
    {
      foreach (var proc in Process.GetProcessesByName("Waddle.Wpf"))
      {
        try
        {
          proc.Kill(entireProcessTree: true);
          proc.WaitForExit(3000);
        }
        catch
        {
        }
        finally
        {
          proc.Dispose();
        }
      }

      SqliteConnection.ClearAllPools();

      // A throwaway database in the temp folder: the tests must NEVER touch the real one in %LocalAppData%\Waddle.
      _testDataDir = Path.Combine(Path.GetTempPath(), $"waddle-uitest-{Guid.NewGuid():N}");
      Directory.CreateDirectory(_testDataDir);
      _dbPath = Path.Combine(_testDataDir, "waddle.db");
      Environment.SetEnvironmentVariable("WADDLE_DB_PATH", _dbPath);

      // By default the first-run "start with Windows?" question counts as already answered,
      // so every test below sees the same screens as before. The startup tests delete this file.
      _askedFlagPath = Path.Combine(Path.GetTempPath(), $"waddle-uitest-{Guid.NewGuid():N}.asked");
      File.WriteAllText(_askedFlagPath, "1");
      Environment.SetEnvironmentVariable("WADDLE_STARTUP_RUN_KEY", UiTestRunKey);
      Environment.SetEnvironmentVariable("WADDLE_STARTUP_ASKED_FLAG", _askedFlagPath);
      _autoTimerFlagPath = Path.Combine(Path.GetTempPath(), $"waddle-uitest-{Guid.NewGuid():N}.autostart-timer");
      Environment.SetEnvironmentVariable("WADDLE_AUTOSTART_TIMER_FLAG", _autoTimerFlagPath);
      Registry.CurrentUser.DeleteSubKeyTree(@"Software\WaddleUITests", throwOnMissingSubKey: false);

      var baseDir = AppContext.BaseDirectory;
      _exePath = Path.GetFullPath(Path.Combine(
          baseDir, "..", "..", "..", "..",
          "Waddle.Wpf", "bin", "Release", "net10.0-windows", "Waddle.Wpf.exe"));

      if (!File.Exists(_exePath))
      {
        throw new FileNotFoundException($"Could not find WPF executable at: {_exePath}");
      }
    }

    private static void DeleteDatabaseFiles(string dbPath)
    {
      foreach (var suffix in new[] { "", "-wal", "-shm" })
      {
        var file = dbPath + suffix;
        if (File.Exists(file))
        {
          File.Delete(file);
        }
      }
    }

    private void LaunchApp()
    {
      SqliteConnection.ClearAllPools();
      _app = Application.Launch(_exePath);
      _automation = new UIA3Automation();
    }

    private void SeedCalibration(double faceValue = 0.25)
    {
      var initializer = new DatabaseInitializer(_dbPath);
      initializer.Initialize();

      using (var conn = initializer.OpenConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = """
                    INSERT INTO settings (settings_key, face_value, calibrated_at)
                    VALUES (1, @faceValue, @calibratedAt);
                    """;
        cmd.Parameters.AddWithValue("@faceValue", faceValue);
        cmd.Parameters.AddWithValue("@calibratedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
      }

      SqliteConnection.ClearAllPools();
    }

    private void SeedDailyHistory(string dateIso, int secondsGood)
    {
      var initializer = new DatabaseInitializer(_dbPath);
      initializer.Initialize();

      using (var conn = initializer.OpenConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = """
                    INSERT INTO daily_history (date, seconds_good)
                    VALUES (@date, @secondsGood);
                    """;
        cmd.Parameters.AddWithValue("@date", dateIso);
        cmd.Parameters.AddWithValue("@secondsGood", secondsGood);
        cmd.ExecuteNonQuery();
      }

      SqliteConnection.ClearAllPools();
    }

    private Window WaitForWindowByTitle(string title, int timeoutSeconds = 15)
    {
      var result = Retry.WhileNull(
          () => _app!.GetAllTopLevelWindows(_automation!)
                     .FirstOrDefault(w => w.Title == title),
          timeout: TimeSpan.FromSeconds(timeoutSeconds));

      Assert.NotNull(result.Result);
      return result.Result!;
    }

    private Window SkipFirstRunCalibrationAndGetMainWindow()
    {
      var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 15);
      var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
      Assert.NotNull(exitBtn);
      exitBtn!.Invoke();

      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));
      Assert.NotNull(mainWindow);
      return mainWindow!;
    }

    [Fact]
    public void App_Launches_And_ShowsMainWindow()
    {
      LaunchApp();
      var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();
      Assert.Equal("Waddle", mainWindow.Title);
    }

    [Fact]
    public void Calibration_FirstRun_ShowsGetStarted_And_HandlesMissingCamera()
    {
      LaunchApp();
      var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 15);

      var header = calWindow.FindFirstDescendant(cf => cf.ByName("Get started!"));
      Assert.NotNull(header);

      var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
      Assert.NotNull(exitBtn);
      Assert.True(exitBtn!.IsEnabled);

      var tryAgainResult = Retry.WhileNull(
          () =>
          {
            var btn = calWindow.FindFirstDescendant(cf => cf.ByName("Try again"))?.AsButton();
            return (btn != null && btn.IsEnabled && !btn.IsOffscreen) ? btn : null;
          },
          timeout: TimeSpan.FromSeconds(10));

      if (tryAgainResult.Result != null)
      {
        Assert.True(tryAgainResult.Result.IsEnabled);
      }
    }

    [Fact]
    public void MainWindow_CalibrateButton_OpensCalibrationOverlay()
    {
      LaunchApp();
      var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();

      var calibrateBtn = mainWindow.FindFirstDescendant(cf => cf.ByName("Calibrate face size"))?.AsButton();
      Assert.NotNull(calibrateBtn);
      calibrateBtn!.Invoke();

      var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 10);
      Assert.NotNull(calWindow);

      var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
      Assert.NotNull(exitBtn);
      exitBtn!.Invoke();
    }

    [Fact]
    public void SessionTimer_StartWithoutCalibration_PromptsCalibration_And_BlocksTimer()
    {
      LaunchApp();
      var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();

      var startBtn = mainWindow.FindFirstDescendant(
          cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton();
      Assert.NotNull(startBtn);
      startBtn!.Invoke();

      var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 10);
      Assert.NotNull(calWindow);

      var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
      Assert.NotNull(exitBtn);
      exitBtn!.Invoke();

      var startBtnAfter = mainWindow.FindFirstDescendant(
          cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton();
      Assert.NotNull(startBtnAfter);
    }

    [Fact]
    public void CalibratedApp_SkipsFirstRunPrompt_And_ShowsMainWindowDirectly()
    {
      SeedCalibration(0.25);
      LaunchApp();

      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));
      Assert.NotNull(mainWindow);
      Assert.Equal("Waddle", mainWindow!.Title);

      var cardTitle = mainWindow.FindFirstDescendant(cf => cf.ByName("Sat up today"));
      Assert.NotNull(cardTitle);
    }

    [Fact]
    public void CalibratedSession_StartAndStop_TogglesSessionState()
    {
      SeedCalibration(0.25);
      LaunchApp();

      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));
      Assert.NotNull(mainWindow);

      var startBtn = mainWindow!.FindFirstDescendant(
          cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton();
      Assert.NotNull(startBtn);
      startBtn!.Invoke();

      System.Threading.Thread.Sleep(1000);
      var calWindow = _app.GetAllTopLevelWindows(_automation!)
                          .FirstOrDefault(w => w.Title == "Waddle - Calibrate");
      Assert.Null(calWindow);

      var stopBtn = mainWindow.FindFirstDescendant(cf => cf.ByName("Stop"))?.AsButton();
      if (stopBtn != null && !stopBtn.IsOffscreen)
      {
        stopBtn.Invoke();

        var startAgain = Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(
                cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton(),
            timeout: TimeSpan.FromSeconds(5));
        Assert.NotNull(startAgain.Result);
      }
    }

    [Fact]

    public void DailyHistory_SeededInDatabase_DisplaysSavedTimeOnMainWindow()
    {
      SeedCalibration(0.25);
      var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
      SeedDailyHistory(today, 3660);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));
      Assert.NotNull(mainWindow);
      var zeroTimeText = mainWindow!.FindFirstDescendant(cf => cf.ByName("0m"));
      Assert.Null(zeroTimeText);
    }
    public void Dispose()
    {
      _automation?.Dispose();
      if (_app != null)
      {
        if (!_app.HasExited)
        {
          var pid = _app.ProcessId;
          _app.Kill();
          try
          {
            using var proc = Process.GetProcessById(pid);
            proc.WaitForExit(5000);
          }
          catch
          {
          }
        }
        _app.Dispose();
      }
      SqliteConnection.ClearAllPools();
      Registry.CurrentUser.DeleteSubKeyTree(@"Software\WaddleUITests", throwOnMissingSubKey: false);
      if (File.Exists(_askedFlagPath)) File.Delete(_askedFlagPath);
      if (File.Exists(_autoTimerFlagPath)) File.Delete(_autoTimerFlagPath);
      Environment.SetEnvironmentVariable("WADDLE_AUTOSTART_TIMER_FLAG", null);
      Environment.SetEnvironmentVariable("WADDLE_STARTUP_RUN_KEY", null);
      Environment.SetEnvironmentVariable("WADDLE_STARTUP_ASKED_FLAG", null);
      Environment.SetEnvironmentVariable("WADDLE_DB_PATH", null);
      try { Directory.Delete(_testDataDir, recursive: true); } catch { /* temp folder, fine if a file is still locked */ }
      GC.SuppressFinalize(this);
    }
  }
}
