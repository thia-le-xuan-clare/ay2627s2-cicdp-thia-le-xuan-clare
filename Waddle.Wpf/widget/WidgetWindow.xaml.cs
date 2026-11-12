using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Waddle.Wpf;

public enum WidgetMode { Session, Calibrate }

public partial class WidgetWindow : Window
{
    // remember where/how big the widget was last time
    private static double? _left, _top, _width, _height;
    private static bool? _lastBig;

    private const double MediumW = 300, MediumH = 370, SmallW = 260, SmallH = 180;

    private bool _big;                      // true = camera + timer, false = timer only
    private WidgetMode _mode = WidgetMode.Session;

    public WidgetWindow()
    {
        InitializeComponent();

        _big = _lastBig ?? true;
        ApplyLayout();
        if (_width is double w && _height is double h) { Width = w; Height = h; }
        else { Width = _big ? MediumW : SmallW; Height = _big ? MediumH : SmallH; }

        if (_left is double l && _top is double t && IsOnAScreen(l, t))
        {
            Left = l; Top = t;
        }
        else
        {
            var area = SystemParameters.WorkArea;           // first time: bottom-right of main screen
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;
        }
    }

    // Topmost keeps it over other windows (drag it to any monitor).
    // NOACTIVATE: clicking it never activates it, so the dashboard doesn't pop up.
    // TOOLWINDOW: hidden from Alt+Tab.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static bool IsOnAScreen(double l, double t) =>   // guards against an unplugged monitor
        l + 60 > SystemParameters.VirtualScreenLeft && l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60 &&
        t + 30 > SystemParameters.VirtualScreenTop && t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 30;

    // ---------------- mode ----------------
    public void SetMode(WidgetMode mode)
    {
        _mode = mode;
        bool session = mode == WidgetMode.Session;
        SessionPanel.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        CalText.Visibility = session ? Visibility.Collapsed : Visibility.Visible;
        Controls.Visibility = session ? Visibility.Visible : Visibility.Collapsed;

        if (session)
        {
            CameraMonitor.Instance.Release("calibration");  // the session keeps the camera on if it needs it
        }
        else
        {
            if (!_big) SetBig(true);                        // calibrating needs the camera visible
            _ = CameraMonitor.Instance.AcquireAsync("calibration");
        }
    }

    // ---------------- layout ----------------
    private void ApplyLayout()
    {
        CameraRow.Height = _big ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TimerRow.Height = _big ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        SizeButton.Content = _big ? "\uE73F" : "\uE740";
        SizeButton.ToolTip = _big ? "Make smaller" : "Make bigger";
    }

    private void SetBig(bool big)
    {
        _big = big;
        ApplyLayout();
        Width = big ? MediumW : SmallW;
        Height = big ? MediumH : SmallH;
    }

    private void Size_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == WidgetMode.Calibrate) return;
        SetBig(!_big);
    }

    // ---------------- buttons ----------------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();                 // timer keeps running
    private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();

    private bool _confirmingStop;

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn)
        {
            TimeTracker.Instance.Stop();
            Close();
            return;
        }

        // Second click within 3 seconds -> stop timer and close widget
        if (_confirmingStop)
        {
            _confirmingStop = false;
            ResetStopButton(btn);
            TimeTracker.Instance.Stop();
            Close();
            return;
        }

        // First click -> switch to coral-red "Confirm?" state
        _confirmingStop = true;
        btn.Content = "Confirm stop?";
        btn.Background = (System.Windows.Media.Brush)FindResource("Bad");
        btn.BorderBrush = (System.Windows.Media.Brush)FindResource("Bad");
        btn.Foreground = (System.Windows.Media.Brush)FindResource("Surface");

        // Auto-reset after 3 seconds if not clicked
        await Task.Delay(3000);
        if (_confirmingStop)
        {
            _confirmingStop = false;
            ResetStopButton(btn);
        }
    }

    private static void ResetStopButton(System.Windows.Controls.Button btn)
    {
        btn.Content = "Stop";
        btn.ClearValue(BackgroundProperty);
        btn.ClearValue(BorderBrushProperty);
        btn.ClearValue(ForegroundProperty);
    }


    // "Try again" on the camera reminder
    private void Retry_Click(object sender, RoutedEventArgs e) =>
        _ = _mode == WidgetMode.Calibrate
            ? CameraMonitor.Instance.AcquireAsync("calibration")
            : CameraMonitor.Instance.RetryAsync();

    private void Window_Closed(object? sender, EventArgs e)
    {
        CameraMonitor.Instance.Release("calibration");      // no-op if a session still needs the camera
        _left = Left; _top = Top; _width = ActualWidth; _height = ActualHeight; _lastBig = _big;
    }
}