using System.Windows;
using System.Windows.Controls;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class SettingsWindowTests : IDisposable
{
    private readonly WpfApplicationFixture _wpf;
    private readonly StartupTestScope _scope = new();

    public SettingsWindowTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(WpfState.Reset);
    }

    public void Dispose()
    {
        _wpf.Invoke(WpfState.Reset);
        _scope.Dispose();
    }

    private void WithWindow(Action<SettingsWindow> test) => _wpf.Invoke(() =>
    {
        var window = new SettingsWindow();
        try { test(window); }
        finally { window.Close(); }
    });

    private static CheckBox Check(SettingsWindow w) => (CheckBox)w.FindName("StartupCheck");
    private static TextBlock Error(SettingsWindow w) => (TextBlock)w.FindName("ErrorText");

    /// <summary>Flips the checkbox, which is what a mouse click, the keyboard and a screen reader's Toggle all do.</summary>
    private static void ClickCheckbox(SettingsWindow w) => Check(w).IsChecked = Check(w).IsChecked != true;

    [Fact]
    public void Opens_Unticked_WhenStartupIsOff()
    {
        WithWindow(w => Assert.False(Check(w).IsChecked));
    }

    [Fact]
    public void Opens_Ticked_WhenStartupIsAlreadyOn()
    {
        StartupManager.Set(true);

        WithWindow(w => Assert.True(Check(w).IsChecked));
    }

    [Fact]
    public void TickingTheBox_TurnsStartupOn_Immediately()
    {
        WithWindow(w =>
        {
            ClickCheckbox(w);

            Assert.True(Check(w).IsChecked);
            Assert.True(StartupManager.IsEnabled());
        });
    }

    [Fact]
    public void UntickingTheBox_TurnsStartupOff_Immediately()
    {
        StartupManager.Set(true);

        WithWindow(w =>
        {
            ClickCheckbox(w);

            Assert.False(Check(w).IsChecked);
            Assert.False(StartupManager.IsEnabled());
        });
    }

    [Fact]
    public void TickingThenUnticking_EndsUpOff()
    {
        WithWindow(w =>
        {
            ClickCheckbox(w);
            ClickCheckbox(w);

            Assert.False(StartupManager.IsEnabled());
        });
    }

    [Fact]
    public void ReopeningTheDialog_ShowsTheSavedChoice()
    {
        WithWindow(w => ClickCheckbox(w));                 // turn it on, close the dialog

        WithWindow(w => Assert.True(Check(w).IsChecked));  // a new dialog shows it on
    }

    [Fact]
    public void NoErrorIsShown_WhenTheChangeWorks()
    {
        WithWindow(w =>
        {
            ClickCheckbox(w);

            Assert.Equal(Visibility.Collapsed, Error(w).Visibility);
        });
    }

    [Fact]
    public void WhenWindowsRefuses_TheBoxGoesBack_AndAnErrorIsShown()
    {
        WithWindow(w =>
        {
            _scope.MakeRegistryRefuseWrites();

            ClickCheckbox(w);

            Assert.False(Check(w).IsChecked);
            Assert.Equal(Visibility.Visible, Error(w).Visibility);
            Assert.False(string.IsNullOrWhiteSpace(Error(w).Text));
        });
    }

    [Fact]
    public void TheErrorDisappears_OnceALaterChangeWorks()
    {
        WithWindow(w =>
        {
            _scope.MakeRegistryRefuseWrites();
            ClickCheckbox(w);
            Assert.Equal(Visibility.Visible, Error(w).Visibility);

            StartupManager.RunKeyPath = _scope.RunKeyPath;     // Windows lets us again
            ClickCheckbox(w);

            Assert.Equal(Visibility.Collapsed, Error(w).Visibility);
            Assert.True(StartupManager.IsEnabled());
        });
    }

    [Fact]
    public void DoneButton_ClosesTheDialog()
    {
        var closed = false;

        _wpf.Invoke(() =>
        {
            var window = new SettingsWindow();
            window.Closed += (_, _) => closed = true;
            window.Show();

            ((Button)window.FindName("DoneButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        Assert.True(closed);
    }
}