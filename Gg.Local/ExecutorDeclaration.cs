namespace Gg.Local;

/// <summary>
/// Which agent this machine has, and where its binary is — one entry, parsed
/// one way for everybody who reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bare path names no agent, and four readers assumed one.</b> The
/// executor, the allowance meter's refresh, the hand-flight's refusal and the
/// doctor each took <c>GG_EXECUTOR_BINARY</c> as a path and each assumed it was
/// <c>claude</c>, silently. An adapter for a second CLI tool had nowhere to be
/// chosen, and a login adapter for the first had nowhere to learn which tool it
/// was for. The declaration says: <c>claude=/usr/local/bin/claude</c>.
/// </para>
/// <para>
/// <b>The bare form is kept, and it means claude.</b> Every unit on every host
/// today is written as a path, and a reader that refused it would be a deploy
/// breaking every runner that exists — the shape this repository names its
/// worst. The assumption is not removed; it is written down in one place,
/// where a reader of <see cref="Agent"/> can see it was made.
/// </para>
/// <para>
/// <b><c>HostDeclaration</c>'s shape, down to the refusal.</b> A malformed entry
/// is refused with a diagnosis naming the variable and the entry, never
/// sanitised into something that runs. And an agent nobody has an adapter for
/// is refused too, rather than falling back to claude: a fallback there would be
/// the silent assumption coming back through the door it was shown out of.
/// </para>
/// <para>
/// <b>Here rather than in <c>Gg.Runner</c>, because <c>Gg.Client</c> cannot see
/// that project.</b> Both see this one. A parser in each would be <i>"the
/// two-computations problem this file's own history records"</i>
/// (<c>HostDeclaration</c>), one variable over — the doctor and the runner
/// reading one entry and reaching two answers.
/// </para>
/// </remarks>
/// <param name="Agent">Which adapter: one of <see cref="Known"/>.</param>
/// <param name="Binary">Where that agent's binary is on this machine.</param>
public sealed record ExecutorDeclaration(string Agent, string Binary)
{
    /// <summary>The variable that carries the declaration.</summary>
    public const string Variable = "GG_EXECUTOR_BINARY";

    /// <summary>The agent a bare path has always meant.</summary>
    public const string Claude = "claude";

    /// <summary>Every agent this build has an adapter for.</summary>
    /// <remarks>
    /// One, and the list exists so that the second is a line here rather than
    /// a search for every place the first was assumed.
    /// </remarks>
    public static IReadOnlyList<string> Known { get; } = [Claude];

    /// <summary>
    /// Reads one declaration, or refuses it with a diagnosis naming the variable.
    /// </summary>
    public static ExecutorDeclaration Parse(string? entry, string variable)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            throw new InvalidOperationException(
                $"{variable} is empty. Set it to the agent binary this machine invokes - a path, "
              + $"or '{Claude}=<path>'. Known agents: {string.Join(", ", Known)}.");
        }

        // A BARE PATH, which is every deployment written before agents had
        // names. On any platform gg runs on, a path with an '=' in it is a path
        // nobody has, so the split is safe to attempt first.
        var split = entry.IndexOf('=', StringComparison.Ordinal);
        if (split < 0)
        {
            return new ExecutorDeclaration(Claude, entry);
        }

        var agent = entry[..split];
        var binary = entry[(split + 1)..];

        if (agent.Length == 0 || binary.Length == 0)
        {
            throw new InvalidOperationException(
                $"{variable} entry '{entry}' is not agent=path. Expected one like "
              + $"'{Claude}=/usr/local/bin/{Claude}', or a bare path, which means {Claude}.");
        }

        if (!Known.Contains(agent, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{variable} names an agent this gg has no adapter for: '{agent}' in "
              + $"'{entry}'. Known agents: {string.Join(", ", Known)}.");
        }

        return new ExecutorDeclaration(agent, binary);
    }

    /// <summary>The declaration in force, or null when the machine declares no agent.</summary>
    /// <remarks>
    /// Null is the ordinary state for most machines and not a degraded one -
    /// <c>ExecutorConfiguration</c>'s rule, kept here so both readers of an
    /// absent variable agree it is absent rather than malformed.
    /// </remarks>
    public static ExecutorDeclaration? ParseOrNull(string? entry, string variable) =>
        string.IsNullOrWhiteSpace(entry) ? null : Parse(entry, variable);
}
