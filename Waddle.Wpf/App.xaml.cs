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

        // Database setup (unchanged)
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waddle", "waddle.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        new DatabaseInitializer(dbPath).Initialize();

        // Dependency injection
        var sc = new ServiceCollection();
        sc.AddSingleton<ILogService, FileLogService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
        Services = sc.BuildServiceProvider();

        var log = Services.GetRequiredService<ILogService>();
        log.Info("App started");
        log.Info($"Database ready at {dbPath}");

        Services.GetRequiredService<MainWindow>().Show();
    }
}