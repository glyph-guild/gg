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
    /// <param name="adapterFor">
    /// How to build an adapter for a provider key, given its git host and its
    /// api client. Omitted, the DECLARATION chooses:
    /// <see cref="RefNamedDestinationAdapter"/> for a host declared
    /// <c>!pathscoped</c>, <see cref="HttpsDestinationAdapter"/> otherwise.
    /// <para>
    /// <b>The seam exists because the limit was already written down.</b>
    /// <c>HttpsDestinationAdapter</c> says its path shapes are <i>one
    /// convention</i> and that a provider spelling them differently is a second
    /// adapter — and until this parameter, such an adapter could be dispatched
    /// to and never REGISTERED, because this method named the class it built.
    /// </para>
    /// <para>
    /// <b>Then the parameter was never passed outside a test, which is the same
    /// bug one layer up.</b> So the choice moved into the default, where the
    /// caller that matters reaches it: <c>Gg.Cli</c> calls this without a
    /// factory. This parameter is now what it always claimed to be — a way for
    /// a test to substitute — rather than the only route to half the adapters.
    /// </para>
    /// </param>
    public static IReadOnlyList<IDestinationAdapter> FromEnvironment(
        Func<string, HttpClient> clientFor,
        string? apis = null,
        string? hosts = null,
        Func<string, string, HttpClient, IDestinationAdapter>? adapterFor = null)
    {
        ArgumentNullException.ThrowIfNull(clientFor);

        var declared = apis ?? Environment.GetEnvironmentVariable(ApisVariable) ?? "";
        var known = ParseHosts(
            hosts ?? Environment.GetEnvironmentVariable(VcsConfiguration.HostsVariable) ?? "");

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

            // THE SAME DECLARATION, on the other side of it. One forge's
            // spelling differs in two places - the clone url it takes and the
            // proposal it accepts - and both belong to one fact. A deployment
            // made to state that fact twice would eventually state it once.
            var build = adapterFor ?? (host.IsPathScoped
                ? static (provider, forgeHost, client) =>
                    (IDestinationAdapter)new RefNamedDestinationAdapter(provider, forgeHost, client)
                : static (provider, forgeHost, client) =>
                    new HttpsDestinationAdapter(provider, forgeHost, client));

            adapters.Add(build(key, host.Host, clientFor(api)));
        }

        return adapters;
    }

    /// <summary>
    /// The api declaration, whose values are urls and carry no suffixes.
    /// </summary>
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

            parsed[parts[0]] = parts[1];
        }

        return parsed;
    }

    /// <summary>
    /// The host declaration, parsed by the type that owns its spelling.
    /// </summary>
    /// <remarks>
    /// This used to be the same method as <see cref="Parse"/>, with its own copy
    /// of the suffix stripping. Sharing one parser with the reading side is what
    /// stops a suffix understood there from reaching a url from here.
    /// </remarks>
    private static Dictionary<string, HostDeclaration> ParseHosts(string raw) =>
        HostDeclaration
            .ParseAll(raw, VcsConfiguration.HostsVariable)
            .ToDictionary(declared => declared.Key, StringComparer.Ordinal);
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

    /// <summary>
    /// Why the push was refused, asked of the remote rather than of git's wording.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Told apart by where the remote's branch IS, not by whether it exists.</b> An
    /// earlier version asked only whether the branch was there and, if it was, reported
    /// the LOCAL head as already pushed. On a second attempt the branch always exists, so
    /// a branch somebody else had moved was reported as a successful push carrying a
    /// commit that is not on the remote at all - and <c>destination.pushed</c> would
    /// record it, putting a person in front of work they cannot fetch.
    /// </para>
    /// <para>
    /// A message that changes between git versions is still not something a refusal is
    /// classified by.
    /// </para>
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
            // The branch is not there, so nothing about fast-forwarding explains this.
            // Whatever git said stands - most often a credential that cannot write.
            return new PushOutcome.Refused(slug, refusal);
        }

        var head = await HeadAsync(workingDirectory, cancellationToken);

        if (string.Equals(tip, head, StringComparison.Ordinal))
        {
            // ALREADY THERE, and this is the only shape that deserves the name: the
            // remote is at the exact commit this tree is at. A runner that pushed and
            // died before recording it comes back to this.
            return new PushOutcome.AlreadyThere(branch, tip);
        }

        if (await IsAncestorAsync(workingDirectory, tip, head, cancellationToken))
        {
            // It would have fast-forwarded, so the refusal is about something else and
            // git's own words are the honest answer.
            return new PushOutcome.Refused(slug, refusal);
        }

        // THE BRANCH MOVED. Somebody pushed to it between attempts - a developer fixing
        // it themselves is a real case, not a hypothetical - and this attempt does not
        // build on what they wrote. The two ways forward are to rewrite their work or to
        // stop, and this system does not rewrite anything in a customer's repository.
        //
        // Says what it FOUND rather than what it wanted: "push failed" sends somebody to
        // look at their credential, and this is not that.
        return new PushOutcome.Refused(
            slug,
            $"the branch '{branch}' has moved since this flight last pushed. It is at "
          + $"{Short(tip)}, this attempt builds on {Short(head)}, and that commit is not on "
          + "it - so this push is refusing to rewrite the branch. The commits already there "
          + "are somebody's work: fetch the branch and continue from where it is now, or "
          + "ground this flight and fly a new one.");
    }

    /// <summary>Whether the remote's tip is already in this tree's history.</summary>
    /// <remarks>
    /// Answered by git, and answerable at all only because the tip was just advertised to
    /// us. A shallow clone may not hold the object, in which case git says no - which is
    /// the safe answer, because not knowing that a fast-forward is safe is not knowing.
    /// </remarks>
    private static async Task<bool> IsAncestorAsync(
        string workingDirectory, string tip, string head, CancellationToken cancellationToken)
    {
        try
        {
            await GitInvocation.Plain("merge-base", "--is-ancestor", tip, head)
                .RunAsync(workingDirectory, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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

    private static string Short(string commit) => commit[..Math.Min(7, commit.Length)];
}
