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

    // Wait briefly for the background worker thread to finish exiting after firing Failed
    var deadline = DateTime.UtcNow.AddSeconds(2);
    while (service.IsRunning && DateTime.UtcNow < deadline)
    {
      Thread.Sleep(10);
    }

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

  [Fact]
  public void Dispose_WhenNeverStarted_DoesNotThrow()
  {
    var service = new OpenCvCameraService("any-path.xml");

    var ex = Record.Exception(() => service.Dispose());

    Assert.Null(ex);
  }

  [Fact]
  public void Stop_CalledTwice_DoesNotThrow()
  {
    var service = new OpenCvCameraService(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.xml"));
    service.Start();

    var ex = Record.Exception(() =>
    {
      service.Stop();
      service.Stop();
    });

    Assert.Null(ex);
    Assert.False(service.IsRunning);
  }

  [Fact]
  public void Start_WhenCascadeFileMissing_LogsAWarning()
  {
    var log = new RecordingLog();
    var service = new OpenCvCameraService(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.xml"), log);
    using var failedSignal = new ManualResetEventSlim(false);
    service.Failed += _ => failedSignal.Set();

    service.Start();

    Assert.True(failedSignal.Wait(TimeSpan.FromSeconds(3)));
    service.Stop();
    Assert.Contains(log.InfoLines, l => l.StartsWith("WARNING: camera: Face model not found", StringComparison.Ordinal));
    Assert.Contains("Camera stopped", log.InfoLines);
  }

  [Fact]
  public void Start_WithAnUnusableCascadeFile_RaisesFailedInsteadOfCrashing()
  {
    // File exists, so the first check passes. With no webcam, OpenCamera() returns null ("Cannot open any camera").
    // With a webcam, the empty file is rejected as a face model. Either way Failed must fire and nothing may throw.
    string cascade = Path.Combine(Path.GetTempPath(), $"empty_cascade_{Guid.NewGuid()}.xml");
    File.WriteAllText(cascade, "<not-a-cascade/>");

    try
    {
      var service = new OpenCvCameraService(cascade);
      string? error = null;
      using var failedSignal = new ManualResetEventSlim(false);
      service.Failed += msg => { error = msg; failedSignal.Set(); };

      service.Start();

      Assert.True(failedSignal.Wait(TimeSpan.FromSeconds(10)), "Expected Failed to fire.");
      Assert.False(string.IsNullOrWhiteSpace(error));
      service.Stop();
    }
    finally
    {
      File.Delete(cascade);
    }
  }
}
