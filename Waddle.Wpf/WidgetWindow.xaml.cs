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

    private bool _big;

    public WidgetWindow()
    {
        InitializeComponent();
        if (_width is double w && _height is double h) { Width = w; Height = h; }

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

    public void SetMode(WidgetMode mode)
    {
        bool session = mode == WidgetMode.Session;
        SessionPanel.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        CalText.Visibility = session ? Visibility.Collapsed : Visibility.Visible;
        Controls.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
    }

    // small <-> big presets (you can also drag the corner grip to any size)
    private void Size_Click(object sender, RoutedEventArgs e)
    {
        _big = !_big;
        Width = _big ? 380 : 260;
        Height = _big ? 240 : 170;
        SizeButton.Content = _big ? "\uE73F" : "\uE740";
        SizeButton.ToolTip = _big ? "Make smaller" : "Make bigger";
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();                 // timer keeps running
    private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        TimeTracker.Instance.Stop();
        Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _left = Left; _top = Top; _width = ActualWidth; _height = ActualHeight;
    }
}