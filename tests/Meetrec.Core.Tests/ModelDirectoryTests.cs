using Meetrec.Core.Models;

namespace Meetrec.Core.Tests;

public class ModelDirectoryTests
{
    [Fact]
    public void Resolve_OverrideVariableSet_UsesItOnAnyOs()
    {
        // Arrange
        var custom = Path.Combine(Path.GetTempPath(), "custom-models");
        string? Env(string name) => name == ModelDirectory.OverrideVariable ? custom : null;

        // Act
        var result = ModelDirectory.Resolve(Env, isWindows: true, home: "/home/u", localAppData: @"C:\Local");

        // Assert
        Assert.Equal(Path.GetFullPath(custom), result);
    }

    [Fact]
    public void Resolve_Mac_UsesCacheInHome()
    {
        // Arrange
        string? Env(string _) => null;

        // Act
        var result = ModelDirectory.Resolve(Env, isWindows: false, home: "/Users/u", localAppData: "/unused");

        // Assert
        Assert.Equal(Path.Combine("/Users/u", ".cache", "meetrec", "models"), result);
    }

    [Fact]
    public void Resolve_Windows_UsesLocalAppData()
    {
        // Arrange
        string? Env(string _) => null;

        // Act
        var result = ModelDirectory.Resolve(Env, isWindows: true, home: "/unused", localAppData: "LocalAppData");

        // Assert
        Assert.Equal(Path.Combine("LocalAppData", "meetrec", "models"), result);
    }

    [Fact]
    public void Resolve_BlankOverride_IsIgnored()
    {
        // Arrange
        string? Env(string _) => "  ";

        // Act
        var result = ModelDirectory.Resolve(Env, isWindows: false, home: "/Users/u", localAppData: "/unused");

        // Assert
        Assert.Equal(Path.Combine("/Users/u", ".cache", "meetrec", "models"), result);
    }
}
