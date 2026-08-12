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

/// <summary>What landing produced, or why it did not.</summary>
public abstract record LandingOutcome
{
    /// <summary>Pushed and proposed.</summary>
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
    Task<LandingOutcome> LandAsync(LandingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// How a branch is named, so a person can trace it back.
/// </summary>
/// <remarks>
/// Declared here because the control plane names the branch and the runner
/// pushes it: two derivations of one name is how a flight ends up unable to
/// find the branch it just created.
/// </remarks>
public static class DestinationBranch
{
    /// <summary>The prefix every branch this platform creates carries.</summary>
    public const string Prefix = "gg/";

    /// <summary>
    /// The branch for a flight, carrying its number.
    /// </summary>
    /// <remarks>
    /// <c>GG-42</c> is the thing a person can type and the thing that ties a
    /// branch back to a record. A name nobody can trace is a branch nobody will
    /// ever delete.
    /// </remarks>
    public static string For(string flightNumber) => Prefix + Safe(flightNumber);

    /// <summary>Whether this is a branch this platform would have created.</summary>
    public static bool IsOurs(string branch) =>
        branch is not null && branch.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// A component safe in a ref name.
    /// </summary>
    /// <remarks>
    /// The flight number comes from the control plane and is <c>GG-42</c>
    /// shaped, so this removes nothing in a healthy system. It is here because a
    /// ref name is passed to git, and git has opinions about what is in one -
    /// notably that <c>..</c> is forbidden, which is why the dot is not in the
    /// allowed set at all rather than allowed-and-then-collapsed.
    /// </remarks>
    private static string Safe(string component) =>
        new([.. component.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
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
