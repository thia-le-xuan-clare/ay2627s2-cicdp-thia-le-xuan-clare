using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Waddle.Core;
using Waddle.Infrastructure;
using Waddle.Infrastructure.Data;

namespace Waddle.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Database setup
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waddle", "waddle.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var db = new DatabaseInitializer(dbPath);
        db.Initialize();

        // Dependency injection
        var sc = new ServiceCollection();
        sc.AddSingleton(db);
        sc.AddSingleton<IDailyTimeStore, SqliteDailyTimeStore>();
        sc.AddSingleton<ILogService, FileLogService>();
        sc.AddSingleton<ICameraService>(sp => new OpenCvCameraService(
            Path.Combine(AppContext.BaseDirectory, "haarcascade_frontalface_default.xml"),
            sp.GetRequiredService<ILogService>()));
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
        Services = sc.BuildServiceProvider();

        var log = Services.GetRequiredService<ILogService>();
        log.Info("App started");
        log.Info($"Database ready at {dbPath}");

        // Shared timer: where it saves and logs
        var timer = TimeTracker.Instance;
        timer.Log = log;
        timer.Store = Services.GetRequiredService<IDailyTimeStore>();

        // Shared camera, and how it ties to the timer and the banner
        var camera = CameraMonitor.Instance;
        var banner = BannerService.Instance;
        camera.Log = log;
        banner.Log = log;
        camera.Camera = Services.GetRequiredService<ICameraService>();

        // camera goes away -> pause. The banner waits 30 s so a meeting that borrows the camera stays quiet,
        // and it never nudges (a long meeting would just nag).
        camera.CameraLost += () =>
        {
            if (timer.AutoPause(PauseReason.CameraOff))
                banner.Show(BannerKind.CameraOff, "Camera is disabled", "Try again",
                    onButtonAsync: () => camera.RetryAsync(),       // button shows "Trying..." until the check finishes
                    delay: MonitorTuning.CameraOffBannerDelay, nudge: false);
        };

        // lens covered -> pause with a banner (no button: it resumes by itself once uncovered)
        camera.CameraCovered += () =>
        {
            if (timer.AutoPause(PauseReason.Covered))
                banner.Show(BannerKind.Covered, "Camera is covered");
        };

        // camera/lens back AND a face seen -> resume by itself
        camera.CameraRestored += () =>
        {
            timer.AutoResume();
            banner.Clear(BannerKind.CameraOff);
            banner.Clear(BannerKind.Covered);
        };

        // no face for a while -> pause quietly (nobody is there to read a banner)
        camera.AwayStarted += () => timer.AutoPause(PauseReason.Away);

        // face back after being away -> ask before resuming. Resuming here does NOT reopen the widget.
        camera.FaceBack += () =>
        {
            if (timer.Reason == PauseReason.Away)
                banner.Show(BannerKind.Away, "Welcome back. Resume?", "Resume", timer.ResumeFromAway);
        };

        // any banner goes away once the timer isn't paused by the camera/away any more
        // (resumed, paused by the user instead, or stopped)
        timer.PauseReasonChanged += reason =>
        {
            if (reason is PauseReason.None or PauseReason.User) banner.ClearAll();
            if (reason == PauseReason.None) camera.ResetAwayWatch();    // running again: watch for "away" from scratch
        };

        timer.SessionEnded += () => { camera.Release("session"); banner.ClearAll(); };   // session over -> camera off
        timer.RetryCameraRequested += async () => await camera.RetryAsync();

        Services.GetRequiredService<MainWindow>().Show();
    }
}