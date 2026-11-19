using System.IO;
using Waddle.Wpf;

namespace Waddle.Tests;

// StartupManager has static settings, so these run one after another with the other WPF tests.
[Collection(WpfCollection.Name)]
public sealed class StartupManagerTests : IDisposable
{
    private readonly StartupTestScope _scope = new();

    public void Dispose() => _scope.Dispose();

    // ---- start with Windows (registry) ----

    [Fact]
    public void IsEnabled_IsFalse_WhenNothingIsRegistered()
    {
        Assert.False(StartupManager.IsEnabled());
    }

    [Fact]
    public void Set_True_RegistersWaddleAndIsEnabledBecomesTrue()
    {
        var ok = StartupManager.Set(true);

        Assert.True(ok);
        Assert.True(StartupManager.IsEnabled());
        Assert.NotNull(_scope.ReadRunValue());
    }

    [Fact]
    public void Set_True_StoresTheRunningExePathInQuotes()
    {
        StartupManager.Set(true);

        // quotes matter: without them a path like C:\Program Files\... would not launch at login
        Assert.Equal($"\"{Environment.ProcessPath}\"", _scope.ReadRunValue());
    }

    [Fact]
    public void Set_False_RemovesTheEntry()
    {
        StartupManager.Set(true);

        var ok = StartupManager.Set(false);

        Assert.True(ok);
        Assert.False(StartupManager.IsEnabled());
        Assert.Null(_scope.ReadRunValue());
    }

    [Fact]
    public void Set_False_WhenNothingIsRegistered_StillSucceeds()
    {
        Assert.True(StartupManager.Set(false));
        Assert.False(StartupManager.IsEnabled());
    }

    [Fact]
    public void Set_True_Twice_StaysEnabled()
    {
        Assert.True(StartupManager.Set(true));
        Assert.True(StartupManager.Set(true));

        Assert.True(StartupManager.IsEnabled());
    }

    [Fact]
    public void Set_CanBeToggledOnOffOn()
    {
        StartupManager.Set(true);
        StartupManager.Set(false);
        StartupManager.Set(true);

        Assert.True(StartupManager.IsEnabled());
    }

    [Fact]
    public void Set_ReturnsFalse_WhenWindowsRefusesTheWrite()
    {
        _scope.MakeRegistryRefuseWrites();

        Assert.False(StartupManager.Set(true));
        Assert.False(StartupManager.IsEnabled());      // and IsEnabled doesn't throw either
    }

    // ---- first-run marker ----

    [Fact]
    public void HasBeenAsked_IsFalse_OnAFreshInstall()
    {
        Assert.False(StartupManager.HasBeenAsked());
    }

    [Fact]
    public void MarkAsked_ThenHasBeenAskedIsTrue()
    {
        StartupManager.MarkAsked();

        Assert.True(StartupManager.HasBeenAsked());
    }

    [Fact]
    public void MarkAsked_CreatesTheFolderWhenItIsMissing()
    {
        Assert.False(Directory.Exists(_scope.FlagDirectory));

        StartupManager.MarkAsked();

        Assert.True(File.Exists(_scope.FlagPath));
    }

    [Fact]
    public void MarkAsked_CalledTwice_DoesNotThrow()
    {
        StartupManager.MarkAsked();
        StartupManager.MarkAsked();

        Assert.True(StartupManager.HasBeenAsked());
    }

    [Fact]
    public void MarkAsked_DoesNotTurnStartupOn()
    {
        StartupManager.MarkAsked();

        Assert.False(StartupManager.IsEnabled());
    }
}
