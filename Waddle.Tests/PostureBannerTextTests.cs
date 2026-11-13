using System.Reflection;
using Waddle.Wpf;

namespace Waddle.Tests;

/// <summary>The text on the posture banner (the countdown the person reads).</summary>
public sealed class PostureBannerTextTests
{
    // PostureText is private static, so call it by name
    private static string PostureText(int secondsLeft) =>
        (string)typeof(App).GetMethod("PostureText", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { secondsLeft })!;

    [Theory]
    [InlineData(20, "Please sit up straight\n20s left")]
    [InlineData(1, "Please sit up straight\n1s left")]
    public void PostureText_WithSecondsLeft_ShowsTheCountdownUnderTheMessage(int secondsLeft, string expected)
    {
        Assert.Equal(expected, PostureText(secondsLeft));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PostureText_WithNoTimeLeft_ShowsOnlyTheMessage(int secondsLeft)
    {
        Assert.Equal("Please sit up straight", PostureText(secondsLeft));
    }
}
