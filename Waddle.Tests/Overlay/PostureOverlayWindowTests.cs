using System.Windows.Input;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class PostureOverlayWindowTests : IDisposable
{
    private readonly WpfApplicationFixture _wpf;

    public PostureOverlayWindowTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(WpfState.Reset);
    }

    public void Dispose() => _wpf.Invoke(WpfState.Reset);

    [Fact]
    public void IsHotkeyExit_ReturnsTrueOnlyForCtrlShiftE()
    {
        Assert.True(PostureOverlayWindow.IsHotkeyExit(Key.E, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(PostureOverlayWindow.IsHotkeyExit(Key.E, ModifierKeys.Control));
        Assert.False(PostureOverlayWindow.IsHotkeyExit(Key.E, ModifierKeys.None));
        Assert.False(PostureOverlayWindow.IsHotkeyExit(Key.Escape, ModifierKeys.Control | ModifierKeys.Shift));
    }

    [Fact]
    public void Window_InitializesOnWpfThreadWithoutError()
    {
        _wpf.Invoke(() =>
        {
            var window = new PostureOverlayWindow();
            window.Show();
            window.Close();
        });
    }
}
