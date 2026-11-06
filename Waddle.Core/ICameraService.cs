using System;

namespace Waddle.Core;

/// <summary>
/// One camera frame (already drawn with the face box) plus what the detector found.
/// Brightness is the average grey level of the frame: 0 = pure black, 255 = pure white.
/// A covered lens gives a very low number.
/// </summary>
public sealed record FaceFrame(byte[] Jpeg, bool FaceFound, int FaceWidth, int FrameWidth, double Brightness = 255)
{
    /// <summary>Face width as a fraction of the frame width (same at any camera resolution).</summary>
    public double FaceRatio => FrameWidth == 0 ? 0 : (double)FaceWidth / FrameWidth;
}

public interface ICameraService
{
    /// <summary>Raised on a background thread for every processed frame.</summary>
    event Action<FaceFrame>? FrameReady;

    /// <summary>Raised on a background thread when the camera or face model can't be used.</summary>
    event Action<string>? Failed;

    bool IsRunning { get; }
    void Start();
    void Stop();
}