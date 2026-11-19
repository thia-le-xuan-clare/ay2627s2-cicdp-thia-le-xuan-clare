using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Waddle.UITests
{
    // Same test class as MainWindowTests.cs (so it shares the launch/cleanup helpers); this file holds the
    // "start with Windows" tests: first-run overlay and the Settings dialog.
    public partial class WaddleAppTests
    {
        private const string PromptTitle = "Waddle - Start with Windows";
        private const string SettingsTitle = "Waddle - Settings";
        private const string StartupQuestion = "Do you want Waddle to start when you log in?";
        private const string StartupCheckboxText = "Start Waddle when I log in to Windows";

        private void PretendItIsAFreshInstall()
        {
            if (File.Exists(_askedFlagPath)) File.Delete(_askedFlagPath);
        }

        private static string? ReadStartupEntry()
        {
            using var key = Registry.CurrentUser.OpenSubKey(UiTestRunKey);
            return key?.GetValue("Waddle") as string;
        }

        private Button FindButton(Window window, string name)
        {
            var button = window.FindFirstDescendant(cf => cf.ByName(name))?.AsButton();
            Assert.NotNull(button);
            return button!;
        }

        private Window OpenSettingsDialog(Window mainWindow)
        {
            FindButton(mainWindow, "Settings").Invoke();

            // The dialog is modal and owned by the dashboard, so UI Automation lists it under the dashboard
            // (ModalWindows), not as a separate top-level window like the calibration overlay.
            var found = Retry.WhileNull(
                () => mainWindow.ModalWindows.FirstOrDefault(w => w.Title == SettingsTitle)
                      ?? _app!.GetAllTopLevelWindows(_automation!).FirstOrDefault(w => w.Title == SettingsTitle),
                timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(found.Result);
            return found.Result!;
        }

        private CheckBox FindStartupCheckbox(Window settings)
        {
            var box = settings.FindFirstDescendant(cf => cf.ByName(StartupCheckboxText))?.AsCheckBox();
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
        public void FirstInstall_ShowsTheStartupQuestion_WithBothButtons()
        {
            PretendItIsAFreshInstall();
            LaunchApp();

            var prompt = WaitForWindowByTitle(PromptTitle, 15);

            Assert.NotNull(prompt.FindFirstDescendant(cf => cf.ByName(StartupQuestion)));
            Assert.True(FindButton(prompt, "Yes, start on login").IsEnabled);
            Assert.True(FindButton(prompt, "No thanks").IsEnabled);
        }

        [Fact]
        public void FirstInstall_Yes_TurnsStartupOn_ThenContinuesToCalibration()
        {
            PretendItIsAFreshInstall();
            LaunchApp();
            var prompt = WaitForWindowByTitle(PromptTitle, 15);

            FindButton(prompt, "Yes, start on login").Invoke();

            var entry = Retry.WhileNull(() => ReadStartupEntry(), timeout: TimeSpan.FromSeconds(5));
            Assert.NotNull(entry.Result);
            Assert.EndsWith("\"", entry.Result!);                       // the path is stored in quotes
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();  // the usual first-run flow carries on
            Assert.Equal("Waddle", mainWindow.Title);
        }

        [Fact]
        public void FirstInstall_No_LeavesStartupOff_ThenContinuesToCalibration()
        {
            PretendItIsAFreshInstall();
            LaunchApp();
            var prompt = WaitForWindowByTitle(PromptTitle, 15);

            FindButton(prompt, "No thanks").Invoke();

            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();
            Assert.Equal("Waddle", mainWindow.Title);
            Assert.Null(ReadStartupEntry());
        }

        [Fact]
        public void StartupQuestion_IsOnlyAskedOnce()
        {
            SeedCalibration(0.25);
            PretendItIsAFreshInstall();
            LaunchApp();
            FindButton(WaitForWindowByTitle(PromptTitle, 15), "No thanks").Invoke();
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
                () => mainWindow.ModalWindows.All(w => w.Title != SettingsTitle)
                      && _app!.GetAllTopLevelWindows(_automation!).All(w => w.Title != SettingsTitle),
                timeout: TimeSpan.FromSeconds(5));
            Assert.True(gone.Success);
            Assert.NotNull(mainWindow.FindFirstDescendant(cf => cf.ByName("Sat up today")));
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