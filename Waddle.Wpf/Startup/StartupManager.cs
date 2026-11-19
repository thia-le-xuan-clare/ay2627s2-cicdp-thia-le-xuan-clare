using System.IO;
using Microsoft.Win32;

namespace Waddle.Wpf;

/// <summary>
/// Registers/unregisters Waddle to launch when the current user logs in to Windows
/// (HKCU Run key, so no admin rights needed), and remembers whether we've asked yet.
/// </summary>
public static class StartupManager
{
    public const string RunValueName = "Waddle";

    /// <summary>
    /// Registry key (under HKCU) that holds the start-on-login entry. Tests point this at a throwaway key,
    /// either by setting it or with the WADDLE_STARTUP_RUN_KEY environment variable (for the UI tests, which launch the real exe).
    /// </summary>
    public static string RunKeyPath { get; set; } =
        Environment.GetEnvironmentVariable("WADDLE_STARTUP_RUN_KEY")
        ?? @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Marker file that says the first-run question was shown. Overridable for tests (env var WADDLE_STARTUP_ASKED_FLAG).</summary>
    public static string AskedFlagPath { get; set; } =
        Environment.GetEnvironmentVariable("WADDLE_STARTUP_ASKED_FLAG")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waddle", ".startup-asked");

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) is string;
        }
        catch { return false; }
    }

    /// <summary>Turns "start with Windows" on or off. Returns false if Windows refused (e.g. locked down by policy).</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is null) return false;
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>True once the first-run "start with Windows?" question has been shown.</summary>
    public static bool HasBeenAsked() => File.Exists(AskedFlagPath);

    public static void MarkAsked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AskedFlagPath)!);
            File.WriteAllText(AskedFlagPath, "1");
        }
        catch { /* worst case we ask again next launch */ }
    }
}