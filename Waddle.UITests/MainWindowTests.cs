using System;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
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
            // Ensure a clean first-run database state for each test
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
            // Verifies first-run calibration opens, can be exited, and MainWindow loads
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();
            Assert.False(string.IsNullOrWhiteSpace(mainWindow.Title));
        }

        [Fact]
        public void Calibration_FirstRun_ShowsGetStarted_And_HandlesMissingCamera()
        {
            var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 15);

            // Verify first-run header is displayed
            var header = calWindow.FindFirstDescendant(cf => cf.ByName("Get started!"));
            Assert.NotNull(header);

            // Verify Exit button is present and enabled
            var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
            Assert.NotNull(exitBtn);
            Assert.True(exitBtn!.IsEnabled);

            // On CI (no webcam), camera check finishes with a problem and Scan button switches to "Try again"
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

            // Click the "Calibrate" button on MainWindow
            var calibrateBtn = mainWindow.FindFirstDescendant(cf => cf.ByName("Calibrate"))?.AsButton();
            Assert.NotNull(calibrateBtn);
            calibrateBtn!.Invoke();

            // CalibrationWindow should open again
            var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 10);
            Assert.NotNull(calWindow);

            // Close calibration overlay
            var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
            Assert.NotNull(exitBtn);
            exitBtn!.Invoke();
        }

        [Fact]
        public void SessionTimer_StartWithoutCalibration_PromptsCalibration_And_BlocksTimer()
        {
            var mainWindow = SkipFirstRunCalibrationAndGetMainWindow();

            // Click "Start" on MainWindow without having calibrated
            var startBtn = mainWindow.FindFirstDescendant(cf => cf.ByName("Start"))?.AsButton();
            Assert.NotNull(startBtn);
            startBtn!.Invoke();

            // StartSessionAsync calls App.EnsureCalibrated("Calibrate your face size first...")
            var calWindow = WaitForWindowByTitle("Waddle - Calibrate", 10);
            Assert.NotNull(calWindow);

            // Exit calibration without scanning -> timer should NOT start
            var exitBtn = calWindow.FindFirstDescendant(cf => cf.ByName("Exit"))?.AsButton();
            Assert.NotNull(exitBtn);
            exitBtn!.Invoke();

            // Verify "Start" button is still present (session did not start)
            var startBtnAfter = mainWindow.FindFirstDescendant(cf => cf.ByName("Start"))?.AsButton();
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
