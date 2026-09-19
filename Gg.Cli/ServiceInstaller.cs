namespace Gg.Cli;

/// <summary>Which service manager a machine has, as far as installing gg goes.</summary>
public enum ServicePlatform
{
    /// <summary>Linux with systemd running as its init.</summary>
    Systemd,

    /// <summary>macOS, whose daemons are launchd's.</summary>
    Launchd,
}

/// <summary>What a person asked <c>gg service install</c> for.</summary>
/// <param name="ControlPlane">Where the runner reports; required for a first install.</param>
/// <param name="Enroll">Whether an enrollment token is to be read, prompted or piped - never an argument.</param>
/// <param name="User">The service user, or null for the platform's default.</param>
public sealed record ServiceRequest(string? ControlPlane, bool Enroll, string? User);

/// <summary>What happened, in sentences, and whether it was done or refused.</summary>
public sealed record ServiceOutcome(bool Done, IReadOnlyList<string> Said);

/// <summary>Raised by a host whose side effect did not happen.</summary>
public sealed class ServiceHostException(string message) : Exception(message);

/// <summary>
/// Every side effect <c>gg service install</c> has, and nothing else.
/// </summary>
public interface IServiceHost
{
    /// <summary>The service manager this machine has, or null for none this build installs on.</summary>
    ServicePlatform? Platform { get; }

    /// <summary>Whether this process may write root-owned files and create users.</summary>
    bool IsElevated { get; }

    /// <summary>Whether a file or directory is at this path.</summary>
    bool Exists(string path);

    /// <summary>A user's home directory, or null when there is no such user.</summary>
    string? HomeOf(string user);

    /// <summary>The groups a user is in.</summary>
    IReadOnlyList<string> GroupsOf(string user);

    /// <summary>Makes a system user with its own group and this home, and no login shell.</summary>
    void CreateUser(string user, string home);

    /// <summary>Makes a directory owned by <paramref name="owner"/> with this mode.</summary>
    void CreateDirectory(string path, string owner, UnixFileMode mode);

    /// <summary>Writes a file by rename, owned by <paramref name="owner"/>, with this mode from its first byte.</summary>
    void WriteFile(string path, string content, string owner, UnixFileMode mode);

    /// <summary>A file's text, or null when it is not there.</summary>
    string? ReadFile(string path);

    /// <summary>Removes a file, or a directory that is empty. Absent is not an error.</summary>
    void Delete(string path);

    /// <summary>Whether this is a directory with nothing in it.</summary>
    bool IsEmptyDirectory(string path);

    /// <summary>Enables and starts the service a definition describes.</summary>
    void Start(ServicePlatform platform, string name, string definition);

    /// <summary>Restarts a running service, so it runs the gg now installed.</summary>
    void Restart(ServicePlatform platform, string name);

    /// <summary>Stops and disables a service.</summary>
    void Stop(ServicePlatform platform, string name, string definition);

    /// <summary>Tells the service manager a definition is gone.</summary>
    void Forget(ServicePlatform platform, string name);
}

/// <summary><c>gg service install</c> and <c>gg service uninstall</c>.</summary>
public static class ServiceInstaller
{
    /// <summary>The gg a service runs: a root-owned link that install.sh makes.</summary>
    public const string Binary = "/usr/local/bin/gg";

    /// <summary>What an install wrote, so an uninstall removes exactly that.</summary>
    public const string Manifest = "/etc/gg/service.manifest";

    /// <summary>The systemd unit's name.</summary>
    public const string SystemdName = "gg-runner-up";

    /// <summary>The launchd daemon's label, and so its plist's name.</summary>
    /// <remarks>
    /// <b>With <c>gg-</c> in it</b>, as the systemd unit has: the doctor
    /// recognizes a gg service by that prefix to say which checks describe the
    /// shell it was run from rather than the service.
    /// </remarks>
    public const string LaunchdLabel = "dev.glyphguild.gg-runner-up";

    /// <summary>Installs the service. Not built yet.</summary>
    public static ServiceOutcome Install(IServiceHost host, ServiceRequest request, Func<string> readToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(readToken);

        return new ServiceOutcome(false, ["gg service install is not built yet."]);
    }

    /// <summary>Removes what an install wrote. Not built yet.</summary>
    public static ServiceOutcome Uninstall(IServiceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new ServiceOutcome(false, ["gg service uninstall is not built yet."]);
    }
}
