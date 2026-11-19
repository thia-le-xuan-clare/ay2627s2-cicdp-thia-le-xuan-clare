namespace Waddle.Wpf;

/// <summary>
/// Counts the full-screen overlays (calibration, posture warning) that are open right now.
/// Things that pop up on their own, like the idle reminder, check this and stay away. UI thread only.
/// </summary>
public static class OverlayState
{
  private static int _count;

  public static bool IsActive => _count > 0;

  public static void Enter() => _count++;

  public static void Exit()
  {
    if (_count > 0) _count--;
  }
}
