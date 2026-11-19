using System.Windows;
using System.Windows.Input;

namespace Waddle.Wpf;

/// <summary>
/// Full-screen frosted overlay with the settings card in the middle, the same look as the calibration overlay.
/// It covers the work area (not the taskbar), so the floating widget and everything else sits under it.
/// Open it with Show(), not ShowDialog(): a modal dialog locks every other window in the app.
/// </summary>
public partial class SettingsWindow : Window
{
  public SettingsWindow()
  {
    InitializeComponent();

    var area = SystemParameters.WorkArea;
    Left = area.Left;
    Top = area.Top;
    Width = area.Width;
    Height = area.Height;

    Loaded += (_, _) => { OverlayState.Enter(); SettingsCard.FocusFirst(); };
    Closed += (_, _) => OverlayState.Exit();
  }

  private void SettingsCard_CloseRequested() => Close();

  private void Window_KeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key == Key.Escape) Close();
  }
}
