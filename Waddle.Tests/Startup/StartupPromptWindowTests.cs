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

    private static void Click(StartupPromptWindow w, string buttonName) =>
        ((Button)w.FindName(buttonName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
    public void Yes_TurnsStartupOn_RemembersItAsked_AndCloses()
    {
        WithWindow((w, closed) =>
        {
            Click(w, "YesButton");

            Assert.True(StartupManager.IsEnabled());
            Assert.True(StartupManager.HasBeenAsked());
            Assert.True(closed());
        });
    }

    [Fact]
    public void No_LeavesStartupOff_RemembersItAsked_AndCloses()
    {
        WithWindow((w, closed) =>
        {
            Click(w, "NoButton");

            Assert.False(StartupManager.IsEnabled());
            Assert.True(StartupManager.HasBeenAsked());
            Assert.True(closed());
        });
    }

    [Fact]
    public void Escape_ActsLikeNoThanks()
    {
        WithWindow((w, closed) =>
        {
            PressEscape(w);

            Assert.False(StartupManager.IsEnabled());
            Assert.True(StartupManager.HasBeenAsked());
            Assert.True(closed());
        });
    }

    [Fact]
    public void ClosingTheWindowAnyOtherWay_StillRemembersItAsked_WithoutTurningStartupOn()
    {
        WithWindow((w, closed) =>
        {
            w.Close();      // e.g. Alt+F4

            Assert.False(StartupManager.IsEnabled());
            Assert.True(StartupManager.HasBeenAsked());
        });
    }

    [Fact]
    public void Yes_WhenWindowsRefusesTheWrite_StillClosesAndDoesNotAskAgain()
    {
        _scope.MakeRegistryRefuseWrites();

        WithWindow((w, closed) =>
        {
            Click(w, "YesButton");

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
    public void Buttons_SayWhatTheyDo()
    {
        WithWindow((w, _) =>
        {
            Assert.Equal("Yes, start on login", ((Button)w.FindName("YesButton")).Content);
            Assert.Equal("No thanks", ((Button)w.FindName("NoButton")).Content);
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
