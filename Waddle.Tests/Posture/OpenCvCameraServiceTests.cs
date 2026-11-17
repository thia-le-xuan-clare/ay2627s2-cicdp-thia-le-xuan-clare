using System.IO;
using Waddle.Core;
using Waddle.Infrastructure;

namespace Waddle.Tests;

public class OpenCvCameraServiceTests
{
    [Fact]
    public void FaceFrame_FaceRatio_ComputesFractionCorrectly()
    {
        var frame = new FaceFrame(Array.Empty<byte>(), FaceFound: true, FaceWidth: 160, FrameWidth: 640);

        Assert.Equal(0.25, frame.FaceRatio, precision: 5);
    }

    [Fact]
    public void FaceFrame_ZeroFrameWidth_ReturnsZeroWithoutDivideByZero()
    {
        var frame = new FaceFrame(Array.Empty<byte>(), FaceFound: false, FaceWidth: 100, FrameWidth: 0);

        Assert.Equal(0, frame.FaceRatio);
    }

    [Fact]
    public void Start_WhenCascadeFileMissing_RaisesFailedEventAndStops()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.xml");
        var service = new OpenCvCameraService(missingPath);

        string? receivedError = null;
        using var failedSignal = new ManualResetEventSlim(false);

        service.Failed += msg =>
        {
            receivedError = msg;
            failedSignal.Set();
        };

        service.Start();

        Assert.True(failedSignal.Wait(TimeSpan.FromSeconds(3)), "Expected Failed event to fire within 3 seconds.");
        Assert.Contains("Face model not found", receivedError);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Stop_WhenNeverStarted_DoesNotThrow()
    {
        var service = new OpenCvCameraService("any-path.xml");

        var ex = Record.Exception(() => service.Stop());

        Assert.Null(ex);
        Assert.False(service.IsRunning);
    }
}
