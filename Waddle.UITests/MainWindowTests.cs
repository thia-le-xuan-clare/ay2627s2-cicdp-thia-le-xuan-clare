using System;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;

namespace Waddle.UITests
{
    public class MainWindowTests : IDisposable
    {
        private readonly Application _app;
        private readonly UIA3Automation _automation;

        public MainWindowTests()
        {
            // Locate the compiled Waddle.Wpf.exe inside the Release build folder
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

        [Fact]
        public void App_Launches_And_ShowsMainWindow()
        {
            // Wait up to 5 seconds for the main window to open
            var mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(5));

            Assert.NotNull(mainWindow);
            Assert.False(string.IsNullOrWhiteSpace(mainWindow.Title));
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

