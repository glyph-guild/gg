namespace Gg.Client;

/// <summary>The gg services this machine's service manager knows about.</summary>
public static class InstalledUnits
{
    /// <summary>Where systemd keeps the units an administrator installed.</summary>
    public const string SystemdDirectory = "/etc/systemd/system";

    /// <summary>Where launchd keeps the daemons an administrator installed.</summary>
    public const string LaunchdDirectory = "/Library/LaunchDaemons";

    /// <summary>The file names of the gg services installed here.</summary>
    public static IReadOnlyList<string> Find(
        string systemd = SystemdDirectory, string launchd = LaunchdDirectory) => [];
}
