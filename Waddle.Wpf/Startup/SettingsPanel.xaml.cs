using System.Windows;
using System.Windows.Controls;

namespace Waddle.Wpf;

/// <summary>
/// The settings card shown in the dashboard's in-window overlay (MainWindow's SettingsOverlay).
/// It's a panel, not a window: a modal ShowDialog() window locks every other window in the app,
/// which left the floating widget's close button unclickable.
/// </summary>
public partial class SettingsPanel : UserControl
{
  private bool _ignoreChanges;     // true while we set a box ourselves, so that doesn't count as the user changing it

  /// <summary>Raised when the user presses Done. The host decides how to hide the panel.</summary>
  public event Action? CloseRequested;

  public SettingsPanel()
  {
    InitializeComponent();
    Refresh();
  }

  /// <summary>Re-reads both settings. The host calls this each time the overlay opens.</summary>
  public void Refresh()
  {
    _ignoreChanges = true;
    StartupCheck.IsChecked = StartupManager.IsEnabled();
    AutoTimerCheck.IsChecked = StartupManager.IsAutoStartTimerEnabled();
    _ignoreChanges = false;
    ErrorText.Visibility = Visibility.Collapsed;
  }

  /// <summary>Puts keyboard focus on the first checkbox (so Tab / Space work straight away).</summary>
  public void FocusFirst() => StartupCheck.Focus();

  // Fires for a mouse click, the keyboard, and a screen reader's Toggle: anything that changes the box.
  private void StartupCheck_Changed(object sender, RoutedEventArgs e) =>
      Apply(StartupCheck, StartupManager.Set);

  private void AutoTimerCheck_Changed(object sender, RoutedEventArgs e) =>
      Apply(AutoTimerCheck, StartupManager.SetAutoStartTimer);

  private void Apply(CheckBox box, Func<bool, bool> save)
  {
    if (_ignoreChanges) return;

    var want = box.IsChecked == true;
    if (save(want))
    {
      ErrorText.Visibility = Visibility.Collapsed;
      return;
    }

    // The change was refused (e.g. Windows policy blocks the Run key): put the box back and say so
    _ignoreChanges = true;
    box.IsChecked = !want;
    _ignoreChanges = false;
    ErrorText.Text = "Couldn't change this setting. Windows may be blocking it on this PC.";
    ErrorText.Visibility = Visibility.Visible;
  }

  private void Done_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
}
