using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class StartupPromptWindowTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly StartupTestScope _scope = new();

  public StartupPromptWindowTests(WpfApplicationFixture wpf)
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

  private static CheckBox SignInCheck(StartupPromptWindow w) => (CheckBox)w.FindName("StartupCheck");
  private static CheckBox TimerCheck(StartupPromptWindow w) => (CheckBox)w.FindName("AutoTimerCheck");

  private static void ClickContinue(StartupPromptWindow w) =>
      ((Button)w.FindName("ContinueButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

  private static void PressEscape(StartupPromptWindow w)
  {
    using var source = new HwndSource(new HwndSourceParameters("waddle-test"));
    w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
  }

  /// <summary>Runs the body on the WPF thread and tells it whether the window closed.</summary>
  private void WithWindow(Action<StartupPromptWindow, Func<bool>> test) => _wpf.Invoke(() =>
  {
    var window = new StartupPromptWindow();
    var closed = false;
    window.Closed += (_, _) => closed = true;
    try { test(window, () => closed); }
    finally { if (!closed) window.Close(); }
  });

  [Fact]
  public void BothOptions_StartUnticked()
  {
    WithWindow((w, _) =>
    {
      Assert.False(SignInCheck(w).IsChecked);
      Assert.False(TimerCheck(w).IsChecked);
    });
  }

  [Fact]
  public void Continue_WithNothingTicked_LeavesBothOff_RemembersItAsked_AndCloses()
  {
    WithWindow((w, closed) =>
    {
      ClickContinue(w);

      Assert.False(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
      Assert.True(StartupManager.HasBeenAsked());
      Assert.True(closed());
    });
  }

  [Fact]
  public void Continue_WithSignInTicked_TurnsStartupOn_ButNotTheTimer()
  {
    WithWindow((w, closed) =>
    {
      SignInCheck(w).IsChecked = true;

      ClickContinue(w);

      Assert.True(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
      Assert.True(StartupManager.HasBeenAsked());
      Assert.True(closed());
    });
  }

  [Fact]
  public void Continue_WithTimerTicked_TurnsAutoStartOn_ButNotSignIn()
  {
    WithWindow((w, closed) =>
    {
      TimerCheck(w).IsChecked = true;

      ClickContinue(w);

      Assert.True(StartupManager.IsAutoStartTimerEnabled());
      Assert.False(StartupManager.IsEnabled());
      Assert.True(closed());
    });
  }

  [Fact]
  public void Continue_WithBothTicked_TurnsBothOn()
  {
    WithWindow((w, _) =>
    {
      SignInCheck(w).IsChecked = true;
      TimerCheck(w).IsChecked = true;

      ClickContinue(w);

      Assert.True(StartupManager.IsEnabled());
      Assert.True(StartupManager.IsAutoStartTimerEnabled());
    });
  }

  [Fact]
  public void Nothing_IsSaved_UntilContinueIsPressed()
  {
    WithWindow((w, _) =>
    {
      SignInCheck(w).IsChecked = true;
      TimerCheck(w).IsChecked = true;

      Assert.False(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
    });
  }

  [Fact]
  public void Escape_SkipsTheQuestion_SavesNothing_AndDoesNotAskAgain()
  {
    WithWindow((w, closed) =>
    {
      SignInCheck(w).IsChecked = true;
      TimerCheck(w).IsChecked = true;

      PressEscape(w);

      Assert.False(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
      Assert.True(StartupManager.HasBeenAsked());
      Assert.True(closed());
    });
  }

  [Fact]
  public void ClosingTheWindowAnyOtherWay_StillRemembersItAsked_WithoutTurningAnythingOn()
  {
    WithWindow((w, closed) =>
    {
      w.Close();      // e.g. Alt+F4

      Assert.False(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
      Assert.True(StartupManager.HasBeenAsked());
    });
  }

  [Fact]
  public void Continue_WhenWindowsRefusesTheWrite_StillClosesAndDoesNotAskAgain()
  {
    StartupTestScope.MakeRegistryRefuseWrites();

    WithWindow((w, closed) =>
    {
      SignInCheck(w).IsChecked = true;

      ClickContinue(w);

      Assert.True(closed());
      Assert.False(StartupManager.IsEnabled());
    });

    // the marker is written to its own (valid) path, so the question is not repeated every launch
    Assert.True(StartupManager.HasBeenAsked());
  }

  [Fact]
  public void Window_CoversTheWholeWorkArea()
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
  public void Options_SayWhatTheyDo()
  {
    WithWindow((w, _) =>
    {
      Assert.Equal("Launch Waddle when I sign in to Windows", SignInCheck(w).Content);
      Assert.Equal("Start posture timer automatically on launch", TimerCheck(w).Content);
      Assert.Equal("Continue", ((Button)w.FindName("ContinueButton")).Content);
    });
  }

  [Fact]
  public async Task WhileOpen_CountsAsAnOverlay_SoTheIdleReminderStaysAway()
  {
    StartupPromptWindow? window = null;
    _wpf.Invoke(() =>
    {
      window = new StartupPromptWindow();
      window.Show();
    });

    Assert.True(await _wpf.WaitUntilAsync(() => OverlayState.IsActive), "overlay never became active");

    _wpf.Invoke(() => window!.Close());

    Assert.False(_wpf.Query(() => OverlayState.IsActive));
  }
}
