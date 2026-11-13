using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Waddle.Core;
using Waddle.Infrastructure;
using Waddle.Infrastructure.Data;

namespace Waddle.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private TaskbarIcon? _tray;

    /// True when a calibration exists. If not, opens the calibration overlay (with an optional message) and checks again.
    public static bool EnsureCalibrated(string? message = null)
    {
        var store = Services.GetRequiredService<ICalibrationStore>();
        if (store.Load() is not null) return true;

        var window = Services.GetRequiredService<CalibrationWindow>();
        if (message is not null && window.DataContext is CalibrationViewModel vm)
            vm.SubtitleText = message;

        window.ShowDialog();
        return store.Load() is not null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            // Create Tray Icon directly in C#
            var iconUri = new Uri("pack://application:,,,/Assets/duck-icon/duck_happy.ico");
            using var iconStream = GetResourceStream(iconUri)!.Stream;

            _tray = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(32, 32)),
                ToolTipText = "Waddle"
            };
            _tray.ForceCreate(enablesEfficiencyMode: false);

            var menu = new ContextMenu();
            var open = new MenuItem { Header = "Open Waddle" };
            open.Click += (_, _) =>
            {
                var w = Services.GetRequiredService<MainWindow>();
                w.Show();
                w.WindowState = WindowState.Normal;
                w.Activate();
            };
            var quit = new MenuItem { Header = "Quit" };
            quit.Click += (_, _) => Shutdown();
            menu.Items.Add(open);
            menu.Items.Add(quit);
            _tray.ContextMenu = menu;

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
            sc.AddSingleton<ICalibrationStore, SqliteCalibrationStore>();
            sc.AddTransient<CalibrationViewModel>();
            sc.AddTransient<CalibrationWindow>();
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

            camera.CameraLost += () =>
            {
                if (timer.AutoPause(PauseReason.CameraOff))
                    banner.Show(BannerKind.CameraOff, "Camera is disabled", "Try again",
                        onButtonAsync: () => camera.RetryAsync(),
                        delay: MonitorTuning.CameraOffBannerDelay, nudge: false);
            };

            camera.CameraCovered += () =>
            {
                if (timer.AutoPause(PauseReason.Covered))
                    banner.Show(BannerKind.Covered, "Camera is covered");
            };

            camera.CameraRestored += () =>
            {
                timer.AutoResume();
                banner.Clear(BannerKind.CameraOff);
                banner.Clear(BannerKind.Covered);
            };

            camera.AwayStarted += () => timer.AutoPause(PauseReason.Away);

            camera.FaceBack += () =>
            {
                if (timer.Reason == PauseReason.Away)
                    banner.Show(BannerKind.Away, "Welcome back. Resume?", "Resume", timer.ResumeFromAway);
            };

            timer.PauseReasonChanged += reason =>
            {
                if (reason is PauseReason.None or PauseReason.User) banner.ClearAll();
                if (reason == PauseReason.None) camera.ResetAwayWatch();
            };

            var idle = IdleReminder.Instance;
            idle.Log = log;                         // so the log shows why the reminder did or didn't appear
            timer.SessionEnded += () => { camera.Release("session"); banner.ClearAll(); };
            timer.SessionEnded += idle.Arm;
            idle.Due += () => Services.GetRequiredService<MainWindow>().ShowIdleReminder();
            timer.RetryCameraRequested += async () => await camera.RetryAsync();

            if (!EnsureCalibrated()) log.Info("Calibration skipped on first run");

            Services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup Crash");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}