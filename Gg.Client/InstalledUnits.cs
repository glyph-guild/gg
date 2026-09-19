namespace Gg.Client;

/// <summary>The gg services this machine's service manager knows about.</summary>
public static class InstalledUnits
{
    /// <summary>Where systemd keeps the units an administrator installed.</summary>
    public const string SystemdDirectory = "/etc/systemd/system";

    /// <summary>Where launchd keeps the daemons an administrator installed.</summary>
    public const string LaunchdDirectory = "/Library/LaunchDaemons";

    /// <summary>The file names of the gg services installed here, sorted.</summary>
    /// <remarks>
    /// <b>By name, and only by name.</b> A unit is <c>gg-*.service</c> and a
    /// daemon a plist whose name carries <c>gg-</c> - what the runbooks and
    /// <c>gg service install</c> write. Reading one would need privileges a
    /// doctor should not ask for, and its presence is the whole fact needed:
    /// that there is a process here this shell is not.
    /// </remarks>
    public static IReadOnlyList<string> Find(
        string systemd = SystemdDirectory, string launchd = LaunchdDirectory) =>
        [
            .. Named(systemd, "*.service")
                .Where(name => name.StartsWith("gg-", StringComparison.Ordinal)),
            .. Named(launchd, "*.plist")
                .Where(name => name.Contains("gg-", StringComparison.Ordinal)),
        ];

    private static IEnumerable<string> Named(string directory, string pattern)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)
                    .ToList()
                : [];
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            // A directory this user may not list says nothing about what is in
            // it, and a doctor that threw here would report nothing at all.
            return [];
        }
    }
}
