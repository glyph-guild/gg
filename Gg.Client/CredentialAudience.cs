using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// One machine a credential is meant to reach, and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>A LOCAL RECORD, not a contract type.</b> Nothing about this crosses a wire: it is
/// derived on a developer's machine by joining two reads the control plane already
/// serves. A contract type would be a promise about something that never leaves here.
/// </para>
/// <para>
/// <b>Declared and Reported are separate because they answer different questions.</b>
/// Declared is "a document says this machine needs it" — somebody wrote that down.
/// Reported is "this machine has tried and said it cannot get it" — the machine measured.
/// A machine can be either without the other, and the pair is what makes the list worth
/// reading before a push rather than after a failure.
/// </para>
/// </remarks>
/// <param name="Locator">The credential this row is about, which every row carries so a
/// person checking a broadcast can see both halves at once (ADR-0037 rule 5).</param>
/// <param name="Reachable">
/// Whether a push can reach it now. False for a pool member, which has no profile and
/// must be reached by its host (Decision 5), and for a machine that is offline — nothing
/// stores a credential in between, so there is no later.
/// </param>
/// <param name="Through">
/// The host that would have to pass it on, when this is a member. Null when the machine
/// is the recipient itself. A person told only "unreachable" can do nothing with it.
/// </param>
public sealed record CredentialAudienceRow(
    string RunnerId,
    string Label,
    string Locator,
    bool Declared,
    bool Reported,
    bool Reachable,
    string? Through);

/// <summary>
/// Who a credential is meant to reach, derived from what the tenant has declared and
/// what its machines have reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decisions 9 to 11.</b> A push names a credential; the fleet says who
/// needs it. The owner's framing: <i>"the fleet says who is missing what, and that report
/// IS the feature."</i>
/// </para>
/// <para>
/// <b>It needs no new endpoint, which was measured rather than assumed.</b>
/// <c>FleetProfile.Credentials</c> is the declaration and <c>RunnerSummary.Profile</c>
/// is the join; <c>RunnerSummary.Lacks</c> is the report. Both reads are declared,
/// served, and Developer-audience already. The cost taken on purpose: the derivation
/// lives here, so a control-plane endpoint computing the same audience later would be
/// the second derivation Decision 7 warns about — it would be a MOVE, not an addition.
/// </para>
/// </remarks>
public static class CredentialAudience
{
    /// <summary>
    /// The machines a credential is meant to reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PURE, AND IT TAKES NO STORE AND NO DELEGATE.</b> That is
    /// <c>CredentialRows.For</c>'s rule and this inherits it for the same reason: an
    /// audience is printed, and a builder that could be handed a store is one a later
    /// change makes resolve a credential on a render path.
    /// </para>
    /// <para>
    /// <b>A row for a machine that is present and unreachable, never silence.</b> A
    /// pool member omitted would make the list read as complete, and the thing a person
    /// would notice is that member failing a flight days later.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CredentialAudienceRow> For(
        string locator,
        IReadOnlyList<RunnerSummary> runners,
        IReadOnlyList<FleetProfileState> profiles)
    {
        ArgumentException.ThrowIfNullOrEmpty(locator);
        ArgumentNullException.ThrowIfNull(runners);
        ArgumentNullException.ThrowIfNull(profiles);

        // WHICH PROFILES DECLARE IT, once rather than per runner: reading the fleet is
        // the expensive half (a profile is the newest entry on a stream rather than a
        // row), and asking per recipient would replay every stream per machine.
        var declaring = profiles
            .Where(p => p.Profile.Credentials.Contains(locator, StringComparer.Ordinal))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var rows = new List<CredentialAudienceRow>();

        foreach (var runner in runners)
        {
            var declared = runner.Profile is { Length: > 0 } profile && declaring.Contains(profile);
            var reported = Reported(runner, locator);

            // A MEMBER'S HOST CARRIES THE DECLARATION, because the member carries none:
            // `profile` is not among the columns a member's enrolment writes, so it is
            // NULL for every one of them.
            var host = runner.HostRunnerId is { Length: > 0 } id
                ? runners.FirstOrDefault(r => string.Equals(r.RunnerId, id, StringComparison.Ordinal))
                : null;

            var throughHost = host is not null && Declares(host, declaring);

            if (!declared && !reported && !throughHost)
            {
                continue;
            }

            rows.Add(new CredentialAudienceRow(
                RunnerId: runner.RunnerId,
                Label: runner.Label,
                Locator: locator,
                Declared: declared,
                Reported: reported,

                // OFFLINE IS NOT REACHABLE AND NOT A QUEUE. Nothing stores a credential
                // in between, which is ADR-0037's problem statement, so a row that read
                // like a reachable one would promise a delivery gg cannot make.
                //
                // A MEMBER IS NOT REACHABLE EITHER, whatever its state: Decision 5's
                // host-to-member recursion is the only path to one and this slice does
                // not build it.
                Reachable: host is null
                        && !string.Equals(runner.State, RunnerStates.Offline, StringComparison.Ordinal),

                Through: host?.Label));
        }

        return rows;
    }

    private static bool Declares(RunnerSummary runner, HashSet<string> declaring) =>
        runner.Profile is { Length: > 0 } profile && declaring.Contains(profile);

    /// <summary>
    /// Whether this machine has reported that it cannot resolve the credential.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THREE THINGS HAVE TO LINE UP, and each one alone is a wrong answer.</b> The
    /// KIND, because <c>ReadinessKinds.All</c> is three and a missing agent is not a
    /// missing credential. The SUBJECT, because a machine short of something else is not
    /// short of this. And <c>Met</c> INVERTED, because the same record describes "I
    /// checked and it is fine" — so reading an item's presence as a problem would name
    /// every machine that has ever measured this credential as unable to get it.
    /// </para>
    /// <para>
    /// <b>Absence is not health.</b> A machine that has never measured contributes
    /// nothing here, and that is the honest answer rather than a reassuring one.
    /// </para>
    /// </remarks>
    private static bool Reported(RunnerSummary runner, string locator) =>
        runner.Lacks.Any(item =>
            string.Equals(item.Kind, ReadinessKinds.Credential, StringComparison.Ordinal)
         && string.Equals(item.Subject, locator, StringComparison.Ordinal)
         && !item.Met);
}
