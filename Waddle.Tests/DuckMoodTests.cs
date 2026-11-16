using Waddle.Wpf;

namespace Waddle.Tests;

/// <summary>The dashboard duck and its message follow the time sat up today (and slouching).</summary>
[Collection(WpfCollection.Name)]
public sealed class DuckMoodTests : IDisposable
{
    private readonly WpfApplicationFixture _wpf;

    public DuckMoodTests(WpfApplicationFixture wpf)
    {
        _wpf = wpf;
        _wpf.Invoke(Clean);
    }

    public void Dispose() => _wpf.Invoke(Clean);

    private static TimeTracker Tracker => TimeTracker.Instance;

    private static void Clean()
    {
        WpfState.Reset();
        Tracker.IsBadPosture = false;    // Reset only clears it through Stop(), which does nothing when the timer isn't running
    }

    /// <summary>Pretends today's saved time is this many seconds.</summary>
    private static void SatUpToday(double seconds)
    {
        var cache = Private.Get<Dictionary<DateOnly, double>>(Tracker, "_cache");
        cache[DateOnly.FromDateTime(DateTime.Now)] = seconds;
    }

    // ---------- which animation ----------

    [Fact]
    public void MoodEmotion_WithNoTimeToday_IsSad()
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(0);

            Assert.Equal(DuckEmotion.Sad, Tracker.MoodEmotion);
        });
    }

    [Fact]
    public void MoodEmotion_WithLessThanASecond_IsStillSad()
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(0.5);

            Assert.Equal(DuckEmotion.Sad, Tracker.MoodEmotion);
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(300.0)]
    [InlineData(1800.0)]
    [InlineData(7200.0)]
    public void MoodEmotion_WithTimeToday_IsHappy(double seconds)
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(seconds);

            Assert.Equal(DuckEmotion.Happy, Tracker.MoodEmotion);
        });
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(600.0)]
    public void MoodEmotion_WhileSlouching_IsAngry_WhateverTheTime(double seconds)
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(seconds);

            Tracker.IsBadPosture = true;

            Assert.Equal(DuckEmotion.Angry, Tracker.MoodEmotion);
        });
    }

    [Fact]
    public void MoodEmotion_AfterSittingUpAgain_GoesBackToTheTimeBasedMood()
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(600);
            Tracker.IsBadPosture = true;

            Tracker.IsBadPosture = false;

            Assert.Equal(DuckEmotion.Happy, Tracker.MoodEmotion);
        });
    }

    // ---------- which message ----------

    [Theory]
    [InlineData(0.0, "Waddle misses you!\nStart your timer.")]
    [InlineData(0.5, "Waddle misses you!\nStart your timer.")]
    [InlineData(1.0, "Nice start!\nKeep it up!")]
    [InlineData(59.0, "Nice start!\nKeep it up!")]
    [InlineData(60.0, "You're on a roll.\nSit tall!")]
    [InlineData(119.0, "You're on a roll.\nSit tall!")]
    [InlineData(120.0, "You are going strong!")]
    [InlineData(179.0, "You are going strong!")]
    [InlineData(180.0, "Over 3 minutes of great posture!")]
    [InlineData(10000.0, "Over 3 minutes of great posture!")]
    public void MoodMessage_FollowsTheTimeSatUpToday(double seconds, string expected)
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(seconds);

            Assert.Equal(expected, Tracker.MoodMessage);
        });
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(2000.0)]
    public void MoodMessage_WhileSlouching_TellsThemToSitUp_WhateverTheTime(double seconds)
    {
        _wpf.Invoke(() =>
        {
            SatUpToday(seconds);

            Tracker.IsBadPosture = true;

            Assert.Equal("Sit up straight, Waddle's watching!", Tracker.MoodMessage);
        });
    }

    // ---------- the animation frames ----------

    [Theory]
    [InlineData(DuckEmotion.Happy)]
    [InlineData(DuckEmotion.Sad)]
    [InlineData(DuckEmotion.Angry)]
    [InlineData(DuckEmotion.Sleep)]
    public void DuckAnimator_HasLoadableFrames_ForEveryEmotion(DuckEmotion emotion)
    {
        _wpf.Invoke(() =>
        {
            var duck = new DuckAnimator();

            duck.Force(emotion);

            Assert.Equal(emotion, duck.Emotion);
            Assert.True(duck.FrameWidth > 0);
            Assert.True(duck.FrameHeight > 0);
        });
    }

    [Fact]
    public void DuckAnimator_Force_PinsTheEmotion_AndNullFollowsTheTimerAgain()
    {
        _wpf.Invoke(() =>
        {
            var duck = new DuckAnimator();

            duck.Force(DuckEmotion.Angry);
            Assert.Equal(DuckEmotion.Angry, duck.Emotion);

            duck.Force(null);             // timer isn't running, so it falls back to sleep
            Assert.Equal(DuckEmotion.Sleep, duck.Emotion);
        });
    }
}
