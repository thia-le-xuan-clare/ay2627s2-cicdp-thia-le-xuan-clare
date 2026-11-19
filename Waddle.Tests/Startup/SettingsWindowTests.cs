using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Waddle.Wpf;

namespace Waddle.Tests;

// The window is only the full-screen frosted overlay around the settings card; what the card's checkboxes do is
// covered in SettingsPanelTests.
[Collection(WpfCollection.Name)]
public sealed class SettingsWindowTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly StartupTestScope _scope = new();

  public SettingsWindowTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
    DrainOverlays();
  }

  public void Dispose()
  {
    DrainOverlays();
    _wpf.Invoke(WpfState.Reset);
    _scope.Dispose();
  }

  private static void DrainOverlays()
  {
    while (OverlayState.IsActive) OverlayState.Exit();
  }

  private static void PressEscape(SettingsWindow w)
  {
    using var source = new HwndSource(new HwndSourceParameters("waddle-test"));
    w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
  }

  /// <summary>Runs the body on the WPF thread and tells it whether the window closed.</summary>
  private void WithWindow(Action<SettingsWindow, Func<bool>> test) => _wpf.Invoke(() =>
  {
    var window = new SettingsWindow();
    var closed = false;
    window.Closed += (_, _) => closed = true;
    try { test(window, () => closed); }
    finally { if (!closed) window.Close(); }
  });

  private static SettingsPanel Card(SettingsWindow w) => (SettingsPanel)w.FindName("SettingsCard");

  [Fact]
  public void Window_CoversTheWholeWorkArea_LikeTheOtherOverlays()
  {
    WithWindow((w, _) =>
    {
      var area = SystemParameters.WorkArea;

      Assert.Equal(area.Left, w.Left);
      Assert.Equal(area.Top, w.Top);
      Assert.Equal(area.Width, w.Width);
      Assert.Equal(area.Height, w.Height);
    });
  }

  [Fact]
  public void Window_HasTheSameFrostedBackdropAsTheOtherOverlays()
  {
    WithWindow((w, _) =>
    {
      Assert.True(w.Topmost);
      Assert.True(w.AllowsTransparency);
      Assert.Equal(WindowStyle.None, w.WindowStyle);
      Assert.False(w.ShowInTaskbar);
      Assert.Equal(System.Windows.Media.Color.FromArgb(0xCC, 0x3B, 0x2F, 0x14),
              ((System.Windows.Media.SolidColorBrush)w.Background).Color);
    });
  }

  [Fact]
  public void Card_ShowsBothStartupSettings()
  {
    StartupManager.Set(true);
    StartupManager.SetAutoStartTimer(true);

    WithWindow((w, _) =>
    {
      Assert.True(((CheckBox)Card(w).FindName("StartupCheck")).IsChecked);
      Assert.True(((CheckBox)Card(w).FindName("AutoTimerCheck")).IsChecked);
    });
  }

  [Fact]
  public void DoneButton_ClosesTheOverlay()
  {
    WithWindow((w, closed) =>
    {
      ((Button)Card(w).FindName("DoneButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

      Assert.True(closed());
    });
  }

  [Fact]
  public void Escape_ClosesTheOverlay_AndKeepsWhatWasTicked()
  {
    WithWindow((w, closed) =>
    {
      ((CheckBox)Card(w).FindName("AutoTimerCheck")).IsChecked = true;

      PressEscape(w);

      Assert.True(closed());
      Assert.True(StartupManager.IsAutoStartTimerEnabled());      // settings apply immediately, Esc just leaves
    });
  }

  [Fact]
  public async Task WhileOpen_CountsAsAnOverlay_SoTheIdleReminderStaysAway()
  {
    SettingsWindow? window = null;
    _wpf.Invoke(() =>
    {
      window = new SettingsWindow();
      window.Show();
    });

    Assert.True(await _wpf.WaitUntilAsync(() => OverlayState.IsActive), "overlay never became active");

    _wpf.Invoke(() => window!.Close());

    Assert.False(_wpf.Query(() => OverlayState.IsActive));
  }
}
