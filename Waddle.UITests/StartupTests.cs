using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Waddle.UITests
{
  // Same test class as MainWindowTests.cs (so it shares the launch/cleanup helpers); this file holds the
  // "start with Windows" tests: first-run overlay and the Settings overlay (both full-screen windows).
  public partial class WaddleAppTests
  {
    private const string PromptTitle = "Waddle - Start with Windows";
    private const string SettingsTitle = "Waddle - Settings";
    private const string StartupQuestion = "How should Waddle start?";
    private const string StartupCheckboxText = "Launch Waddle when I sign in to Windows";
    private const string AutoTimerCheckboxText = "Start posture timer automatically on launch";

    private void PretendItIsAFreshInstall()
    {
      if (File.Exists(_askedFlagPath)) File.Delete(_askedFlagPath);
    }

    private static string? ReadStartupEntry()
    {
      using var key = Registry.CurrentUser.OpenSubKey(UiTestRunKey);
      return key?.GetValue("Waddle") as string;
    }

    private static Button FindButton(Window window, string name)
    {
      var button = window.FindFirstDescendant(cf => cf.ByName(name))?.AsButton();
      Assert.NotNull(button);
      return button!;
    }

    // Settings is a full-screen overlay window (like the calibration one), so it is a top-level window.
    private Window OpenSettingsDialog(Window mainWindow)
    {
      FindButton(mainWindow, "Settings").Invoke();
      return WaitForWindowByTitle(SettingsTitle, 10);
    }

    private static CheckBox FindStartupCheckbox(Window settings)
    {
      var box = settings.FindFirstDescendant(cf => cf.ByName(StartupCheckboxText))?.AsCheckBox();
      Assert.NotNull(box);
      return box!;
    }

    private static CheckBox FindAutoTimerCheckbox(Window settings)
    {
      var box = settings.FindFirstDescendant(cf => cf.ByName(AutoTimerCheckboxText))?.AsCheckBox();
      Assert.NotNull(box);
      return box!;
    }

    private void RestartApp()
    {
      _automation?.Dispose();
      if (_app is { HasExited: false }) _app.Kill();
      _app?.Dispose();
      Thread.Sleep(500);
      SqliteConnection.ClearAllPools();
      LaunchApp();
    }

    // ---- first-run overlay ----

    [Fact]
    public void FirstInstall_ShowsTheStartupQuestion_WithBothOptionsUnticked()
    {
      PretendItIsAFreshInstall();
      LaunchApp();

      var prompt = WaitForWindowByTitle(PromptTitle, 15);

      Assert.NotNull(prompt.FindFirstDescendant(cf => cf.ByName(StartupQuestion)));
      Assert.False(FindStartupCheckbox(prompt).IsChecked);
      Assert.False(FindAutoTimerCheckbox(prompt).IsChecked);
      Assert.True(FindButton(prompt, "Continue").IsEnabled);
    }

    [Fact]
    public void FirstInstall_TickingSignIn_TurnsStartupOn_ThenContinuesToCalibration()
    {
      PretendItIsAFreshInstall();
      LaunchApp();
      var prompt = WaitForWindowByTitle(PromptTitle, 15);

      FindStartupCheckbox(prompt).Toggle();
      FindButton(prompt, "Continue").Invoke();

      var entry = Retry.WhileNull(() => ReadStartupEntry(), timeout: TimeSpan.FromSeconds(5));
      Assert.NotNull(entry.Result);
      Assert.EndsWith("\"", entry.Result!);                       // the path is stored in quotes
      Assert.False(File.Exists(_autoTimerFlagPath));              // the timer option was left unticked
      var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();  // the usual first-run flow carries on
      Assert.Equal("Waddle", mainWindow.Title);
    }

    [Fact]
    public void FirstInstall_TickingTimer_TurnsAutoStartOn_WithoutSignIn()
    {
      PretendItIsAFreshInstall();
      LaunchApp();
      var prompt = WaitForWindowByTitle(PromptTitle, 15);

      FindAutoTimerCheckbox(prompt).Toggle();
      FindButton(prompt, "Continue").Invoke();

      var marker = Retry.WhileFalse(() => File.Exists(_autoTimerFlagPath), timeout: TimeSpan.FromSeconds(5));
      Assert.True(marker.Success);
      Assert.Null(ReadStartupEntry());
    }

    [Fact]
    public void FirstInstall_ContinueWithNothingTicked_LeavesBothOff_ThenContinuesToCalibration()
    {
      PretendItIsAFreshInstall();
      LaunchApp();
      var prompt = WaitForWindowByTitle(PromptTitle, 15);

      FindButton(prompt, "Continue").Invoke();

      var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();
      Assert.Equal("Waddle", mainWindow.Title);
      Assert.Null(ReadStartupEntry());
      Assert.False(File.Exists(_autoTimerFlagPath));
    }

    [Fact]
    public void StartupQuestion_IsOnlyAskedOnce()
    {
      SeedCalibration(0.25);
      PretendItIsAFreshInstall();
      LaunchApp();
      FindButton(WaitForWindowByTitle(PromptTitle, 15), "Continue").Invoke();
      Assert.NotNull(_app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10)));

      RestartApp();

      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));
      Assert.Equal("Waddle", mainWindow!.Title);
      var prompt = _app.GetAllTopLevelWindows(_automation!).FirstOrDefault(w => w.Title == PromptTitle);
      Assert.Null(prompt);
    }

    [Fact]
    public void AlreadyAnswered_GoesStraightToTheDashboard_WithNoQuestion()
    {
      SeedCalibration(0.25);
      LaunchApp();

      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10));

      Assert.Equal("Waddle", mainWindow!.Title);
      Assert.Null(_app.GetAllTopLevelWindows(_automation!).FirstOrDefault(w => w.Title == PromptTitle));
    }

    // ---- settings button and dialog ----

    [Fact]
    public void Dashboard_HasASettingsButton_ThatOpensTheDialog()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

      var settings = OpenSettingsDialog(mainWindow);

      Assert.NotNull(FindStartupCheckbox(settings));
      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_StartsUnticked_WhenStartupIsOff()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

      var settings = OpenSettingsDialog(mainWindow);

      Assert.False(FindStartupCheckbox(settings).IsChecked);
      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_StartsTicked_WhenStartupIsAlreadyOn()
    {
      using (var key = Registry.CurrentUser.CreateSubKey(UiTestRunKey))
        key.SetValue("Waddle", "\"C:\\\\Waddle\\\\Waddle.Wpf.exe\"");
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

      var settings = OpenSettingsDialog(mainWindow);

      Assert.True(FindStartupCheckbox(settings).IsChecked);
      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_Toggle_TurnsStartupOnThenOff()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;
      var settings = OpenSettingsDialog(mainWindow);
      var checkbox = FindStartupCheckbox(settings);

      checkbox.Toggle();
      var on = Retry.WhileNull(() => ReadStartupEntry(), timeout: TimeSpan.FromSeconds(5));
      Assert.NotNull(on.Result);

      checkbox.Toggle();
      var off = Retry.WhileFalse(() => ReadStartupEntry() is null, timeout: TimeSpan.FromSeconds(5));
      Assert.True(off.Success);

      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_Done_ClosesItAndReturnsToTheDashboard()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;
      var settings = OpenSettingsDialog(mainWindow);

      FindButton(settings, "Done").Invoke();

      var gone = Retry.WhileFalse(
          () => _app!.GetAllTopLevelWindows(_automation!).All(w => w.Title != SettingsTitle),
          timeout: TimeSpan.FromSeconds(5));
      Assert.True(gone.Success);
      Assert.NotNull(mainWindow.FindFirstDescendant(cf => cf.ByName("Sat up today")));
    }

    [Fact]
    public void SettingsDialog_HasBothStartupCheckboxes_BothUnticked_ByDefault()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

      var settings = OpenSettingsDialog(mainWindow);

      Assert.False(FindStartupCheckbox(settings).IsChecked);
      Assert.False(FindAutoTimerCheckbox(settings).IsChecked);
      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_AutoTimerToggle_WritesAndRemovesTheMarker_WithoutTouchingSignIn()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;
      var settings = OpenSettingsDialog(mainWindow);
      var checkbox = FindAutoTimerCheckbox(settings);

      checkbox.Toggle();
      var on = Retry.WhileFalse(() => File.Exists(_autoTimerFlagPath), timeout: TimeSpan.FromSeconds(5));
      Assert.True(on.Success);
      Assert.Null(ReadStartupEntry());                // the Windows sign-in entry is a separate setting

      checkbox.Toggle();
      var off = Retry.WhileFalse(() => !File.Exists(_autoTimerFlagPath), timeout: TimeSpan.FromSeconds(5));
      Assert.True(off.Success);

      FindButton(settings, "Done").Invoke();
    }

    [Fact]
    public void SettingsDialog_StartsTimerTicked_WhenAutoStartIsAlreadyOn()
    {
      File.WriteAllText(_autoTimerFlagPath, "1");
      SeedCalibration(0.25);
      LaunchApp();
      var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

      var settings = OpenSettingsDialog(mainWindow);

      Assert.True(FindAutoTimerCheckbox(settings).IsChecked);
      FindButton(settings, "Done").Invoke();
    }

    [Fact]

    public void SettingsChoice_IsKeptAfterRestartingTheApp()
    {
      SeedCalibration(0.25);
      LaunchApp();
      var settings = OpenSettingsDialog(_app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!);
      FindStartupCheckbox(settings).Toggle();
      Assert.NotNull(Retry.WhileNull(() => ReadStartupEntry(), timeout: TimeSpan.FromSeconds(5)).Result);
      FindButton(settings, "Done").Invoke();
      RestartApp();
      var reopened = OpenSettingsDialog(_app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!);
      Assert.True(FindStartupCheckbox(reopened).IsChecked);
      FindButton(reopened, "Done").Invoke();
    }
  }
}
