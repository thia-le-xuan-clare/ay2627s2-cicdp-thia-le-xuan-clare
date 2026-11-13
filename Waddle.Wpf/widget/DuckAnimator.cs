using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Waddle.Wpf;

public enum DuckEmotion { Happy, Sad, Angry, Sleep }

/// <summary>
/// Swaps between the two frames of the current emotion every 500 ms so the duck looks animated.
/// Emotion follows the timer by default; Force() pins one (e.g. Angry while slouching).
/// </summary>
public sealed class DuckAnimator : INotifyPropertyChanged
{
    private static readonly Dictionary<DuckEmotion, BitmapImage[]> Frames = new()
    {
        [DuckEmotion.Angry] = Load("angry", 1, 2),
        [DuckEmotion.Happy] = Load("happy", 3, 4),
        [DuckEmotion.Sad]   = Load("sad",   5, 6),
        [DuckEmotion.Sleep] = Load("sleep", 7, 8),
    };

    private static BitmapImage[] Load(string folder, params int[] nums) =>
        nums.Select(n =>
        {
            var bmp = new BitmapImage(new Uri(
                $"pack://application:,,,/Assets/ducks-animation/{folder}/duck_{n}.png", UriKind.Absolute));
            bmp.Freeze();
            return bmp;
        }).ToArray();

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DuckEmotion? _forced;
    private int _index;

    public DuckEmotion Emotion { get; private set; } = DuckEmotion.Happy;
    public BitmapImage Frame => Frames[Emotion][_index];

    // the frames are different sizes, so the image is sized per frame inside a fixed canvas (see XAML)
    public double FrameWidth => Frame.PixelWidth;
    public double FrameHeight => Frame.PixelHeight;

    public DuckAnimator()
    {
        _timer.Tick += (_, _) => { _index ^= 1; Notify(); };     // frame 0 <-> frame 1
    }

    public void Start()
    {
        if (_timer.IsEnabled) return;
        TimeTracker.Instance.PropertyChanged += OnTrackerChanged;
        UpdateEmotion();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        TimeTracker.Instance.PropertyChanged -= OnTrackerChanged;
    }

    /// <summary>Pin an emotion (e.g. Angry when slouching). Pass null to follow the timer again.</summary>
    public void Force(DuckEmotion? emotion)
    {
        _forced = emotion;
        UpdateEmotion();
    }

    private void OnTrackerChanged(object? s, PropertyChangedEventArgs e) => UpdateEmotion();

    private void UpdateEmotion()
    {
        var t = TimeTracker.Instance;
        var wanted = _forced ?? (!t.IsRunning ? DuckEmotion.Sleep : t.Reason switch
        {
            PauseReason.None => DuckEmotion.Happy,
            PauseReason.Covered or PauseReason.CameraOff => DuckEmotion.Sad,
            _ => DuckEmotion.Sleep                                // paused by you, or away
        });

        if (wanted == Emotion) return;
        Emotion = wanted;
        _index = 0;
        Notify();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}