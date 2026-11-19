using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace Waddle.UITests
{
    // Same test class as MainWindowTests.cs (shares the launch/cleanup helpers).
    public partial class WaddleAppTests
    {
        [Fact]
        public void Calendar_StartsWithTodayAlreadySelected()
        {
            SeedCalibration(0.25);
            LaunchApp();
            var mainWindow = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(10))!;

            // Only the selected square shows its time, and a day with nothing saved shows "0m".
            // Nothing is clicked here, so seeing it means the app selected today on its own.
            var todaysTime = Retry.WhileNull(
                () => mainWindow.FindFirstDescendant(cf => cf.ByName("0m")),
                timeout: TimeSpan.FromSeconds(5));

            var seen = string.Join(" | ", mainWindow
                .FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n)));

            Assert.True(todaysTime.Result != null,
                "Today's square did not show its time (\"0m\"). If this exe is older than the SelectToday change, " +
                "run: dotnet build Waddle.Wpf/Waddle.Wpf.csproj -c Release.  Text the test could see: " + seen);
        }
    }
}