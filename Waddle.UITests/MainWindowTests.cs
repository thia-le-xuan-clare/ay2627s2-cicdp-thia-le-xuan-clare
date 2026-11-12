using System;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace Waddle.UITests
{
    public class WaddleAppTests : IDisposable
    {
        private readonly Application _app;
        private readonly UIA3Automation _automation;
        private readonly string _dbPath;

        public WaddleAppTests()
        {
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Waddle",
                "waddle.db");

            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            var baseDir = AppContext.BaseDirectory;
            var exePath = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..",
                "Waddle.Wpf", "bin", "Release", "net10.0-windows", "Waddle.Wpf.exe"));

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Could not find WPF executable at: {exePath}");
            }

            _app = Application.Launch(exePath);
            _automation = new UIA3Automation();
        }

        private Window WaitForWindowByTitle(string title, int timeoutSeconds = 15)
        {
            var result = Retry.WhileNull(
                () => _app.GetAllTopLevelWindows(_automation)
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

            var mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
            Assert.NotNull(mainWindow);
            return mainWindow!;
        }

        [Fact]
        public void App_Launches_And_ShowsMainWindow()
        {
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();
            Assert.False(string.IsNullOrWhiteSpace(mainWindow.Title));
        }

        [Fact]
        public void Calibration_FirstRun_ShowsGetStarted_And_HandlesMissingCamera()
        {
            var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 15);

            var header = calWindow.FindFirstDescendant(cf => cf.ByName("Get started!"));
            Assert.NotNull(header);

            var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
            Assert.NotNull(exitBtn);
            Assert.True(exitBtn!.IsEnabled);

            var tryAgainResult = Retry.WhileNull(
                () => calWindow.FindFirstDescendant(cf => cf.ByName("Try again"))?.AsButton(),
                timeout: TimeSpan.FromSeconds(10));

            Assert.NotNull(tryAgainResult.Result);
            Assert.True(tryAgainResult.Result!.IsEnabled);
        }

        [Fact]
        public void MainWindow_CalibrateButton_OpensCalibrationOverlay()
        {
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();

            // Exact button Content from MainWindow.xaml: "Calibrate face size"
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
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();

            // TimerButtonText starts with "Start" (e.g. "Start" or "Start session")
            var startBtn = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton();
            Assert.NotNull(startBtn);
            startBtn!.Invoke();

            // StartSessionAsync opens CalibrationWindow because no calibration exists yet
            var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 10);
            Assert.NotNull(calWindow);

            // Exit without calibrating -> timer remains stopped
            var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
            Assert.NotNull(exitBtn);
            exitBtn!.Invoke();

            var startBtnAfter = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Start", PropertyConditionFlags.MatchSubstring))?.AsButton();
            Assert.NotNull(startBtnAfter);
        }

        public void Dispose()
        {
            _automation?.Dispose();
            if (_app != null && !_app.HasExited)
            {
                _app.Close();
            }
            _app?.Dispose();
        }
    }
}
