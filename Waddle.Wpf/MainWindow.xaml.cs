using System.Threading.Tasks;
using System.Windows;

namespace Waddle.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private WidgetWindow? _widget;
    private bool _starting;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // closing the dashboard ends the app: save the running session, free the camera, exit
        Closed += (_, _) =>
        {
            TimeTracker.Instance.Stop();
            CameraMonitor.Instance.Shutdown();
            Application.Current.Shutdown();
        };
    }

    // The view model commands set Status; these Click handlers do the window work.
    private async void Start_Click(object sender, RoutedEventArgs e) => await StartSessionAsync();

    private void Recalibrate_Click(object sender, RoutedEventArgs e) => OpenWidget(WidgetMode.Calibrate);

    private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();
    private void Stop_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.ConfirmAndStop();

    // "Try again" on the camera reminder
    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (TimeTracker.Instance.IsRunning || CameraMonitor.Instance.HasHolders)
            await CameraMonitor.Instance.RetryAsync();      // if the timer was auto-paused it resumes by itself
        else
            await StartSessionAsync();                      // the first start failed, so start again
    }

    // The camera is checked BEFORE the timer starts: no camera, no timer.
    private async Task StartSessionAsync()
    {
        if (TimeTracker.Instance.IsRunning) { OpenWidget(WidgetMode.Session); return; }
        if (_starting) return;
        _starting = true;
        try
        {
            if (!await CameraMonitor.Instance.AcquireAsync("session")) return;   // reminder banner explains why
            TimeTracker.Instance.Start();
            OpenWidget(WidgetMode.Session);
        }
        finally { _starting = false; }
    }

    private void OpenWidget(WidgetMode mode)
    {
        if (_widget == null)
        {
            _widget = new WidgetWindow();           // deliberately NOT owned by this window
            _widget.Closed += (_, _) =>
            {
                _widget = null;
                TimeTracker.Instance.IsWidgetOpen = false;   // dashboard Pause/Stop + camera appear
                TimeTracker.Instance.IsWidgetCollapsed = false;
            };
            TimeTracker.Instance.IsWidgetCollapsed = false;
            TimeTracker.Instance.IsWidgetOpen = true;        // dashboard Pause/Stop + camera hide
            _widget.Show();                         // ShowActivated=false: dashboard stays where it is
        }
        _widget.SetMode(mode);
    }
}