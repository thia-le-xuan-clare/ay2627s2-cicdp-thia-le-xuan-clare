using System.Windows;
using System.Windows.Controls;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class SettingsPanelTests : IDisposable
{
  private readonly WpfApplicationFixture _wpf;
  private readonly StartupTestScope _scope = new();

  public SettingsPanelTests(WpfApplicationFixture wpf)
  {
    _wpf = wpf;
    _wpf.Invoke(WpfState.Reset);
  }

  public void Dispose()
  {
    _wpf.Invoke(WpfState.Reset);
    _scope.Dispose();
  }

  private void WithPanel(Action<SettingsPanel> test) => _wpf.Invoke(() => test(new SettingsPanel()));

  private static CheckBox SignInCheck(SettingsPanel p) => (CheckBox)p.FindName("StartupCheck");
  private static CheckBox TimerCheck(SettingsPanel p) => (CheckBox)p.FindName("AutoTimerCheck");
  private static TextBlock Error(SettingsPanel p) => (TextBlock)p.FindName("ErrorText");

  /// <summary>Flips a checkbox, which is what a mouse click, the keyboard and a screen reader's Toggle all do.</summary>
  private static void Click(CheckBox box) => box.IsChecked = box.IsChecked != true;

  // ---- launch when I sign in to Windows ----

  [Fact]
  public void Opens_BothUnticked_OnAFreshInstall()
  {
    WithPanel(p =>
    {
      Assert.False(SignInCheck(p).IsChecked);
      Assert.False(TimerCheck(p).IsChecked);
    });
  }

  [Fact]
  public void Opens_SignInTicked_WhenStartupIsAlreadyOn()
  {
    StartupManager.Set(true);

    WithPanel(p =>
    {
      Assert.True(SignInCheck(p).IsChecked);
      Assert.False(TimerCheck(p).IsChecked);
    });
  }

  [Fact]
  public void TickingSignIn_TurnsStartupOn_Immediately()
  {
    WithPanel(p =>
    {
      Click(SignInCheck(p));

      Assert.True(StartupManager.IsEnabled());
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
    });
  }

  [Fact]
  public void UntickingSignIn_TurnsStartupOff_Immediately()
  {
    StartupManager.Set(true);

    WithPanel(p =>
    {
      Click(SignInCheck(p));

      Assert.False(SignInCheck(p).IsChecked);
      Assert.False(StartupManager.IsEnabled());
    });
  }

  // ---- start posture timer automatically on launch ----

  [Fact]
  public void Opens_TimerTicked_WhenAutoStartIsAlreadyOn()
  {
    StartupManager.SetAutoStartTimer(true);

    WithPanel(p =>
    {
      Assert.True(TimerCheck(p).IsChecked);
      Assert.False(SignInCheck(p).IsChecked);
    });
  }

  [Fact]
  public void TickingTimer_TurnsAutoStartOn_WithoutTouchingSignIn()
  {
    WithPanel(p =>
    {
      Click(TimerCheck(p));

      Assert.True(StartupManager.IsAutoStartTimerEnabled());
      Assert.False(StartupManager.IsEnabled());
    });
  }

  [Fact]
  public void UntickingTimer_TurnsAutoStartOff_Immediately()
  {
    StartupManager.SetAutoStartTimer(true);

    WithPanel(p =>
    {
      Click(TimerCheck(p));

      Assert.False(TimerCheck(p).IsChecked);
      Assert.False(StartupManager.IsAutoStartTimerEnabled());
    });
  }

  [Fact]
  public void Refresh_PicksUpChangesMadeWhileThePanelWasHidden()
  {
    WithPanel(p =>
    {
      StartupManager.Set(true);
      StartupManager.SetAutoStartTimer(true);

      p.Refresh();

      Assert.True(SignInCheck(p).IsChecked);
      Assert.True(TimerCheck(p).IsChecked);
    });
  }

  [Fact]
  public void ReopeningThePanel_ShowsTheSavedChoices()
  {
    WithPanel(p => { Click(SignInCheck(p)); Click(TimerCheck(p)); });

    WithPanel(p =>
    {
      Assert.True(SignInCheck(p).IsChecked);
      Assert.True(TimerCheck(p).IsChecked);
    });
  }

  // ---- errors ----

  [Fact]
  public void NoErrorIsShown_WhenTheChangeWorks()
  {
    WithPanel(p =>
    {
      Click(SignInCheck(p));
      Click(TimerCheck(p));

      Assert.Equal(Visibility.Collapsed, Error(p).Visibility);
    });
  }

  [Fact]
  public void WhenWindowsRefusesSignIn_TheBoxGoesBack_AndAnErrorIsShown()
  {
    WithPanel(p =>
    {
      StartupTestScope.MakeRegistryRefuseWrites();

      Click(SignInCheck(p));

      Assert.False(SignInCheck(p).IsChecked);
      Assert.Equal(Visibility.Visible, Error(p).Visibility);
      Assert.False(string.IsNullOrWhiteSpace(Error(p).Text));
    });
  }

  [Fact]
  public void WhenTheTimerMarkerCantBeSaved_TheBoxGoesBack_AndAnErrorIsShown()
  {
    WithPanel(p =>
    {
      _scope.MakeAutoTimerFlagRefuseWrites();

      Click(TimerCheck(p));

      Assert.False(TimerCheck(p).IsChecked);
      Assert.Equal(Visibility.Visible, Error(p).Visibility);
    });
  }

  [Fact]
  public void TheErrorDisappears_OnceALaterChangeWorks()
  {
    WithPanel(p =>
    {
      StartupTestScope.MakeRegistryRefuseWrites();
      Click(SignInCheck(p));
      Assert.Equal(Visibility.Visible, Error(p).Visibility);

      StartupManager.RunKeyPath = _scope.RunKeyPath;     // Windows lets us again
      Click(SignInCheck(p));

      Assert.Equal(Visibility.Collapsed, Error(p).Visibility);
      Assert.True(StartupManager.IsEnabled());
    });
  }

  // ---- Done ----

  [Fact]
  public void DoneButton_AsksTheHostToClose()
  {
    var closeRequested = false;

    _wpf.Invoke(() =>
    {
      var panel = new SettingsPanel();
      panel.CloseRequested += () => closeRequested = true;

      ((Button)panel.FindName("DoneButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    });

    Assert.True(closeRequested);
  }
}
