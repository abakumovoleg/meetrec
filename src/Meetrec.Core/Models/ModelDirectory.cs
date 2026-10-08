namespace Meetrec.Core.Models;

public static class ModelDirectory
{
    public const string OverrideVariable = "MEETREC_MODELS";

    public static string Resolve() => Resolve(
        Environment.GetEnvironmentVariable,
        OperatingSystem.IsWindows(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string Resolve(Func<string, string?> getEnvironmentVariable, bool isWindows, string home, string localAppData)
    {
        var overridden = getEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return Path.GetFullPath(overridden);
        }

        return isWindows
            ? Path.Combine(localAppData, "meetrec", "models")
            : Path.Combine(home, ".cache", "meetrec", "models");
    }
}
