using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The audience says which machines have reported they cannot resolve the credential,
/// from readings the fleet already sends.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-06, and the half of Decision 11 that is NOT blocked.</b> The slice first
/// sent this out of scope pending an owner's Article VIII call, and measuring found
/// that wrong: <c>ReadinessKinds.Credential</c> exists, a machine already reports the
/// declared credential references it cannot resolve, and <c>RunnerSummary.Lacks</c>
/// already carries <c>credential:&lt;locator&gt;</c> per machine to the Developer
/// audience. No new storage, no new argument.
/// </para>
/// <para>
/// <b>What stays out of scope is the other column</b> — a machine holding one nothing
/// declares any more. That needs an inbound report of what a machine HOLDS, and
/// <c>HeartbeatAccepted.Forget</c> carries locators only downward.
/// </para>
/// <para>
/// <b>It is the machine's own report, not a derivation</b>, and that difference is
/// worth keeping straight. Declared need comes from a document somebody wrote; this
/// comes from a machine having tried. So a machine that has never measured contributes
/// nothing here, and silence is not "it has the credential".
/// </para>
/// <para>
/// <b>Both columns at once, because they answer different questions.</b> "Who is
/// supposed to have this" is the push list. "Who has said they cannot get it" is the
/// urgency. A machine can be in one and not the other, and the pairing is what makes
/// the list worth reading before a push rather than after a failure.
/// </para>
/// </remarks>
public class AnAudienceNamesWhoCannotResolveItTests
{
    private const string Locator = "local:acme/widgets";
    private const string Other = "local:acme/other";

    private static ReadinessItem Lacking(string locator, bool met = false) =>
        new() { Kind = ReadinessKinds.Credential, Subject = locator, Met = met };

    private static RunnerSummary ARunner(
        string id, string label, string? profile = null,
        IReadOnlyList<ReadinessItem>? lacks = null) =>
        new()
        {
            RunnerId = id,
            Label = label,
            State = RunnerStates.Idle,
            Profile = profile,
            Lacks = lacks ?? [],
        };

    private static FleetProfileState AProfile(string name, params string[] credentials) =>
        new()
        {
            Name = name,
            Version = "v1",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Profile = new FleetProfile { Environment = name, Roles = [], Credentials = credentials },
        };

    [Test]
    public async Task A_machine_that_reported_it_cannot_resolve_the_credential_says_so()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev", lacks: [Lacking(Locator)])],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reported).IsTrue()
            .Because("the machine measured and said it could not get this one, which is the fact "
                   + "the fleet already sends and nothing was reading.");
    }

    [Test]
    public async Task A_machine_that_reported_nothing_about_it_does_not()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Declared).IsTrue();

        await Assert.That(audience[0].Reported).IsFalse()
            .Because("silence is not a report. A machine that has never measured has said nothing, "
                   + "and reading that as 'it has the credential' is the inference this column "
                   + "exists to avoid making.");
    }

    [Test]
    public async Task A_reading_about_a_different_credential_is_not_about_this_one()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev", lacks: [Lacking(Other)])],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reported).IsFalse()
            .Because("the subject is the locator, so a machine short of something else is not short "
                   + "of this. A match on kind alone would make every credential look missing "
                   + "whenever any one was.");
    }

    [Test]
    public async Task A_reading_of_a_different_kind_is_not_about_a_credential_at_all()
    {
        // ReadinessKinds has three members and they are not interchangeable: a machine
        // lacking an AGENT is not a machine lacking a credential, and a filter on
        // Subject alone would conflate them the day a subject spelling overlaps.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev",
                     lacks: [new ReadinessItem
                     {
                         Kind = ReadinessKinds.Agent,
                         Subject = Locator,
                         Met = false,
                     }])],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reported).IsFalse()
            .Because("the kind is checked as well as the subject, because ReadinessKinds.All is "
                   + "three things and only one of them is a credential.");
    }

    [Test]
    public async Task A_reading_that_was_MET_is_not_a_machine_that_cannot_resolve_it()
    {
        // THE ONE THAT INVERTS. ReadinessItem carries Met, so the same shape describes
        // "I checked and it is fine" and "I checked and it is not" - and reading the
        // presence of an item as a problem would report every healthy machine as short.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev", lacks: [Lacking(Locator, met: true)])],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reported).IsFalse()
            .Because("Met means it resolved. A filter that ignored it would name every machine "
                   + "that has ever measured this credential as unable to get it.");
    }

    [Test]
    public async Task A_machine_can_report_a_shortfall_in_something_no_profile_declares()
    {
        // THE TWO COLUMNS COME APART, which is why they are two. A machine that
        // measured a credential its profile no longer names is the shape the OTHER
        // half of Decision 11 is about - and it belongs in the audience, because
        // somebody is clearly expecting it to work.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev", lacks: [Lacking(Locator)])],
            [AProfile("dev", Other)]);

        await Assert.That(audience.Count).IsEqualTo(1)
            .Because("it said it cannot get this credential. Leaving it out because no document "
                   + "declares it would hide the machine most likely to be failing.");

        await Assert.That(audience[0].Declared).IsFalse()
            .Because("and the row says the need was not declared, so a person can see that the "
                   + "profile and the machine disagree.");

        await Assert.That(audience[0].Reported).IsTrue();
    }
}
