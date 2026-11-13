using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Waddle.Wpf;

/// <summary>
/// A small message at the top-centre of the screen. It is always on top but never takes focus,
/// so it can't interrupt typing or a meeting. BannerService decides what it says and when.
/// </summary>
public partial class BannerWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;   // clicking it never activates the window
    private const int WsExToolWindow = 0x00000080;   // keeps it out of Alt+Tab

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>The action button (Try again / Resume) was clicked.</summary>
    public event Action? ActionClicked;
    /// <summary>The small X was clicked.</summary>
    public event Action? DismissClicked;

    private readonly DuckAnimator _duck = new();
    private DuckEmotion? _duckEmotion;

    public BannerWindow()
    {
        InitializeComponent();
        DuckPanel.DataContext = _duck;
        IsVisibleChanged += (_, _) => UpdateDuckRunning();      // only animate while the banner is on screen
    }

    private string? _buttonText;

    public void SetContent(string message, string? buttonText, DuckEmotion? duck = null)
    {
        SetDuck(duck);
        _buttonText = buttonText;
        MessageText.Text = message;
        ActionButton.Content = buttonText;
        ActionButton.IsEnabled = true;
        ActionButton.Visibility = string.IsNullOrEmpty(buttonText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetDuck(DuckEmotion? duck)
    {
        if (duck == _duckEmotion) return;                       // already showing this one: don't restart the animation
        _duckEmotion = duck;
        DuckPanel.Visibility = duck is null ? Visibility.Collapsed : Visibility.Visible;
        _duck.Force(duck);
        UpdateDuckRunning();
    }

    private void UpdateDuckRunning()
    {
        if (IsVisible && _duckEmotion is not null) _duck.Start(); else _duck.Stop();
    }

    /// <summary>While the action is running the button says "Trying..." and can't be pressed again.</summary>
    public void SetBusy(bool busy)
    {
        ActionButton.IsEnabled = !busy;
        ActionButton.Content = busy ? "Trying..." : _buttonText;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
    }

    // the window sizes itself to its text, so re-centre whenever the size changes
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + 8;
    }

    private void Action_Click(object sender, RoutedEventArgs e) => ActionClicked?.Invoke();
    private void Close_Click(object sender, RoutedEventArgs e) => DismissClicked?.Invoke();
}