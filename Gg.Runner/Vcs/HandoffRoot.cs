namespace Gg.Runner.Vcs;

/// <summary>One flight's tree, kept because a person may want to take it over.</summary>
public sealed record HeldTree
{
    public required string FlightId { get; init; }

    /// <summary>Where it is, on this machine.</summary>
    public required string Path { get; init; }

    /// <summary>When it was moved here.</summary>
    public required DateTimeOffset HeldAt { get; init; }

    /// <summary>How much disk it is using.</summary>
    /// <remarks>
    /// Measured, because this is the first resource this product consumes in a
    /// customer's environment that nobody had put a number against. A retention
    /// policy chosen without one is a guess.
    /// </remarks>
    public required long Bytes { get; init; }
}

/// <summary>
/// Where a flight's tree goes when it ends with nowhere to land.
/// </summary>
/// <remarks>
/// <para>
/// <b>A separate root so the sweep does not have to learn anything.</b> The
/// startup sweep works because a runner that is starting holds no lease, so
/// every tree under the working root belongs to a process that is gone - "all of
/// them", a rule with no state behind it, which therefore cannot be wrong about
/// which trees are live. A sweep that had to know which trees were takeable
/// would lose exactly that.
/// </para>
/// <para>
/// So the tree MOVES. The working root stays statelessly sweepable and this root
/// has its own explicit retention, and neither rule needs to know about the
/// other.
/// </para>
/// <para>
/// <b>Kept for the flights that did not land.</b> A landed flight has a branch
/// and a proposal, so its work exists somewhere a person can fetch. A violated or
/// exhausted one has neither, and the work exists only here - which is precisely
/// the flight somebody wants to take over, and precisely the one that had nothing
/// left to take before this existed.
/// </para>
/// </remarks>
public sealed class HandoffRoot
{
    /// <summary>
    /// How long a held tree survives.
    /// </summary>
    /// <remarks>
    /// <b>A number somebody chose.</b> Seven days is long enough that a flight
    /// which ended on a Friday is still takeable on the Monday, and short enough
    /// that a machine running many flights does not fill up while nobody is
    /// looking. It is here, as one constant, so that changing it is a decision
    /// rather than an emergent property of when somebody last ran something.
    /// </remarks>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly string _path;
    private readonly TimeProvider _time;

    public HandoffRoot(string? path = null, TimeProvider? time = null)
    {
        _path = path ?? DefaultPath();
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Where held trees live when nobody overrides it.</summary>
    /// <remarks>
    /// Beside the working root rather than inside it, because a directory inside
    /// it is a directory the sweep deletes.
    /// </remarks>
    public static string DefaultPath() =>
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(WorkingTreeRoot.DefaultPath().TrimEnd('/'))
                ?? WorkingTreeRoot.DefaultPath(),
            "handoff");

    public string Path => _path;

    /// <summary>
    /// Moves a flight's trees here, and says how much disk that kept.
    /// </summary>
    /// <remarks>
    /// A move rather than a copy: the tree is already on this disk and copying it
    /// would double the cost of the one resource this root exists to spend
    /// deliberately. Across filesystems a move is a copy anyway, and that is the
    /// platform's business rather than a case to write twice.
    /// </remarks>
    public HeldTree? Hold(string flightId, string from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightId);

        if (!Directory.Exists(from))
        {
            return null;
        }

        var destination = For(flightId);

        Directory.CreateDirectory(_path);
        Restrict(_path);

        if (Directory.Exists(destination))
        {
            // A second flight with the same id is not a thing, so this is a
            // leftover from a run that died between the move and the delete.
            Directory.Delete(destination, recursive: true);
        }

        Directory.Move(from, destination);

        var heldAt = _time.GetUtcNow();

        // Written down, because retention is measured against it and the runner
        // that wrote it will have restarted long before it expires. Reading it
        // back off the filesystem's own timestamps would be a SECOND derivation
        // of one fact, and the two disagree the moment anything copies a tree.
        Directory.CreateDirectory(Marks);
        File.WriteAllText(MarkFor(flightId), heldAt.ToString("O"));

        return new HeldTree
        {
            FlightId = flightId,
            Path = destination,
            HeldAt = heldAt,
            Bytes = Size(destination),
        };
    }

    /// <summary>Where a flight's held tree is, whether or not it exists.</summary>
    public string For(string flightId) =>
        System.IO.Path.Combine(_path, Safe(flightId));

    /// <summary>Everything currently held.</summary>
    public IReadOnlyList<HeldTree> Held()
    {
        if (!Directory.Exists(_path))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateDirectories(_path)
                .Where(d => !string.Equals(
                    System.IO.Path.GetFileName(d), MarksDirectory, StringComparison.Ordinal))
                .Select(d => new HeldTree
                {
                    FlightId = System.IO.Path.GetFileName(d),
                    Path = d,
                    HeldAt = HeldAt(System.IO.Path.GetFileName(d), d),
                    Bytes = Size(d),
                }),
        ];
    }

    /// <summary>
    /// Deletes what has outlived the retention, and says what it deleted.
    /// </summary>
    /// <remarks>
    /// <b>Returned rather than logged.</b> Expiring somebody's only copy of an
    /// agent's work is a thing that has to be recorded where a person will find
    /// it - a flight that was takeable on Monday and is not on Tuesday, with no
    /// line anywhere saying why, is the silent degradation this project keeps
    /// naming.
    /// </remarks>
    public IReadOnlyList<HeldTree> Expire()
    {
        var cutoff = _time.GetUtcNow() - Retention;
        var expired = new List<HeldTree>();

        foreach (var tree in Held())
        {
            if (tree.HeldAt <= cutoff)
            {
                expired.Add(tree);
                Release(tree.FlightId);
            }
        }

        return expired;
    }

    /// <summary>Removes one held tree, once nobody needs it.</summary>
    public void Release(string flightId)
    {
        var directory = For(flightId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        if (File.Exists(MarkFor(flightId)))
        {
            File.Delete(MarkFor(flightId));
        }
    }

    /// <summary>Where the held-at marks live.</summary>
    /// <remarks>
    /// Beside the trees rather than inside them: a file of ours in somebody's
    /// working tree is a file they have to explain to git.
    /// </remarks>
    private const string MarksDirectory = ".held";

    private string Marks => System.IO.Path.Combine(_path, MarksDirectory);

    private string MarkFor(string flightId) =>
        System.IO.Path.Combine(Marks, Safe(flightId) + ".txt");

    /// <summary>
    /// When this tree was held, from the mark, or from the filesystem when there
    /// is none.
    /// </summary>
    /// <remarks>
    /// The fallback is for a tree held by a version that did not write marks.
    /// Treating it as brand new would keep it forever; treating it as ancient
    /// would delete somebody's work on upgrade. The filesystem's answer is
    /// roughly right and is the least bad of the three.
    /// </remarks>
    private DateTimeOffset HeldAt(string flightId, string directory) =>
        File.Exists(MarkFor(flightId))
        && DateTimeOffset.TryParse(
               File.ReadAllText(MarkFor(flightId)),
               System.Globalization.CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.RoundtripKind,
               out var marked)
            ? marked
            : Directory.GetCreationTimeUtc(directory);

    /// <summary>How much disk a directory is using.</summary>
    private static long Size(string directory)
    {
        try
        {
            return Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch (IOException)
        {
            // A tree being written while it is measured is not a reason to fail
            // the move that is measuring it.
            return 0;
        }
    }

    private static string Safe(string flightId) =>
        new([.. flightId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')]);

    /// <summary>0700, because this is where somebody else's source code is.</summary>
    private static void Restrict(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
