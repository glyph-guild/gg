namespace Gg.Local;

/// <summary>The shapes this program ships in, as far as updating is concerned.</summary>
public enum InstallKind
{
    /// <summary>Nothing here could be identified — say so rather than guess.</summary>
    Unknown,

    /// <summary>A .NET tool in the invoking user's own tools directory.</summary>
    GlobalTool,

    /// <summary>A .NET tool installed to a named directory, which a pool host does.</summary>
    ToolPath,

    /// <summary>A self-contained Native AOT binary, with no update path at all.</summary>
    Native,

    /// <summary>A pool member. The image is the unit of change; nothing here is.</summary>
    Container,
}

/// <summary>
/// Which of those shapes this process is, so advice can name a command that works.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="SelfInvocation"/> cannot answer this, and says so itself:</b>
/// <i>"An apphost - AOT or framework-dependent - takes the verb directly."</i>
/// That type splits the <c>dotnet</c> host from an apphost, which is the
/// question a re-exec asks. A tool shim and a native binary are BOTH apphosts,
/// and separating those is the question here — so this is a second question
/// rather than a second copy of the first.
/// </para>
/// <para>
/// <b>The install layout is the tell.</b> A .NET tool shim sits beside a
/// <c>.store</c> directory holding the IL it runs; a self-contained binary has
/// nothing beside it. That is a fact about the directory rather than about the
/// executable, which is why this takes a predicate and not a path.
/// </para>
/// <para>
/// <b>Why global and tool-path are different answers.</b> A pool host installs
/// with <c>--tool-path</c> precisely so the runner cannot write its own
/// executable, and the unit runs as another user. Telling that machine to run
/// <c>dotnet tool update -g</c> installs into the home of whoever typed it,
/// leaves the shim alone, and reports success — the worst available outcome,
/// because it looks done.
/// </para>
/// </remarks>
/// <param name="Kind">Which shape.</param>
/// <param name="ToolPath">Where the tool lives, when that is what it is.</param>
public sealed record InstallShape(InstallKind Kind, string? ToolPath)
{
    /// <summary>What a .NET tool install puts beside its shim.</summary>
    private const string ToolStore = ".store";

    /// <summary>
    /// The shape of the running process.
    /// </summary>
    /// <remarks>
    /// Resolved once, as a process fact, for the reason
    /// <see cref="SelfInvocation.Current"/> gives: two reads that could differ
    /// are two answers to one question.
    /// </remarks>
    public static InstallShape Current { get; } = For(
        Environment.ProcessPath,
        Directory.Exists,
        InContainer(),
        GlobalToolsDirectory());

    /// <summary>
    /// The same decision over values, so every shape is testable without being in one.
    /// </summary>
    /// <param name="processPath"><c>Environment.ProcessPath</c>.</param>
    /// <param name="directoryExists">Whether a directory is there.</param>
    /// <param name="inContainer">Whether this is a pool member.</param>
    /// <param name="globalToolsDirectory">Where <c>-g</c> would have put it.</param>
    public static InstallShape For(
        string? processPath,
        Func<string, bool> directoryExists,
        bool inContainer,
        string? globalToolsDirectory)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        // FIRST, AND IT OUTRANKS THE LAYOUT. A member carries whichever shape
        // was baked into the image, and none of them is the unit of change
        // there - so what the filesystem says about the shim is true and beside
        // the point.
        if (inContainer)
        {
            return new InstallShape(InstallKind.Container, null);
        }

        if (processPath is not { Length: > 0 })
        {
            return new InstallShape(InstallKind.Unknown, null);
        }

        var directory = Path.GetDirectoryName(processPath);

        if (directory is not { Length: > 0 })
        {
            return new InstallShape(InstallKind.Unknown, null);
        }

        if (!directoryExists(Path.Combine(directory, ToolStore)))
        {
            return new InstallShape(InstallKind.Native, null);
        }

        return string.Equals(directory, globalToolsDirectory, StringComparison.Ordinal)
            ? new InstallShape(InstallKind.GlobalTool, directory)
            : new InstallShape(InstallKind.ToolPath, directory);
    }

    /// <summary>
    /// Whether this process is inside a container.
    /// </summary>
    /// <remarks>
    /// Two signals because either alone is missable: the runtime's own variable
    /// is set by the official base images and by nothing else, and the marker
    /// file is what Docker itself leaves. A member built from some other base
    /// still has the second.
    /// </remarks>
    private static bool InContainer() =>
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase)
        || File.Exists("/.dockerenv");

    /// <summary>
    /// Where a <c>-g</c> install would land for the invoking user.
    /// </summary>
    /// <remarks>
    /// <c>DOTNET_TOOLS_PATH</c> moves it, and honouring that is the difference
    /// between recognising a global install and calling it a tool-path one on
    /// exactly the machines somebody has configured deliberately.
    /// </remarks>
    private static string? GlobalToolsDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_TOOLS_PATH");

        if (configured is { Length: > 0 })
        {
            return configured;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return home is { Length: > 0 } ? Path.Combine(home, ".dotnet", "tools") : null;
    }
}
