using System.Windows;
using System.Windows.Input;

namespace Waddle.Wpf;

/// <summary>
/// First-run overlay asking how Waddle should start: when the user signs in to Windows, and whether the
/// posture timer should begin by itself on launch. Both boxes start unticked; Continue saves what is ticked.
/// </summary>
public partial class StartupPromptWindow : Window
{
  public StartupPromptWindow()
  {
    InitializeComponent();

    var area = SystemParameters.WorkArea;
    Left = area.Left;
    Top = area.Top;
    Width = area.Width;
    Height = area.Height;

    Loaded += (_, _) => { OverlayState.Enter(); StartupCheck.Focus(); };
    Closed += (_, _) =>
    {
      OverlayState.Exit();
      StartupManager.MarkAsked();     // however it closed, don't ask again
    };
  }

  private void Continue_Click(object sender, RoutedEventArgs e)
  {
    // Only turn things ON here: a refused write (e.g. Windows policy) must never stop the first-run flow
    if (StartupCheck.IsChecked == true) StartupManager.Set(true);
    if (AutoTimerCheck.IsChecked == true) StartupManager.SetAutoStartTimer(true);
    Close();
  }

  private void Window_KeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key == Key.Escape) Close();       // skips the question: nothing is saved, even if a box is ticked
  }
}
