using System.Windows;

namespace Waddle.Wpf;

/// <summary>Small settings dialog opened from the dashboard.</summary>
public partial class SettingsWindow : Window
{
    private bool _ignoreChanges;     // true while we set the box ourselves, so that doesn't count as the user changing it

    public SettingsWindow()
    {
        InitializeComponent();
        SetBox(StartupManager.IsEnabled());
    }

    private void SetBox(bool isChecked)
    {
        _ignoreChanges = true;
        StartupCheck.IsChecked = isChecked;
        _ignoreChanges = false;
    }

    // Fires for a mouse click, the keyboard, and a screen reader's Toggle: anything that changes the box.
    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_ignoreChanges) return;

        var want = StartupCheck.IsChecked == true;
        if (StartupManager.Set(want))
        {
            ErrorText.Visibility = Visibility.Collapsed;
            return;
        }

        // Windows refused (e.g. blocked by policy): put the box back and say so
        SetBox(!want);
        ErrorText.Text = "Couldn't change this setting. Windows may be blocking it on this PC.";
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}