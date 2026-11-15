using Waddle.Wpf;

namespace Waddle.Tests;

[Collection(WpfCollection.Name)]
public sealed class IdleReminderTests : IDisposable
{
    private readonly WpfApplicationFixture _wpf;

    public IdleReminderTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(Clean);
    }

    public void Dispose() => _wpf.Invoke(Clean);

    private static void Clean()
    {
        WpfState.Reset();
        while (OverlayState.IsActive) OverlayState.Exit();
        var idle = IdleReminder.Instance;
        idle.Arm();
        Private.Set(idle, "_first", true);
    }

    /// <summary>Pretends the timer has been off and the widget closed for five minutes, then checks once.</summary>
    private static void CheckAfterBeingIdleFiveMinutes()
    {
        var idle = IdleReminder.Instance;
        Private.Set(idle, "_idleSince", DateTime.Now - TimeSpan.FromMinutes(5));
        Private.Call(idle, "Check");
    }

    [Fact]
    public void Check_WhenIdleLongEnough_ShowsTheReminder()
    {
        int due = 0;
        void OnDue() => due++;

        _wpf.Invoke(() =>
        {
            IdleReminder.Instance.Due += OnDue;
            try { CheckAfterBeingIdleFiveMinutes(); }
            finally { IdleReminder.Instance.Due -= OnDue; }
        });

        Assert.Equal(1, due);
    }

    [Fact]
    public void Check_WhileAnOverlayIsOpen_NeverShowsTheReminder()
    {
        int due = 0;
        void OnDue() => due++;

        _wpf.Invoke(() =>
        {
            IdleReminder.Instance.Due += OnDue;
            try
            {
                OverlayState.Enter();           // e.g. the calibration overlay
                CheckAfterBeingIdleFiveMinutes();
            }
            finally { IdleReminder.Instance.Due -= OnDue; }
        });

        Assert.Equal(0, due);
    }

    [Fact]
    public void Check_AfterTheOverlayCloses_TheWaitStartsOver()
    {
        int due = 0;
        void OnDue() => due++;

        _wpf.Invoke(() =>
        {
            IdleReminder.Instance.Due += OnDue;
            try
            {
                OverlayState.Enter();
                CheckAfterBeingIdleFiveMinutes();
                OverlayState.Exit();

                Private.Call(IdleReminder.Instance, "Check");      // overlay just closed: a fresh wait begins, nothing yet
            }
            finally { IdleReminder.Instance.Due -= OnDue; }
        });

        Assert.Equal(0, due);
    }
}
