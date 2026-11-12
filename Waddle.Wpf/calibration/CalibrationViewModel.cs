using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Waddle.Core;

namespace Waddle.Wpf;

public enum CalibrationStep { AlreadyCalibrated, Live, Counting, Done }

/// <summary>
/// Drives the calibration overlay. The camera itself belongs to CameraMonitor; this only borrows it
/// as the "calibration" holder, so it never switches off a running session's camera.
/// </summary>
public partial class CalibrationViewModel : ObservableObject, IDisposable
{
    private const string Holder = "calibration";
    private const int CountdownSeconds = 3;

    private readonly ILogService _log;
    private readonly ICalibrationStore _store;
    private bool _closed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlreadyCalibrated), nameof(IsLive), nameof(IsCounting))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private CalibrationStep step;

    [ObservableProperty] private int countdown;

    [ObservableProperty] private string statusTitle = "Starting camera...";
    [ObservableProperty] private string statusDetail = "";


    [ObservableProperty] private string lastCalibratedText = "";

    public CalibrationViewModel(ILogService log, ICalibrationStore store)
    {
        _log = log;
        _store = store;

        var saved = _store.Load();
        IsFirstRun = saved is null;
        if (saved is null) Step = CalibrationStep.Live;
        else
        {
            Step = CalibrationStep.AlreadyCalibrated;
            LastCalibratedText = $"Last calibrated {saved.CalibratedAt:d MMM yyyy, h:mm tt}.";
        }
        Monitor.PropertyChanged += OnMonitorChanged;
    }

    /// <summary>The UI binds straight to the monitor for the live picture and overlay text.</summary>
    public CameraMonitor Monitor => CameraMonitor.Instance;

    public bool IsAlreadyCalibrated => Step == CalibrationStep.AlreadyCalibrated;
    public bool IsLive => Step != CalibrationStep.AlreadyCalibrated;
    public bool IsCounting => Step == CalibrationStep.Counting;
    public string ScanText => Monitor.IsOn ? "Scan" : "Try again";

    public bool IsFirstRun { get; }
    public string TitleText => IsFirstRun ? "Welcome to Waddle!" : "Please sit up straight";

    /// <summary>The line under the title on first run. The caller can change it before the window opens.</summary>
    public string SubtitleText { get; set; } = "Let's calibrate your face size. Please sit up straight.";

    /// <summary>The window listens for this and closes itself.</summary>
    public event Action? CloseRequested;

    /// <summary>Called when the window opens. Skips the camera if the "already calibrated" screen is showing.</summary>
    public async Task StartAsync()
    {
        if (Step == CalibrationStep.Live) await StartCameraAsync();
    }

    private async Task StartCameraAsync()
    {
        StatusTitle = "Starting camera...";
        StatusDetail = "";
        _log.Security("Camera access requested for calibration");
        await Monitor.AcquireAsync(Holder);
        RefreshStatus();
        ScanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        Step = CalibrationStep.Live;
        await StartCameraAsync();
    }

    [RelayCommand]
    private void Exit() => CloseRequested?.Invoke();

    // Scan is only allowed with exactly ONE face in view. If the camera isn't working the same button becomes "Try again".
    private bool CanScan() =>
        Step == CalibrationStep.Live
        && !Monitor.IsChecking
        && (Monitor.IsOn ? Monitor.FaceCount == 1 : Monitor.HasProblem);

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (!Monitor.IsOn) { await StartCameraAsync(); return; }

        Step = CalibrationStep.Counting;
        StatusTitle = "Hold still...";
        StatusDetail = "";
        var samples = new List<double>();

        for (int i = CountdownSeconds; i > 0; i--)
        {
            Countdown = i;
            await Task.Delay(1000);
            if (_closed) return;

            // someone walked in, or you leaned out of frame: stop and let the status line say why
            if (Monitor.FaceCount != 1)
            {
                Step = CalibrationStep.Live;
                RefreshStatus();
                return;
            }
            samples.Add(Monitor.FaceRatio);
        }

        var ratio = samples.Average();      // average of the countdown frames so one odd frame doesn't skew it
        _store.Save(new CalibrationData(ratio, DateTime.Now));
        _log.Info($"Calibration saved: face fills {ratio:P0} of frame");

        Step = CalibrationStep.Done;
        StatusTitle = "Calibrated!";
        StatusDetail = "";
        await Task.Delay(900);
        if (!_closed) CloseRequested?.Invoke();
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshStatus();
        OnPropertyChanged(nameof(ScanText));
        ScanCommand.NotifyCanExecuteChanged();
    }

    private void RefreshStatus()
    {
        if (Step != CalibrationStep.Live) return;

        if (Monitor.IsChecking)
        {
            StatusTitle = "Checking your camera...";
            StatusDetail = "";
        }
        else if (Monitor.HasProblem)
        {
            StatusTitle = Monitor.Problem!;
            StatusDetail = "";
        }
        else if (!Monitor.IsOn)
        {
            StatusTitle = "Starting camera...";
            StatusDetail = "";
        }
        else
        {
            (StatusTitle, StatusDetail) = Monitor.FaceCount switch
            {
                0 => ("Face is not detected", ""),
                1 => ("Face detected. ", "Please sit up straight then press Scan."),
                _ => ("More than one face detected", "")
            };
        }
    }


    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        Monitor.PropertyChanged -= OnMonitorChanged;
        Monitor.Release(Holder);
    }
}
