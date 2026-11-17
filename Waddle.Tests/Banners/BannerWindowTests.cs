using System.Windows;
using System.Windows.Controls;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class BannerWindowTests : IDisposable
{
    private readonly WpfApplicationFixture _wpf;

    public BannerWindowTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(WpfState.Reset);
    }

    public void Dispose() => _wpf.Invoke(WpfState.Reset);

    /// <summary>Builds a banner window on the WPF thread, runs the test body, then closes it.</summary>
    private void WithWindow(Action<BannerWindow> test) => _wpf.Invoke(() =>
    {
        var window = new BannerWindow();
        try { test(window); }
        finally { window.Close(); }
    });

    private static TextBlock Message(BannerWindow w) => (TextBlock)w.FindName("MessageText");
    private static Button ActionBtn(BannerWindow w) => (Button)w.FindName("ActionButton");
    private static Button CloseBtn(BannerWindow w) => (Button)w.FindName("CloseButton");
    private static FrameworkElement Duck(BannerWindow w) => (FrameworkElement)w.FindName("DuckPanel");

    [Fact]
    public void SetContent_ShowsTheMessage()
    {
        WithWindow(w =>
        {
            w.SetContent("Please sit up straight\n20s left", null);

            Assert.Equal("Please sit up straight\n20s left", Message(w).Text);
        });
    }

    [Fact]
    public void SetContent_WithAButtonText_ShowsAnEnabledButton()
    {
        WithWindow(w =>
        {
            w.SetContent("Camera is disabled", "Try again");

            Assert.Equal("Try again", ActionBtn(w).Content);
            Assert.Equal(Visibility.Visible, ActionBtn(w).Visibility);
            Assert.True(ActionBtn(w).IsEnabled);
        });
    }

    [Fact]
    public void SetContent_WithoutAButtonText_HidesTheButton()
    {
        WithWindow(w =>
        {
            w.SetContent("Camera is covered", null);

            Assert.Equal(Visibility.Collapsed, ActionBtn(w).Visibility);
        });
    }

    [Fact]
    public void SetContent_WithADuck_ShowsTheDuck()
    {
        WithWindow(w =>
        {
            w.SetContent("Please sit up straight", null, DuckEmotion.Angry);

            Assert.Equal(Visibility.Visible, Duck(w).Visibility);
        });
    }

    [Fact]
    public void SetContent_WithoutADuck_HidesTheDuck()
    {
        WithWindow(w =>
        {
            w.SetContent("Please sit up straight", null, DuckEmotion.Angry);

            w.SetContent("Camera is covered", null);

            Assert.Equal(Visibility.Collapsed, Duck(w).Visibility);
        });
    }

    [Fact]
    public void SetBusy_DisablesTheButtonAndSaysTrying_ThenGivesItBack()
    {
        WithWindow(w =>
        {
            w.SetContent("Camera is disabled", "Try again");

            w.SetBusy(true);
            Assert.Equal("Trying...", ActionBtn(w).Content);
            Assert.False(ActionBtn(w).IsEnabled);

            w.SetBusy(false);
            Assert.Equal("Try again", ActionBtn(w).Content);
            Assert.True(ActionBtn(w).IsEnabled);
        });
    }

    [Fact]
    public void ActionButton_Click_RaisesActionClicked()
    {
        var raised = 0;

        WithWindow(w =>
        {
            w.ActionClicked += () => raised++;

            ActionBtn(w).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        Assert.Equal(1, raised);
    }

    [Fact]
    public void CloseButton_Click_RaisesDismissClicked()
    {
        var dismissed = 0;

        WithWindow(w =>
        {
            w.DismissClicked += () => dismissed++;

            CloseBtn(w).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        Assert.Equal(1, dismissed);
    }
}
