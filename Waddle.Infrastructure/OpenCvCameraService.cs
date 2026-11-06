using OpenCvSharp;
using Waddle.Core;

namespace Waddle.Infrastructure;

/// Opens the webcam on a background thread, finds the biggest face in each frame,
/// draws a box around it and publishes the frame. Same logic as the CameraTest project.
public sealed class OpenCvCameraService : ICameraService
{
    private const int DetectWidth = 320;     // the face search runs on a copy this wide
    private const int PreviewWidth = 480;    // the picture sent to the screen is at most this wide
    private const double FlatContrast = 12;  // pixel variation below this = a flat picture (covered / privacy shutter)

    private readonly string _cascadePath;
    private readonly ILogService? _log;
    private Thread? _thread;
    private CancellationTokenSource? _cts;

    public event Action<FaceFrame>? FrameReady;
    public event Action<string>? Failed;

    public bool IsRunning => _thread is { IsAlive: true };

    public OpenCvCameraService(string cascadePath, ILogService? log = null)
    {
        _cascadePath = cascadePath;
        _log = log;
    }

    private readonly object _gate = new();      // Start/Stop can now be called from different threads

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _thread = new Thread(() => Run(token)) { IsBackground = true, Name = "Waddle camera" };
            _thread.Start();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _thread?.Join(1500);        // the loop checks the token every frame, so this is quick
            _thread = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Run(CancellationToken token)
    {
        try
        {
            if (!File.Exists(_cascadePath))
            {
                Fail($"Face model not found: {_cascadePath}");
                return;
            }

            using var capture = OpenCamera();
            if (capture == null) { Fail("Cannot open any camera."); return; }

            using var cascade = new CascadeClassifier(_cascadePath);
            using var frame = new Mat();
            using var gray = new Mat();
            using var small = new Mat();        // small grey copy: the face search runs on this (much cheaper)
            using var preview = new Mat();      // small colour copy: the box is drawn on this and sent to the screen
            _log?.Info("Camera started");

            while (!token.IsCancellationRequested)
            {
                capture.Read(frame);
                if (frame.Empty()) { if (!token.IsCancellationRequested) Fail("Camera stopped sending frames."); break; }

                // Work on a small copy: searching a full-size frame for faces was what heated the laptop.
                // Sizes are converted back, so FaceRatio (face width / frame width) stays the same as before.
                double detectScale = frame.Width > DetectWidth ? (double)DetectWidth / frame.Width : 1.0;
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                if (detectScale < 1.0) Cv2.Resize(gray, small, new Size(), detectScale, detectScale, InterpolationFlags.Area);
                else gray.CopyTo(small);

                Cv2.MeanStdDev(small, out var meanVal, out var stdVal);
                double brightness = meanVal.Val0;       // 0 = black, 255 = white (a covered lens is near 0)
                double contrast = stdVal.Val0;          // low = flat picture, high = a real scene

                // same minimum face size as before (80 px of the original frame), scaled to the small copy
                int minFace = Math.Max(20, (int)Math.Round(80 * detectScale));
                var faces = cascade.DetectMultiScale(
                    small, 1.1, 5, HaarDetectionTypes.ScaleImage, new Size(minFace, minFace));

                // the picture shown on screen is also smaller (less to encode and decode every frame)
                double previewScale = frame.Width > PreviewWidth ? (double)PreviewWidth / frame.Width : 1.0;
                if (previewScale < 1.0) Cv2.Resize(frame, preview, new Size(), previewScale, previewScale, InterpolationFlags.Area);
                else frame.CopyTo(preview);

                bool found = faces.Length > 0;

                // A covered lens is black, or a flat grey placeholder (privacy shutter). Report both as "dark"
                // so CameraMonitor treats them as covered. A found face always means the lens is not covered.
                if (!found && contrast < FlatContrast) brightness = 0;
                int faceWidth = 0;
                if (found)
                {
                    // biggest face = the person closest to the screen
                    var face = faces.OrderByDescending(f => f.Width).First();
                    faceWidth = (int)Math.Round(face.Width / detectScale);      // back to original-frame pixels

                    double k = previewScale / detectScale;                      // small-copy coordinates -> preview coordinates
                    var box = new Rect((int)(face.X * k), (int)(face.Y * k), (int)(face.Width * k), (int)(face.Height * k));
                    Cv2.Rectangle(preview, box, Scalar.LimeGreen, 2);
                }

                Cv2.ImEncode(".jpg", preview, out var jpg, new[] { (int)ImwriteFlags.JpegQuality, 70 });
                FrameReady?.Invoke(new FaceFrame(jpg, found, faceWidth, frame.Width, brightness));

                token.WaitHandle.WaitOne(200);      // about 5 frames per second, wakes early on Stop
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            Fail(ex.Message);
        }
        finally
        {
            _log?.Info("Camera stopped");
        }
    }

    private static VideoCapture? OpenCamera()
    {
        foreach (var api in new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.DSHOW })
        {
            for (int i = 0; i < 3; i++)
            {
                var c = new VideoCapture(i, api);
                if (c.IsOpened()) return c;
                c.Dispose();
            }
        }
        return null;
    }

    private void Fail(string message)
    {
        _log?.Info($"WARNING: camera: {message}");
        Failed?.Invoke(message);
    }
}