using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Waddle.Core;

namespace Waddle.Tests;

/// <summary>
/// A camera that does exactly what the test tells it to: nothing, one frame the moment it starts, or a failure.
/// Plug it into CameraMonitor.Instance.Camera. Frame() builds a real (tiny) JPEG, so call it on the WPF thread.
/// </summary>
internal sealed class FakeCameraService : ICameraService
{
  private int _startCalls;
  private int _stopCalls;

  public event Action<FaceFrame>? FrameReady;
  public event Action<string>? Failed;

  public bool IsRunning { get; private set; }
  public int StartCalls => Volatile.Read(ref _startCalls);
  public int StopCalls => Volatile.Read(ref _stopCalls);

  /// <summary>Sent as soon as Start() is called. Null = the camera stays silent.</summary>
  public FaceFrame? FrameOnStart { get; set; }

  /// <summary>Raised instead of a frame when Start() is called.</summary>
  public string? FailureOnStart { get; set; }

  public void Start()
  {
    Interlocked.Increment(ref _startCalls);
    IsRunning = true;
    if (FailureOnStart is not null) Failed?.Invoke(FailureOnStart);
    else if (FrameOnStart is not null) FrameReady?.Invoke(FrameOnStart);
  }

  public void Stop()
  {
    Interlocked.Increment(ref _stopCalls);
    IsRunning = false;
  }

  public void RaiseFrame(FaceFrame frame) => FrameReady?.Invoke(frame);

  public void RaiseFailure(string message) => Failed?.Invoke(message);

  /// <summary>A frame the monitor can decode. Brightness below 20 counts as a covered lens.</summary>
  public static FaceFrame Frame(int faceCount = 1, double faceRatio = 0.3, double brightness = 200)
  {
    const int frameWidth = 1000;
    var faceWidth = faceCount > 0 ? (int)Math.Round(faceRatio * frameWidth) : 0;
    return new FaceFrame(TinyJpeg(), faceCount > 0, faceWidth, frameWidth, brightness, faceCount);
  }

  private static byte[] TinyJpeg()
  {
    var pixels = new byte[4 * 12];
    Array.Fill(pixels, (byte)128);
    var bitmap = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr24, null, pixels, 12);

    var encoder = new JpegBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
  }
}
