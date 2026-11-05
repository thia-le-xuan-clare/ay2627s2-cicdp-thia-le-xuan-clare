using System.Windows;

namespace Waddle.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private WidgetWindow? _widget;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // closing the dashboard ends the app: save the running session, then exit
        Closed += (_, _) =>
        {
            TimeTracker.Instance.Stop();
            Application.Current.Shutdown();
        };
    }

    // The view model commands set Status; these Click handlers do the window work.
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        TimeTracker.Instance.Start();               // no-op if already running
        OpenWidget(WidgetMode.Session);
    }

    private void Recalibrate_Click(object sender, RoutedEventArgs e) => OpenWidget(WidgetMode.Calibrate);

    private void Pause_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.TogglePause();
    private void Stop_Click(object sender, RoutedEventArgs e) => TimeTracker.Instance.Stop();

    private void OpenWidget(WidgetMode mode)
    {
        if (_widget == null)
        {
            _widget = new WidgetWindow();           // deliberately NOT owned by this window
            _widget.Closed += (_, _) =>
            {
                _widget = null;
                TimeTracker.Instance.IsWidgetOpen = false;   // dashboard Pause/Stop appear
            };
            TimeTracker.Instance.IsWidgetOpen = true;        // dashboard Pause/Stop hide
            _widget.Show();                         // ShowActivated=false: dashboard stays where it is
        }
        _widget.SetMode(mode);
    }
}