using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Waddle.Wpf;

public enum WidgetMode { Session, Calibrate, Reminder }

public partial class WidgetWindow : Window
{
    // remember where/how big the widget was last time
    private static double? _left, _top, _width, _height;
    private static bool? _lastBig;

    private const double MediumW = 300, MediumH = 370, SmallW = 260, SmallH = 180;

    private bool _big;                      // true = camera + timer, false = timer only
    private WidgetMode _mode = WidgetMode.Session;
    private readonly DuckAnimator _duck = new();   // swaps the duck frames when collapsed
    private bool? _bigBeforeReminder;               // size to go back to after the idle reminder

    /// <summary>The "Start session" button in reminder mode was clicked.</summary>
    public event Action? StartRequested;

    public WidgetWindow()
    {
        InitializeComponent();
        DuckPanel.DataContext = _duck;

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
        var previous = _mode;
        _mode = mode;
        bool session = mode == WidgetMode.Session;
        bool calibrate = mode == WidgetMode.Calibrate;
        bool reminder = mode == WidgetMode.Reminder;

        SessionPanel.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        ReminderText.Visibility = reminder ? Visibility.Visible : Visibility.Collapsed;
        CalText.Visibility = calibrate ? Visibility.Visible : Visibility.Collapsed;
        Controls.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Visibility = reminder ? Visibility.Visible : Visibility.Collapsed;

        if (reminder)
        {
            _bigBeforeReminder ??= _big;                    // remember the size so the next session looks like before
            if (_big) SetBig(false);                        // the reminder lives in the small form with the duck
            _duck.Force(DuckEmotion.Sad);
        }
        else if (previous == WidgetMode.Reminder)
        {
            _duck.Force(null);                              // duck follows the timer again
            if (_bigBeforeReminder is bool wasBig)
            {
                _bigBeforeReminder = null;
                if (wasBig != _big) SetBig(wasBig);
            }
        }

        if (session)
        {
            CameraMonitor.Instance.Release("calibration");  // the session keeps the camera on if it needs it
        }
        else if (calibrate)
        {
            if (!_big) SetBig(true);                        // calibrating needs the camera visible
            _ = CameraMonitor.Instance.AcquireAsync("calibration");
        }
    }

    // ---------------- duck ----------------
    /// <summary>Call with true while the person is slouching (duck turns angry), false when they sit up again.</summary>
    public void SetSlouching(bool slouching) => _duck.Force(slouching ? DuckEmotion.Angry : null);

    // ---------------- layout ----------------
    private void ApplyLayout()
    {
        CameraRow.Height = _big ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TimerRow.Height = _big ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        SizeButton.Content = _big ? "\uE73F" : "\uE740";
        SizeButton.ToolTip = _big ? "Make smaller" : "Make bigger";

        DuckPanel.Visibility = _big ? Visibility.Collapsed : Visibility.Visible;   // duck only in the collapsed form
        if (_big) _duck.Stop(); else _duck.Start();
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
        if (_mode != WidgetMode.Session) return;            // calibrate needs the camera, reminder needs the duck
        SetBig(!_big);
    }

    // ---------------- buttons ----------------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();                 // timer keeps running
    private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();
    private void Start_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke();

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
        _duck.Stop();
        CameraMonitor.Instance.Release("calibration");      // no-op if a session still needs the camera
        _left = Left; _top = Top;
        if (_bigBeforeReminder is null) { _width = ActualWidth; _height = ActualHeight; }   // don't save the reminder's small size
        _lastBig = _bigBeforeReminder ?? _big;
    }
}