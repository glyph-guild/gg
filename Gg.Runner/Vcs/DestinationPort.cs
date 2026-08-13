using Gg.Contracts;

namespace Gg.Runner.Vcs;

/// <summary>What landing one flight's work needs.</summary>
public sealed record LandingRequest
{
    /// <summary>The tree the agent worked in.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Which repository, in whatever form the provider uses.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The branch to create. Named by the control plane, which knows the flight
    /// number.
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>What the pull request opens against.</summary>
    public required string BaseRef { get; init; }

    /// <summary>What the change is called, for a person reading a list of them.</summary>
    public required string Title { get; init; }

    /// <summary>The credential the developer registered, resolved on this machine.</summary>
    public required string Secret { get; init; }
}

/// <summary>
/// What pushing produced, or why it did not.
/// </summary>
/// <remarks>
/// <b>Separate from <see cref="LandingOutcome"/> because the two gates are
/// separate.</b> A push that succeeded and a proposal that succeeded are different
/// permissions, granted on different conditions, and a single outcome type would
/// let a caller treat one as the other - which is exactly the conflation that would
/// push work a machine obligation refused.
/// </remarks>
public abstract record PushOutcome
{
    /// <summary>
    /// The branch is on the remote, at this commit.
    /// </summary>
    /// <remarks>
    /// The commit is carried because a pending decision is about a commit. A push
    /// that reported only a branch name would leave the gate pointing at whatever
    /// the branch means later.
    /// </remarks>
    public sealed record Pushed(string Branch, string Commit) : PushOutcome;

    /// <summary>
    /// The branch was already there, and this did not touch it.
    /// </summary>
    /// <remarks>
    /// <b>Not a failure, and not a success either - it is the crash-recovery
    /// case.</b> A runner that pushed, died, and came back finds its own branch. The
    /// commit is still carried, because the reference is what the gate needs and it
    /// exists whether or not this attempt wrote it.
    /// </remarks>
    public sealed record AlreadyThere(string Branch, string Commit) : PushOutcome;

    /// <summary>The remote said no, and this says what it said.</summary>
    public sealed record Refused(string Slug, string Diagnosis) : PushOutcome;

    /// <summary>There was nothing to commit, so there is nothing to push.</summary>
    public sealed record NothingToPush(string Diagnosis) : PushOutcome;
}

/// <summary>What proposing produced, or why it did not.</summary>
public abstract record LandingOutcome
{
    /// <summary>Proposed, on a branch that was already pushed.</summary>
    public sealed record Landed(string Branch, string Uri, int Number) : LandingOutcome;

    /// <summary>
    /// The branch is already there, and this did not touch it.
    /// </summary>
    /// <remarks>
    /// Named rather than counted, and never force-pushed. Fifth application of
    /// <i>never overwrite a lifecycle</i>, and the one where the thing
    /// overwritten might be somebody's work.
    /// </remarks>
    public sealed record BranchExists(string Branch) : LandingOutcome;

    /// <summary>
    /// The credential will not do it, and this says which one.
    /// </summary>
    /// <remarks>
    /// The refusal the whole two-control design rests on. An envelope declared
    /// that this flight may land somewhere; the credential is what grants the
    /// ability to, and it did not. Naming the reference is what makes this
    /// diagnosable rather than a status code with somebody else's wording on it.
    /// </remarks>
    public sealed record CredentialRefused(string Locator, string Diagnosis) : LandingOutcome;

    /// <summary>This runner cannot serve this destination at all.</summary>
    public sealed record Unsupported(string Diagnosis) : LandingOutcome;
}

/// <summary>
/// The write half, and it is a separate port on purpose.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="IVcsAdapter"/> stays read-only and its surface assertion still
/// passes.</b> Widening it would have made "the read path cannot write" an
/// argument about which methods a caller happens to use; keeping the write
/// methods behind a port of their own means the read path is still provably
/// incapable, and the existence of this interface is itself the declared
/// escalation.
/// </para>
/// <para>
/// <b>Two independent controls, and neither is sufficient.</b> An envelope
/// declares that a flight may land somewhere - permission - and a credential
/// carrying write scope grants the ability to. This port is reached only when
/// the control plane has admitted the flight, and it still fails at the
/// credential when the developer registered a read-only one.
/// </para>
/// <para>
/// Forge-neutral like the read adapter: the api base is deployment knowledge,
/// injected, and named nowhere in this binary.
/// </para>
/// </remarks>
public interface IDestinationAdapter
{
    /// <summary>The provider key this adapter answers for.</summary>
    string Provider { get; }

    /// <summary>
    /// Pushes the branch and opens the pull request, or refuses.
    /// </summary>
    /// <remarks>
    /// <b>Idempotent across the seam.</b> Push can succeed and proposal fail,
    /// and the batch that carried the admission will be retried - so proposing
    /// is keyed on the branch. A retry finds the open proposal and returns it
    /// rather than creating a second one.
    /// </remarks>
    /// <summary>
    /// Pushes the branch, and does not propose anything.
    /// </summary>
    /// <remarks>
    /// <b>The first of two gates, and a method of its own.</b> A single call that
    /// pushed and then decided whether to propose would put the gate decision inside
    /// the runner, and the runner is not an authority. Two methods means the control
    /// plane's two permissions map onto two calls that cannot be conflated.
    /// </remarks>
    Task<PushOutcome> PushAsync(LandingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proposes a change on a branch that has already been pushed.
    /// </summary>
    /// <remarks>
    /// Idempotent on the branch: a retry after a proposal failure finds the one that
    /// exists rather than opening a second.
    /// </remarks>
    Task<LandingOutcome> ProposeAsync(
        LandingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Which providers this runner may land work with, and where their apis are.
/// </summary>
/// <remarks>
/// <para>
/// <b>Write needs a second declaration, and that is the point.</b> A provider
/// appears here only if it also appears in <c>GG_VCS_HOSTS</c> - the git host is
/// where the branch goes, and this names where the proposal is asked for. A
/// runner configured to read and not to write CANNOT write: there is no adapter
/// for it to reach, so <b>no destination, no write</b> holds at the level of
/// which objects exist rather than at the level of a check somebody could
/// delete.
/// </para>
/// <para>
/// Absent entirely is the ordinary state and not a degraded one. Deployment
/// knowledge, like the hosts: the same binary runs against a public forge, a
/// self-hosted one and an air-gapped mirror, and gg names none of them.
/// </para>
/// </remarks>
public static class DestinationConfiguration
{
    /// <summary>The variable naming which providers this runner may land with.</summary>
    public const string ApisVariable = "GG_DESTINATION_APIS";

    /// <summary>The adapters this environment describes.</summary>
    /// <remarks>
    /// A key with no host in <c>GG_VCS_HOSTS</c> throws rather than being
    /// skipped. Article XI: a runner that silently landed nothing because of a
    /// typo would fail on one flight, much later, for a reason nothing connects
    /// back to a variable.
    /// </remarks>
    public static IReadOnlyList<IDestinationAdapter> FromEnvironment(
        Func<string, HttpClient> clientFor,
        string? apis = null,
        string? hosts = null)
    {
        ArgumentNullException.ThrowIfNull(clientFor);

        var declared = apis ?? Environment.GetEnvironmentVariable(ApisVariable) ?? "";
        var known = Parse(
            hosts ?? Environment.GetEnvironmentVariable(VcsConfiguration.HostsVariable) ?? "",
            VcsConfiguration.HostsVariable);

        var adapters = new List<IDestinationAdapter>();

        foreach (var (key, api) in Parse(declared, ApisVariable))
        {
            if (!known.TryGetValue(key, out var host))
            {
                throw new InvalidOperationException(
                    $"{ApisVariable} names provider '{key}' and {VcsConfiguration.HostsVariable} does "
                  + "not. Landing pushes a branch to the git host and asks the api for a pull request, "
                  + "so a destination needs both. Declare the host, or remove the destination.");
            }

            adapters.Add(new HttpsDestinationAdapter(key, host, clientFor(api)));
        }

        return adapters;
    }

    private static Dictionary<string, string> Parse(string raw, string variable)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('=', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                throw new InvalidOperationException(
                    $"{variable} entry '{entry}' is not key=value.");
            }

            var value = parts[1].EndsWith(VcsConfiguration.NoPullRequestHeads, StringComparison.Ordinal)
                ? parts[1][..^VcsConfiguration.NoPullRequestHeads.Length]
                : parts[1];

            parsed[parts[0]] = value;
        }

        return parsed;
    }
}

/// <summary>
/// Getting a branch onto a remote, and deciding what happened when that fails.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate from the destination adapter because it is git's, not a forge's.</b>
/// Whether a push fast-forwards, what a refspec without a leading <c>+</c> refuses, and
/// which commit a remote's branch points at are all answered by git and answered
/// identically for <c>https://</c> and for a bare repository on disk. Authentication and
/// pull requests are the forge's, and they stay in the adapter.
/// </para>
/// <para>
/// The seam is what lets the never-overwrite property be proven in CI against a real bare
/// repository, instead of only against a forge nobody's build can reach.
/// </para>
/// </remarks>
public static class GitPush
{
    /// <summary>
    /// Pushes the working tree's head to a branch, and never rewrites what is there.
    /// </summary>
    /// <remarks>
    /// The refspec carries no leading <c>+</c> and there is no <c>--force</c>, so a push
    /// that would not fast-forward FAILS inside git rather than being caught by a check
    /// somebody remembered to run first.
    /// </remarks>
    public static async Task<PushOutcome> PushAsync(
        string url,
        string workingDirectory,
        string branch,
        string slug,
        string? secret,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GitInvocation.Push(url, "HEAD", branch, secret)
                .RunAsync(workingDirectory, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            return await ClassifyAsync(
                url, workingDirectory, branch, slug, secret, refused.Message, cancellationToken);
        }

        return new PushOutcome.Pushed(
            branch, await HeadAsync(workingDirectory, cancellationToken));
    }

    /// <summary>Why the push was refused, asked of the remote rather than of git's wording.</summary>
    /// <remarks>
    /// A message that changes between git versions is not something a refusal should be
    /// classified by, so the remote is asked whether the branch is there instead.
    /// </remarks>
    private static async Task<PushOutcome> ClassifyAsync(
        string url,
        string workingDirectory,
        string branch,
        string slug,
        string? secret,
        string refusal,
        CancellationToken cancellationToken)
    {
        var tip = await TipAsync(url, workingDirectory, branch, secret, cancellationToken);

        if (tip is null)
        {
            return new PushOutcome.Refused(slug, refusal);
        }

        // ALREADY THERE, which is the crash-recovery case rather than a failure: a runner
        // that pushed, died and came back finds its own branch.
        return new PushOutcome.AlreadyThere(
            branch, await HeadAsync(workingDirectory, cancellationToken));
    }

    /// <summary>The commit the remote's branch points at, or null when it has none.</summary>
    private static async Task<string?> TipAsync(
        string url, string workingDirectory, string branch, string? secret,
        CancellationToken cancellationToken)
    {
        var advertised = await GitInvocation
            .LsRemote(url, branch, secret)
            .RunAsync(workingDirectory, cancellationToken);

        // "<sha>\trefs/heads/<branch>", or nothing at all.
        var first = advertised.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return first?.Split('\t', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
            is { Length: > 0 } sha ? sha : null;
    }

    /// <summary>The commit the working tree is at, which is what a push would send.</summary>
    private static async Task<string> HeadAsync(
        string workingDirectory, CancellationToken cancellationToken) =>
        (await GitInvocation.Plain("rev-parse", "HEAD")
            .RunAsync(workingDirectory, cancellationToken)).Trim();
}
