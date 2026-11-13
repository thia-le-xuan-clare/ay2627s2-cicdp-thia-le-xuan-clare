using System.IO;
using System.Text.RegularExpressions;
using Waddle.Infrastructure;

namespace Waddle.Tests;

public sealed class FileLogServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"waddle-logs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Constructor_CreatesTheLogFolder()
    {
        _ = new FileLogService(_dir);

        Assert.True(Directory.Exists(_dir));
    }

    [Fact]

    public void Info_And_Security_Write_To_Separate_Files()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var log = new FileLogService(dir);

        log.Info("hello");
        log.Security("camera on");

        Assert.Contains("hello", File.ReadAllText(Path.Combine(dir, "info.log")));
        Assert.Contains("camera on", File.ReadAllText(Path.Combine(dir, "security.log")));
    }

    [Fact]
    
    public void Info_WritesATimestampedLineToInfoLog()
    {
        var log = new FileLogService(_dir);

        log.Info("hello");

        var line = Assert.Single(File.ReadAllLines(Path.Combine(_dir, "info.log")));
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[INFO\] hello$"), line);
    }

    [Fact]
    public void Security_WritesToSecurityLog_NotInfoLog()
    {
        var log = new FileLogService(_dir);

        log.Security("camera requested");

        Assert.Contains("[SECURITY] camera requested", File.ReadAllText(Path.Combine(_dir, "security.log")));
        Assert.False(File.Exists(Path.Combine(_dir, "info.log")));
    }

    [Fact]
    public void Log_AppendsInsteadOfOverwriting()
    {
        var log = new FileLogService(_dir);

        log.Info("first");
        log.Info("second");

        Assert.Equal(2, File.ReadAllLines(Path.Combine(_dir, "info.log")).Length);
    }

    [Fact]
    public void Log_FromManyThreadsAtOnce_LosesNoLines()
    {
        var log = new FileLogService(_dir);

        Parallel.For(0, 50, i => log.Info($"line {i}"));

        Assert.Equal(50, File.ReadAllLines(Path.Combine(_dir, "info.log")).Length);
    }

    [Fact]
    public void Info_WhenTheLogFileIsLocked_DoesNotThrow()
    {
        // Fails until you wrap File.AppendAllText in try/catch (IOException) in FileLogService.
        var log = new FileLogService(_dir);
        using var locked = new FileStream(Path.Combine(_dir, "info.log"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var error = Record.Exception(() => log.Info("should not crash the app"));

        Assert.Null(error);
    }
}
