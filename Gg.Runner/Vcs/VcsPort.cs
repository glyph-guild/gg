namespace Gg.Runner.Vcs;

/// <summary>One repository to put on disk, pinned to an exact ref.</summary>
public sealed record RepoTarget
{
    /// <summary>Provider key. Which providers exist is the control plane's business.</summary>
    public required string Provider { get; init; }

    /// <summary>Provider-scoped identifier, in whatever form that provider uses.</summary>
    public required string Slug { get; init; }

    /// <summary>The exact ref this flight is pinned to.</summary>
    public required string PinnedRef { get; init; }
}

/// <summary>
/// What an adapter can do, declared rather than discovered by trying.
/// </summary>
/// <remarks>
/// <para>
/// <c>refs/pull/&lt;n&gt;/head</c> is one forge's convention. Another publishes
/// merge-request heads under a different name and some publish nothing at all
/// - so ref resolution lives behind this port as a declared capability from
/// the FIRST adapter, rather than being retrofitted when the second one
/// arrives and finds the port assumed the first.
/// </para>
/// <para>
/// A repository whose pull requests cannot be fetched this way is a capability
/// gap. Discovering it at clone time would make it a network error, in a
/// stack trace, at the far end of somebody's flight.
/// </para>
/// </remarks>
public sealed record VcsCapabilities
{
    /// <summary>
    /// Whether the base repository serves pull-request heads.
    /// </summary>
    /// <remarks>
    /// The capability the whole fork story rests on. Where it holds, a fork's
    /// head is fetched from the base and the runner needs no credential for the
    /// fork at all.
    /// </remarks>
    public required bool PullRequestHeadsFromBase { get; init; }

    /// <summary>How this provider spells refs, in a sentence a person can read.</summary>
    /// <remarks>
    /// Printed in diagnostics. A capability nobody can read is a capability
    /// nobody checks.
    /// </remarks>
    public required string RefScheme { get; init; }
}

/// <summary>Where a pull request's head came from, when the adapter can say.</summary>
public sealed record ForkOrigin
{
    /// <summary>The fork's own slug, e.g. <c>someone-else/widgets</c>.</summary>
    public required string Slug { get; init; }
}

/// <summary>What a pinned ref turned into, or why it could not.</summary>
public abstract record RefResolution
{
    /// <summary>The concrete ref to fetch, and whose head it is when that is known.</summary>
    public sealed record Ref(string Value, ForkOrigin? ForkOrigin) : RefResolution;

    /// <summary>
    /// This adapter cannot serve this ref, and said so before anything was
    /// fetched.
    /// </summary>
    /// <remarks>
    /// Names the CAPABILITY as well as the sentence, so a diagnosis points at
    /// the declaration rather than at a symptom.
    /// </remarks>
    public sealed record Unsupported(string Capability, string Diagnosis) : RefResolution;
}

/// <summary>What a clone produced.</summary>
public sealed record CloneOutcome
{
    /// <summary>The commit that was actually put on disk.</summary>
    public required string HeadCommit { get; init; }

    public required int FileCount { get; init; }

    public required long Bytes { get; init; }
}

/// <summary>
/// Everything the runner asks of a version-control provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only, and there is no write method to remove.</b> Adding one is a
/// scope change rather than an implementation detail, and it fails a test over
/// this interface's surface rather than passing review.
/// </para>
/// <para>
/// Thin on purpose, for the same reason the identity port is: keep the adapter
/// thin enough that the logic worth testing sits behind it. Ref resolution and
/// the capability declaration are the logic; the clone is a subprocess.
/// </para>
/// </remarks>
public interface IVcsAdapter
{
    /// <summary>The provider key this adapter answers for.</summary>
    string Provider { get; }

    /// <summary>What it can do, before it is asked to try.</summary>
    VcsCapabilities Capabilities { get; }

    /// <summary>
    /// Turns a pinned ref into something fetchable, or refuses.
    /// </summary>
    /// <remarks>
    /// Pure and offline. This is where "a fork's head comes from the base
    /// repository" is decided, and where a provider that cannot do that says so
    /// instead of failing later.
    /// </remarks>
    RefResolution Resolve(string pinnedRef);

    /// <summary>
    /// Fetches one ref into a directory. The secret never reaches the argument list.
    /// </summary>
    Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default);
}

/// <summary>An adapter declared it cannot serve this repository.</summary>
/// <remarks>
/// Distinct from a clone failure on purpose: this one is answerable by changing
/// what the flight asks for, and a network error is not.
/// </remarks>
public sealed class VcsCapabilityException(string capability, string message) : Exception(message)
{
    /// <summary>Which declared capability was missing.</summary>
    public string Capability { get; } = capability;
}
