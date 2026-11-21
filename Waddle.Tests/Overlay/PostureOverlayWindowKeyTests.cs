using System.Windows.Input;
using System.Windows.Interop;
using Waddle.Wpf;

namespace Waddle.Tests;

// The Ctrl+Shift+E combination itself is covered in PostureOverlayWindowTests (IsHotkeyExit). Here: a key press
// that is not the hotkey must not make the window ask to exit.
[Collection(WpfCollection.Name)]
public sealed class PostureOverlayWindowKeyTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;

  public PostureOverlayWindowKeyTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
  }

  public void Dispose() => _wpf.Invoke(WpfState.Reset);

  [Theory]
  [InlineData(Key.E)]
  [InlineData(Key.Escape)]
  [InlineData(Key.A)]
  public void OrdinaryKeys_DoNotRequestTheHotkeyExit(Key key)
  {
    _wpf.Invoke(() =>
    {
      var window = new PostureOverlayWindow();
      var requested = 0;
      window.HotkeyExitRequested += () => requested++;
      try
      {
        window.Show();
        using var source = new HwndSource(new HwndSourceParameters("waddle-test"));
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

        Assert.Equal(0, requested);
      }
      finally { window.Close(); }
    });
  }
}
