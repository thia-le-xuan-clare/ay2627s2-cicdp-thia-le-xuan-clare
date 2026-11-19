using Waddle.Wpf;

namespace Waddle.Tests;

// OverlayState is static, so these run with the other WPF tests (one after another) and leave it empty.
[Collection(WpfCollection.Name)]
public sealed class OverlayStateTests : IDisposable
{
  public OverlayStateTests() => Drain();

  public void Dispose() => Drain();

  private static void Drain()
  {
    while (OverlayState.IsActive) OverlayState.Exit();
  }

  [Fact]
  public void IsActive_IsFalseWhenNoOverlayIsOpen() => Assert.False(OverlayState.IsActive);

  [Fact]
  public void Enter_ThenExit_TurnsItOnThenOff()
  {
    OverlayState.Enter();
    Assert.True(OverlayState.IsActive);

    OverlayState.Exit();
    Assert.False(OverlayState.IsActive);
  }

  [Fact]
  public void TwoOverlays_KeepItActiveUntilBothAreClosed()
  {
    OverlayState.Enter();     // e.g. calibration
    OverlayState.Enter();     // e.g. the posture overlay

    OverlayState.Exit();
    Assert.True(OverlayState.IsActive);

    OverlayState.Exit();
    Assert.False(OverlayState.IsActive);
  }

  [Fact]
  public void Exit_WithNothingOpen_DoesNotGoNegative()
  {
    OverlayState.Exit();

    OverlayState.Enter();
    Assert.True(OverlayState.IsActive);

    OverlayState.Exit();
    Assert.False(OverlayState.IsActive);
  }
}
