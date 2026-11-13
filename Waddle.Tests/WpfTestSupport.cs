using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Waddle.Core;
using Waddle.Wpf;

namespace Waddle.Tests;

/// <summary>
/// Gives the WPF tests what the real app has: one STA thread with a running Dispatcher and an Application
/// (so DispatcherTimer, Window and the pack:// image URIs work). Everything that touches the WPF singletons
/// (TimeTracker, BannerService, PostureMonitor, CameraMonitor) must run on this thread: use Invoke / Query.
/// </summary>
public sealed class WpfApplicationFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Exception> _unhandled = new();
    private Dispatcher? _dispatcher;

    public WpfApplicationFixture()
    {
        var ready = new ManualResetEventSlim(false);
        Exception? failure = null;

        _thread = new Thread(() =>
        {
            try
            {
                PointPackUrisAtWaddleWpf();

                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/Waddle.Wpf;component/Styles.xaml", UriKind.Relative)   // same as App.xaml
                });
                _dispatcher = Dispatcher.CurrentDispatcher;

                // an exception on this thread would otherwise take the whole test host down; hand it to the next test instead
                _dispatcher.UnhandledException += (_, e) =>
                {
                    _unhandled.Enqueue(e.Exception);
                    e.Handled = true;
                };
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }

            if (failure is null) Dispatcher.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();

        ready.Wait();
        if (failure is not null) throw new InvalidOperationException("Could not start the WPF test host.", failure);
    }

    /// <summary>
    /// pack://application:,,,/Assets/... must point at Waddle.Wpf (where the duck images live), not at the test host.
    /// </summary>
    private static void PointPackUrisAtWaddleWpf()
    {
        var wpf = typeof(MainViewModel).Assembly;

        try { Application.ResourceAssembly = wpf; }
        catch (InvalidOperationException) { /* something already read it */ }

        if (Application.ResourceAssembly != wpf)
        {
            // last resort: write the private field the setter would have written
            typeof(Application)
                .GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, wpf);
        }

        if (Application.ResourceAssembly != wpf)
            throw new InvalidOperationException(
                $"pack://application URIs point at '{Application.ResourceAssembly?.GetName().Name}', not Waddle.Wpf, so the duck images cannot load.");
    }

    /// <summary>Runs the action on the WPF thread and rethrows anything it throws (so asserts inside it fail the test).</summary>
    public void Invoke(Action action)
    {
        Exception? error = null;
        _dispatcher!.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        if (error is null && _unhandled.TryDequeue(out var late))
            error = new InvalidOperationException("Unhandled exception on the WPF thread: " + late.Message, late);
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    /// <summary>Same as Invoke, but returns a value.</summary>
    public T Query<T>(Func<T> read)
    {
        T result = default!;
        Invoke(() => result = read());
        return result;
    }

    /// <summary>Polls on the WPF thread (letting async continuations run in between) until the condition is true.</summary>
    public async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (Query(condition)) return true;
            await Task.Delay(25);
        }
        return Query(condition);
    }

    public void Dispose() => _dispatcher?.InvokeShutdown();
}

/// <summary>All WPF tests share one fixture and run one after another, because they share the singletons.</summary>
[CollectionDefinition(Name)]
public sealed class WpfCollection : ICollectionFixture<WpfApplicationFixture>
{
    public const string Name = "Wpf";
}

public sealed class RecordingLog : ILogService
{
    public List<string> InfoLines { get; } = new();
    public List<string> SecurityLines { get; } = new();

    public void Info(string message) => InfoLines.Add(message);
    public void Security(string message) => SecurityLines.Add(message);
}

public sealed class FakeCalibrationStore : ICalibrationStore
{
    public CalibrationData? Saved { get; set; }
    public int LoadCalls { get; set; }

    public CalibrationData? Load()
    {
        LoadCalls++;
        return Saved;
    }

    public void Save(CalibrationData data) => Saved = data;
}

/// <summary>Reaches into private members. The WPF singletons have private constructors and private timers, so this is the only way in.</summary>
internal static class Private
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static T Get<T>(object target, string field) => (T)FieldOf(target, field).GetValue(target)!;

    public static void Set(object target, string field, object? value) => FieldOf(target, field).SetValue(target, value);

    public static void SetProperty(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, Any) ?? throw new MissingMemberException(target.GetType().Name, name)).SetValue(target, value);

    public static object? Call(object target, string method, params object?[] args) =>
        (target.GetType().GetMethod(method, Any) ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, args);

    /// <summary>Removes every handler from a field-like event.</summary>
    public static void ClearEvent(object target, string eventName) => Set(target, eventName, null);

    private static FieldInfo FieldOf(object target, string name) =>
        target.GetType().GetField(name, Any) ?? throw new MissingFieldException(target.GetType().Name, name);
}

/// <summary>Swaps App.Services (private setter) for a container that only knows the given calibration store.</summary>
internal static class AppServices
{
    public static void Use(ICalibrationStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICalibrationStore>(store);
        Set(services.BuildServiceProvider());
    }

    public static void Clear() => Set(null);

    private static void Set(IServiceProvider? provider) =>
        typeof(App).GetProperty(nameof(App.Services))!.SetValue(null, provider);
}

/// <summary>Puts the shared singletons back to "just launched". Call it on the WPF thread before and after each test.</summary>
internal static class WpfState
{
    public static void Reset()
    {
        var tracker = TimeTracker.Instance;
        Private.ClearEvent(tracker, "SessionEnded");
        Private.ClearEvent(tracker, "RetryCameraRequested");
        Private.ClearEvent(tracker, "PauseReasonChanged");
        tracker.Stop();                                  // does nothing when not running
        tracker.IsWidgetOpen = false;
        tracker.IsWidgetCollapsed = false;
        tracker.Store = null;
        tracker.Log = null;

        BannerService.Instance.ClearAll();
        BannerService.Instance.Log = null;

        var posture = PostureMonitor.Instance;
        Private.ClearEvent(posture, "SlouchStarted");
        Private.ClearEvent(posture, "CountdownTick");
        Private.ClearEvent(posture, "CountdownExpired");
        Private.ClearEvent(posture, "SlouchEnded");
        Private.Call(posture, "Reset");
        posture.Store = null;
        posture.Log = null;

        var camera = CameraMonitor.Instance;
        Private.SetProperty(camera, "IsOn", false);
        Private.SetProperty(camera, "FaceCount", 0);
        Private.SetProperty(camera, "FaceRatio", 0.0);

        AppServices.Clear();
    }
}