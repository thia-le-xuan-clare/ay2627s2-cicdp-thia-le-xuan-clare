using Waddle.Wpf;

namespace Waddle.Tests;

public sealed class MainViewModelTests
{
  private readonly RecordingLog _log = new();

  [Fact]
  public void NewViewModel_StartsIdle()
  {
    var vm = new MainViewModel(_log);

    Assert.Equal("Idle", vm.Status);
    Assert.False(vm.SimulateFailure);
  }

  [Fact]
  public void Scan_Succeeds_ReportsTheTimeAndLogsTheCameraRequest()
  {
    var vm = new MainViewModel(_log);

    vm.ScanCommand.Execute(null);

    Assert.StartsWith("Scan done at", vm.Status, StringComparison.Ordinal);
    Assert.Contains("Camera access requested by user (Scan button)", _log.SecurityLines);
    Assert.Contains("Scan started", _log.InfoLines);
    Assert.Contains("Scan completed", _log.InfoLines);
  }

  [Fact]
  public void Scan_WhenTheCameraFails_ReportsTheFailureAndLogsIt()
  {
    var vm = new MainViewModel(_log) { SimulateFailure = true };

    vm.ScanCommand.Execute(null);

    Assert.Equal("Scan failed", vm.Status);
    Assert.Contains("Scan failed: Simulated camera failure", _log.InfoLines);
    Assert.Contains("Camera scan error: InvalidOperationException", _log.SecurityLines);
    Assert.DoesNotContain("Scan completed", _log.InfoLines);
  }
}
