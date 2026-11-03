using Waddle.Core;

namespace Waddle.Infrastructure;

public sealed class FileLogService : ILogService
{
    private readonly object _lock = new();
    public string LogDirectory { get; }

    public FileLogService(string? directory = null)
    {
        LogDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Waddle", "logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public void Info(string message) => Write("info", message);
    public void Security(string message) => Write("security", message);

    private void Write(string kind, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{kind.ToUpper()}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            File.AppendAllText(Path.Combine(LogDirectory, $"{kind}.log"), line);
        }
    }
    
}
