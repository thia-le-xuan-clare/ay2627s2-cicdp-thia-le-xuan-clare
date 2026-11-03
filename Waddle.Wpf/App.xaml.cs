using Waddle.Infrastructure.Data;
using System.IO;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Waddle.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waddle", "waddle.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        new DatabaseInitializer(dbPath).Initialize();
    }
}

