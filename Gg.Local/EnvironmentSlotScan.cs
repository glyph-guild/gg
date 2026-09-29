namespace Gg.Local;

/// <summary>An environment instance a host has, as the host can see it.</summary>
/// <remarks>
/// The local half of the wire's reading, on <see cref="MeasuredMachine"/>'s
/// terms: this project cannot reference the wire contract, so the two shapes are
/// separate by construction and the reporter is the one place they meet.
/// </remarks>
public sealed record SeenInstance
{
    /// <summary>The charted environment this instance serves.</summary>
    public required string Environment { get; init; }

    /// <summary>The instance's name, which is a UNIX user on this host.</summary>
    public required string Instance { get; init; }
}

/// <summary>
/// What environment instances this host has, read from the disk they live on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host is the only party whose word about this can be trusted, which is
/// the decision behind this file</b> (good-grief#617, owner 2026-09-29). An
/// instance is a UNIX user with its own rootless daemon, made when the host is
/// built — not a container pulled from a pin, and not a name somebody maintains
/// in a document. A list kept anywhere else is a list that drifts, and the thing
/// it drifts into is a flight handed a daemon that is not there.
/// </para>
/// <para>
/// <b>A slot says which environment it serves, in a file.</b> Nothing else on
/// the host knows: the runbook makes <c>gg-env-1</c>, <c>gg-env-2</c>, and those
/// names say nothing about <c>ui</c> or <c>api</c>. Guessing from the name would
/// be this code deciding what an operator meant. A slot with no such file is not
/// reported at all, which is the honest answer — the host has it and nobody said
/// what it is for.
/// </para>
/// <para>
/// <b>And a slot is reported only if the socket exists.</b> A slot whose daemon
/// never came up is one a flight cannot stand a stack in, and reporting it would
/// hand somebody a name that resolves to nothing. Existence is a necessary
/// condition rather than a sufficient one — a UNIX socket outlives the process
/// that bound it — so the flight's own failure remains the backstop. What this
/// removes is the case that is knowable here and cheap: a slot made and never
/// finished.
/// </para>
/// <para>
/// <b>Null and empty are different answers, deliberately.</b> Null is "this host
/// hosts no environments" — no root, which is every developer's Mac and every
/// member container — and says nothing to anybody. An empty list is a host that
/// HAS the root and has no slots in it, which is a real statement: it retires
/// whatever it used to have. Collapsing the two would make a host that lost
/// every slot indistinguishable from one that was never in this business, and
/// the slots would stay grantable for ever.
/// </para>
/// </remarks>
/// <param name="root">
/// Where instances live — <c>/srv/env</c> on a pool host, a temporary directory
/// in a test.
/// </param>
public sealed class EnvironmentSlotScan(string root)
{
    /// <summary>
    /// The file in a slot's home naming the environment it serves.
    /// </summary>
    /// <remarks>
    /// Held on both sides: <c>deploy/pool-host/environments.md</c> tells an
    /// operator to write it, and this reads it. A test compares the two rather
    /// than trusting that somebody updated the runbook.
    /// </remarks>
    public const string EnvironmentFile = "environment";

    /// <summary>The socket a slot's daemon listens on, relative to its home.</summary>
    /// <remarks>
    /// The other half of the convention <c>EnvironmentNaming</c> holds: what
    /// crosses the wire is the NAME, and where it resolves to is this host's
    /// business.
    /// </remarks>
    public const string SocketPath = "run/docker.sock";

    private readonly string _root = root ?? throw new ArgumentNullException(nameof(root));

    /// <summary>The real one, reading the pool host's own disk.</summary>
    public static EnvironmentSlotScan OfThisHost(string root) => new(root);

    /// <summary>
    /// Every instance this host has and can reach, or null when it hosts none.
    /// </summary>
    public IReadOnlyList<SeenInstance>? Read()
    {
        if (!Directory.Exists(_root))
        {
            return null;
        }

        var seen = new List<SeenInstance>();

        foreach (var home in Directory.EnumerateDirectories(_root).Order(StringComparer.Ordinal))
        {
            var instance = Path.GetFileName(home);
            var declared = Path.Combine(home, EnvironmentFile);

            if (instance.Length == 0
                || !File.Exists(declared)
                || !File.Exists(Path.Combine(home, SocketPath)))
            {
                continue;
            }

            // ONE LINE, TRIMMED. An operator writes this with `tee`, and a
            // trailing newline is what `echo` leaves; a second line would be
            // somebody answering a question this file does not ask.
            var environment = Read(declared);

            if (environment.Length > 0)
            {
                seen.Add(new SeenInstance { Environment = environment, Instance = instance });
            }
        }

        return seen;
    }

    /// <summary>
    /// The first line of a file, or empty for anything this cannot read.
    /// </summary>
    /// <remarks>
    /// A slot this process may not read is a slot it cannot report anything true
    /// about, and a scan that threw would take the whole report down over one
    /// bad directory — so an unreadable slot is an absent one, which is the same
    /// answer as a slot with nothing written in it.
    /// </remarks>
    private static string Read(string file)
    {
        try
        {
            return File.ReadLines(file).FirstOrDefault()?.Trim() ?? string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
