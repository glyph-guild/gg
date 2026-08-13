namespace Gg.Runner.Vcs;

/// <summary>
/// Where a customer's source code lives while a flight needs it, and not after.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trees must not leak.</b> A <c>SIGKILL</c> mid-clone leaves one behind -
/// no <c>finally</c> runs, no handler fires - so cleanup cannot be only a
/// disposal. It is the runner's own reconciliation problem, and it takes the
/// same shape the ready queue does: the reliable path, made fast enough. Trees
/// live under a known root keyed by flight id, and a startup sweep removes what
/// a previous life left.
/// </para>
/// <para>
/// A CACHE directory rather than the config directory the session and
/// credentials live in. Disk is the first resource this product consumes in
/// somebody else's environment, and a cache is the one location an operating
/// system and an operator both already understand to be removable without
/// asking.
/// </para>
/// </remarks>
public sealed class WorkingTreeRoot
{
    private readonly string _path;

    public WorkingTreeRoot(string? path = null) => _path = path ?? DefaultPath();

    /// <summary>Where trees live when nobody overrides it.</summary>
    public static string DefaultPath()
    {
        var cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        var root = !string.IsNullOrWhiteSpace(cacheHome)
            ? cacheHome
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "AppData/Local/Cache"
                : OperatingSystem.IsMacOS() ? "Library/Caches"
                : ".cache");

        return System.IO.Path.Combine(root, "good-grief", "trees");
    }

    /// <summary>The directory every tree lives under.</summary>
    public string Path => _path;

    /// <summary>
    /// A fresh, empty tree for one repository of one flight.
    /// </summary>
    /// <remarks>
    /// The flight id is in the path because the sweep keys on it, and the
    /// repository is hashed rather than named: a slug contains slashes and a
    /// customer's repository name is not something to spell out in a directory
    /// listing somebody might screenshot.
    /// </remarks>
    public string Prepare(string flightId, string slug)
    {
        var directory = For(flightId, slug);

        if (Directory.Exists(directory))
        {
            // A tree already here is from an earlier attempt at the same
            // flight. Reusing it would materialize a mixture of two fetches.
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
        Restrict(directory);
        return directory;
    }

    /// <summary>Where a given repository of a given flight would go.</summary>
    public string For(string flightId, string slug) =>
        System.IO.Path.Combine(_path, Safe(flightId), Fingerprint(slug));

    /// <summary>Whether this flight already had a tree here.</summary>
    /// <remarks>
    /// What <c>fresh-or-reused</c> is derived from. A field, not a feature -
    /// but a field nobody records is one nobody can ever start using.
    /// </remarks>
    public bool AlreadyHeld(string flightId) => Directory.Exists(FlightDirectory(flightId));

    /// <summary>Removes everything this flight put on disk.</summary>
    public void Release(string flightId)
    {
        var directory = FlightDirectory(flightId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Removes every tree under the root, and says how many there were.
    /// </summary>
    /// <remarks>
    /// Run at startup. A runner that is starting holds no lease, so every tree
    /// here belongs to a process that is gone - which makes the rule "all of
    /// them" rather than a set difference against something we would have to
    /// have persisted, and a rule with no state behind it cannot be wrong about
    /// which trees are live.
    /// </remarks>
    public int SweepOrphans()
    {
        if (!Directory.Exists(_path))
        {
            return 0;
        }

        var swept = 0;
        foreach (var directory in Directory.EnumerateDirectories(_path))
        {
            Directory.Delete(directory, recursive: true);
            swept++;
        }

        return swept;
    }

    /// <summary>Where this flight's trees live, whether or not they do.</summary>
    /// <remarks>
    /// Public so the handoff root can move them without being told the layout
    /// twice. One place decides where a flight's trees are.
    /// </remarks>
    public string For(string flightId) => FlightDirectory(flightId);

    private string FlightDirectory(string flightId) => System.IO.Path.Combine(_path, Safe(flightId));

    /// <summary>A flight id, reduced to something that is only ever one path segment.</summary>
    private static string Safe(string flightId) =>
        new([.. flightId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')]);

    /// <summary>A slug as a short hash. Never as a directory name.</summary>
    private static string Fingerprint(string slug) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(slug)))[..16].ToLowerInvariant();

    /// <summary>
    /// 0700, because this is where somebody else's source code is.
    /// </summary>
    /// <remarks>
    /// Every other user on the machine can otherwise read a customer's
    /// repository out of a cache directory, which is a worse disclosure than
    /// anything the wire protocol could manage.
    /// </remarks>
    private static void Restrict(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
