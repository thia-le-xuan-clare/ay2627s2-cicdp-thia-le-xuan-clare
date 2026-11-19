using System.IO;
using Microsoft.Win32;
using Waddle.Wpf;

namespace Waddle.Tests;

/// <summary>
/// Points StartupManager at a throwaway registry key and a throwaway marker file, so tests never touch the
/// user's real "start with Windows" entry. Puts everything back (and deletes the key) on Dispose.
/// </summary>
public sealed class StartupTestScope : IDisposable
{
    private readonly string _originalRunKey = StartupManager.RunKeyPath;
    private readonly string _originalFlagPath = StartupManager.AskedFlagPath;
    private readonly string _rootKey = $@"Software\WaddleTests\{Guid.NewGuid():N}";

    /// <summary>A folder that does NOT exist yet, so MarkAsked has to create it.</summary>
    public string FlagDirectory { get; } = Path.Combine(Path.GetTempPath(), $"waddle-startup-{Guid.NewGuid():N}", "nested");

    public string FlagPath => Path.Combine(FlagDirectory, ".startup-asked");

    public string RunKeyPath => _rootKey + @"\Run";

    public StartupTestScope()
    {
        StartupManager.RunKeyPath = RunKeyPath;
        StartupManager.AskedFlagPath = FlagPath;
    }

    /// <summary>Makes every registry write fail (a key name over 255 characters is rejected by Windows).</summary>
    public void MakeRegistryRefuseWrites() => StartupManager.RunKeyPath = new string('x', 300);

    /// <summary>What is stored in the Run key right now, or null when there is no entry.</summary>
    public string? ReadRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(StartupManager.RunValueName) as string;
    }

    public void Dispose()
    {
        StartupManager.RunKeyPath = _originalRunKey;
        StartupManager.AskedFlagPath = _originalFlagPath;

        Registry.CurrentUser.DeleteSubKeyTree(_rootKey, throwOnMissingSubKey: false);

        var root = Path.GetDirectoryName(FlagDirectory)!;      // the waddle-startup-<guid> folder
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
