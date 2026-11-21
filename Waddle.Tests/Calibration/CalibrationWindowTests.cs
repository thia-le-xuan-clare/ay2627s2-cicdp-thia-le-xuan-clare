using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

// The window is the frosted full-screen overlay around the calibration card; what the card does is covered in
// CalibrationViewModelTests. These tests open it on the "already calibrated" screen so the camera stays off.
[Collection(WpfCollection.Name)]
public sealed class CalibrationWindowTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly FakeCalibrationStore _store = new() { Saved = new CalibrationData(0.25, new DateTime(2026, 11, 13, 9, 0, 0)) };
  private readonly RecordingLog _log = new();

  public CalibrationWindowTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
    DrainOverlays();
  }

  public void Dispose()
  {
    DrainOverlays();
    _wpf.Invoke(WpfState.Reset);
  }

  private static void DrainOverlays()
  {
    while (OverlayState.IsActive) OverlayState.Exit();
  }

  private static void PressEscape(CalibrationWindow w)
  {
    using var source = new HwndSource(new HwndSourceParameters("waddle-test"));
    w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
  }

  /// <summary>Runs the body on the WPF thread with a window that has been created but not shown.</summary>
  private void WithWindow(Action<CalibrationWindow, CalibrationViewModel, Func<bool>> test) => _wpf.Invoke(() =>
  {
    var vm = new CalibrationViewModel(_log, _store);
    var window = new CalibrationWindow(vm);
    var closed = false;
    window.Closed += (_, _) => closed = true;
    try { test(window, vm, () => closed); }
    finally
    {
      if (!closed) window.Close();
      vm.Dispose();
    }
  });

  [Fact]
  public void Window_CoversTheWholeWorkArea_LikeTheOtherOverlays()
  {
    WithWindow((w, _, _) =>
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
    WithWindow((w, _, _) =>
    {
      Assert.True(w.Topmost);
      Assert.True(w.AllowsTransparency);
      Assert.Equal(WindowStyle.None, w.WindowStyle);
      Assert.False(w.ShowInTaskbar);
    });
  }

  [Fact]
  public void Window_UsesTheViewModelAsItsDataContext()
  {
    WithWindow((w, vm, _) => Assert.Same(vm, w.DataContext));
  }

  [Fact]
  public void Escape_ClosesTheOverlay()
  {
    WithWindow((w, _, closed) =>
    {
      PressEscape(w);

      Assert.True(closed());
    });
  }

  [Fact]
  public void Exit_FromTheViewModel_ClosesTheOverlay()
  {
    WithWindow((_, vm, closed) =>
    {
      vm.ExitCommand.Execute(null);

      Assert.True(closed());
    });
  }

  [Fact]
  public async Task WhileOpen_CountsAsAnOverlay_AndGivesTheCameraBackWhenClosed()
  {
    CalibrationWindow? window = null;
    _wpf.Invoke(() =>
    {
      window = new CalibrationWindow(new CalibrationViewModel(_log, _store));
      window.Show();
    });

    Assert.True(await _wpf.WaitUntilAsync(() => OverlayState.IsActive), "overlay never became active");

    _wpf.Invoke(() => window!.Close());

    Assert.False(_wpf.Query(() => OverlayState.IsActive));
    Assert.False(_wpf.Query(() => CameraMonitor.Instance.HasHolders));
  }
}
