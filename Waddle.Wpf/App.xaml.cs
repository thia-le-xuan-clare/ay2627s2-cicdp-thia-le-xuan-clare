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
        var db = new DatabaseInitializer(dbPath);   // kept in a variable so the store can use it
        db.Initialize();

        // Dependency injection
        var sc = new ServiceCollection();
        sc.AddSingleton(db);                                          // NEW
        sc.AddSingleton<IDailyTimeStore, SqliteDailyTimeStore>();     // NEW
        sc.AddSingleton<ILogService, FileLogService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
        Services = sc.BuildServiceProvider();

        var log = Services.GetRequiredService<ILogService>();
        log.Info("App started");
        log.Info($"Database ready at {dbPath}");

        // NEW: give the shared timer its logger and its database store
        TimeTracker.Instance.Log = log;
        TimeTracker.Instance.Store = Services.GetRequiredService<IDailyTimeStore>();

        Services.GetRequiredService<MainWindow>().Show();
    }
}