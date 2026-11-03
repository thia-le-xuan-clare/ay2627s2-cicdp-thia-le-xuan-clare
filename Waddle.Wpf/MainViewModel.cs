using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Waddle.Core;

namespace Waddle.Wpf;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogService _log;

    [ObservableProperty] private string status = "Idle";
    [ObservableProperty] private bool simulateFailure;

    public MainViewModel(ILogService log) => _log = log;

    [RelayCommand]
    private void Scan()
    {
        _log.Info("Scan started");
        try
        {
            if (SimulateFailure)
                throw new InvalidOperationException("Simulated camera failure");

            _log.Security("Camera access requested by user (Scan button)");
            Status = $"Scan done at {DateTime.Now:T}";
            _log.Info("Scan completed");
        }
        catch (Exception ex)
        {
            _log.Info($"Scan failed: {ex.Message}");
            _log.Security($"Camera scan error: {ex.GetType().Name}");
            Status = "Scan failed";
        }
    }
}