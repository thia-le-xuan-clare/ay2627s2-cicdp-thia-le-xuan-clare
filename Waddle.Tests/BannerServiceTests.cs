using System.Windows;
using System.Windows.Controls;
using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class BannerServiceTests : IDisposable
{
    // far enough away that the banner is only queued and no window is ever created
    private static readonly TimeSpan Later = TimeSpan.FromSeconds(60);

    private readonly WpfApplicationFixture _wpf;
    private readonly RecordingLog _log = new();

    public BannerServiceTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(() =>
        {
            WpfState.Reset();
            BannerService.Instance.Log = _log;
        });
    }

    public void Dispose() => _wpf.Invoke(WpfState.Reset);

    private static BannerService Banner => BannerService.Instance;

    private int Count(string startsWith) => _log.InfoLines.Count(l => l.StartsWith(startsWith));

    private static string? CurrentMessage()
    {
        var current = Private.Get<object?>(Banner, "_current");
        return current is null ? null : Private.Get<string>(current, "Message");
    }

    private static BannerWindow Window() => Private.Get<BannerWindow>(Banner, "_window");

    private static Button ButtonNamed(string name) => (Button)Window().FindName(name);

    private static void Click(string name) => ButtonNamed(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // ---------- which banner wins ----------

    [Fact]
    public void Show_WithADelay_QueuesTheBannerAndShowsNothingYet()
    {
        _wpf.Invoke(() => Banner.Show(BannerKind.CameraOff, "Camera is disabled", "Try again", delay: TimeSpan.FromSeconds(30)));

        Assert.Contains("Banner queued: \"Camera is disabled\" (shows in 30 s)", _log.InfoLines);
        Assert.Equal(0, Count("Banner shown"));
    }

    [Fact]
    public void Show_LessUrgentWhileAMoreUrgentOneIsWaiting_IsIgnored()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.CameraOff, "camera off", delay: Later);
            Banner.Show(BannerKind.Away, "away", delay: Later);

            Assert.Equal("camera off", CurrentMessage());
        });
    }

    [Theory]
    [InlineData(BannerKind.Away, BannerKind.Covered)]
    [InlineData(BannerKind.CameraOff, BannerKind.Posture)]
    public void Show_MoreUrgentWhileALessUrgentOneIsWaiting_ReplacesIt(BannerKind first, BannerKind second)
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(first, "less urgent", delay: Later);
            Banner.Show(second, "more urgent", delay: Later);

            Assert.Equal("more urgent", CurrentMessage());
        });
    }

    // ---------- when the widget is open ----------

    [Fact]
    public void Show_WhileTheWidgetIsOpenAndExpanded_WaitsInsteadOfShowing()
    {
        _wpf.Invoke(() =>
        {
            TimeTracker.Instance.IsWidgetOpen = true;
            Banner.Show(BannerKind.CameraOff, "Camera is disabled");
        });

        Assert.Contains("Banner waiting: the widget is open (it shows after the widget is closed or collapsed)", _log.InfoLines);
        Assert.Equal(0, Count("Banner shown"));
    }

    [Fact]
    public void Show_WhileTheWidgetIsCollapsed_ShowsTheBanner()
    {
        _wpf.Invoke(() =>
        {
            TimeTracker.Instance.IsWidgetOpen = true;
            TimeTracker.Instance.IsWidgetCollapsed = true;
            Banner.Show(BannerKind.Covered, "Camera is covered");
        });

        Assert.Contains("Banner shown: \"Camera is covered\"", _log.InfoLines);
    }

    [Fact]
    public void Show_PostureWarning_IgnoresAnOpenWidgetAndShowsStraightAway()
    {
        _wpf.Invoke(() =>
        {
            TimeTracker.Instance.IsWidgetOpen = true;       // the widget has no posture message, so the banner must still appear
            Banner.Show(BannerKind.Posture, "Please sit up straight", nudge: false, duck: DuckEmotion.Angry);
        });

        Assert.Contains("Banner shown: \"Please sit up straight\"", _log.InfoLines);
        Assert.Equal(0, Count("Banner waiting"));
    }

    // ---------- the countdown text ----------

    [Fact]
    public void UpdateMessage_ForTheCurrentKind_ChangesTheText()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Posture, "20s left", delay: Later);

            Banner.UpdateMessage(BannerKind.Posture, "19s left");

            Assert.Equal("19s left", CurrentMessage());
        });
    }

    [Fact]
    public void UpdateMessage_ForADifferentKind_LeavesTheTextAlone()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Posture, "20s left", delay: Later);

            Banner.UpdateMessage(BannerKind.Away, "something else");

            Assert.Equal("20s left", CurrentMessage());
        });
    }

    [Fact]
    public void UpdateMessage_WhileTheBannerIsOnScreen_UpdatesTheWindowText()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Posture, "20s left", nudge: false, duck: DuckEmotion.Angry);

            Banner.UpdateMessage(BannerKind.Posture, "19s left");

            var text = (TextBlock)Window().FindName("MessageText");
            Assert.Equal("19s left", text.Text);
        });
    }

    // ---------- clearing ----------

    [Fact]
    public void Clear_ForTheSameKind_RemovesTheBanner_ButNotForAnotherKind()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Posture, "20s left", delay: Later);

            Banner.Clear(BannerKind.Away);
            Assert.Equal("20s left", CurrentMessage());

            Banner.Clear(BannerKind.Posture);
            Assert.Null(CurrentMessage());
        });

        Assert.Contains("Banner cleared", _log.InfoLines);
    }

    // ---------- buttons and dismissing ----------

    [Fact]
    public void ActionButton_RunsTheQuickAction()
    {
        var clicks = 0;

        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Away, "Welcome back. Resume?", "Resume", onButton: () => clicks++);

            Click("ActionButton");
        });

        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task ActionButton_WithASlowAction_ShowsTryingUntilItFinishes()
    {
        var gate = new TaskCompletionSource();

        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.CameraOff, "Camera is disabled", "Try again", onButtonAsync: () => gate.Task, nudge: false);
            Click("ActionButton");
        });

        var (label, enabled) = _wpf.Query(() => ((string)ButtonNamed("ActionButton").Content, ButtonNamed("ActionButton").IsEnabled));
        Assert.Equal("Trying...", label);
        Assert.False(enabled);

        gate.SetResult();

        Assert.True(await _wpf.WaitUntilAsync(() => ButtonNamed("ActionButton").IsEnabled));
        Assert.Equal("Try again", _wpf.Query(() => (string)ButtonNamed("ActionButton").Content));
    }

    [Fact]
    public void CloseButton_HidesTheBanner_AndSchedulesOneReminder()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Covered, "Camera is covered");

            Click("CloseButton");

            var current = Private.Get<object>(Banner, "_current");
            Assert.True(Private.Get<bool>(current, "Dismissed"));
            Assert.NotNull(Private.Get<DateTime?>(current, "NudgeAt"));
            Assert.False(Window().IsVisible);
        });

        Assert.Contains("Banner dismissed", _log.InfoLines);
    }

    [Fact]
    public void CloseButton_RaisesBannerDismissedWithTheCurrentKind()
    {
        BannerKind? dismissedKind = null;
        void Handler(BannerKind kind) => dismissedKind = kind;

        _wpf.Invoke(() =>
        {
            Banner.BannerDismissed += Handler;
            try
            {
                Banner.Show(BannerKind.Posture, "Please sit up straight", nudge: false, duck: DuckEmotion.Angry);
                Click("CloseButton");
            }
            finally
            {
                Banner.BannerDismissed -= Handler;
            }
        });

        Assert.Equal(BannerKind.Posture, dismissedKind);
    }

    [Fact]
    public void Refresh_OnceTheReminderIsDue_ShowsTheBannerAgain()
    {
        _wpf.Invoke(() =>
        {
            Banner.Show(BannerKind.Covered, "Camera is covered");
            Click("CloseButton");
            var current = Private.Get<object>(Banner, "_current");
            Private.Set(current, "NudgeAt", DateTime.Now - TimeSpan.FromSeconds(1));

            Private.Call(Banner, "Refresh");

            Assert.True(Window().IsVisible);
        });

        Assert.Equal(1, Count("Banner reminder"));
        Assert.Equal(2, Count("Banner shown"));
    }
}
