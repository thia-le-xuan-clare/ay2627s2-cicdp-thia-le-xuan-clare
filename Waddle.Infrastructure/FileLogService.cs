using System.Globalization;
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
    // Escape line breaks so a message can never forge extra log lines (log injection)
    var safeMessage = message.Replace("\r", "\\r").Replace("\n", "\\n");
    var line = string.Create(
        CultureInfo.InvariantCulture,
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{kind.ToUpperInvariant()}] {safeMessage}{Environment.NewLine}");
    lock (_lock)
    {
      try
      {
        File.AppendAllText(Path.Combine(LogDirectory, $"{kind}.log"), line);
      }
      catch (IOException)
      {
        // Ignore locked log files so logging never crashes the app
      }
    }
  }
}
