using Gg.Contracts.Authoring;
namespace Gg.Contracts.Tests;

/// <summary>
/// A fleet profile says which trackers its machines read work items from and
/// which they write them to (slice forty-seven, step 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two lists, because the two are keyed differently.</b> A read is keyed by
/// the provider a ticket names - <c>intent-hosts</c>' spelling - and a write by
/// the destination id an envelope names, which is <c>tracker-apis</c>'.
/// <c>TrackerConfiguration</c> says that separation is deliberate, so a single
/// list would have to invent which one an entry meant.
/// </para>
/// <para>
/// <b>What it is worth.</b> vmlinux001 writes work items because somebody typed
/// <c>tracker-apis</c> into its configuration by hand; vmlinux002 and vmlinux003,
/// both enrolled under the same profile, have neither - so a ticket flight on
/// them reaches an agent with no tool able to read the ticket, which is the gap
/// <c>IntentConfiguration</c> was written for and nothing has ever filled.
/// </para>
/// </remarks>
public class AProfileCarriesATrackerTests
{
    private const string AReadTracker = "ado=https://forge.example/acme/widgets|local:acme/widgets";
    private const string AWriteTracker = "backlog=https://forge.example/acme/widgets|local:acme/widgets";

    private static FleetProfile AProfile() => new()
    {
        Roles = [ProfileRoles.Run],
        Environment = "dev",
        Trackers = [AReadTracker],
        Triage = [AWriteTracker],
    };

    [Test]
    public async Task A_profile_round_trips_both_tracker_lists()
    {
        var rendered = EnvelopeText.Render(AProfile());
        var parsed = EnvelopeYaml.ParseProfile(rendered);

        await Assert.That(parsed.Profile).IsNotNull()
            .Because("rendered by one half and refused by the other is the round trip failing, "
                   + "and the lists below would then be asserting about nothing.");

        var read = parsed.Profile!;

        await Assert.That(read.Trackers).IsEquivalentTo((string[])[AReadTracker])
            .Because("a profile a person edits and applies is the same profile that comes back, "
                   + "or what they read is not what is in force.");
        await Assert.That(read.Triage).IsEquivalentTo((string[])[AWriteTracker]);
    }

    [Test]
    public async Task A_secret_where_a_reference_belongs_is_refused()
    {
        // THE ONE MISTAKE THIS MUST NEVER ALLOW is a token written into a
        // document the whole tenant can read - `credentials`' own sentence, and
        // a tracker entry carries its reference inline, which is the first place
        // somebody will be tempted to put one.
        var secret = FleetProfile.Validate(
            AProfile() with { Trackers = ["ado=https://forge.example/acme|glpat-0123456789"] });

        await Assert.That(secret).IsNotNull()
            .Because("a reference names where a secret is; a bare value IS one.");

        var write = FleetProfile.Validate(
            AProfile() with { Triage = ["backlog=https://forge.example/acme|glpat-0123456789"] });

        await Assert.That(write).IsNotNull().Because("the write list is refused the same way.");
    }

    [Test]
    public async Task A_tracker_with_no_credential_at_all_is_refused()
    {
        // NOT A TRACKER YET. A machine told where to write with nothing to open
        // it is the state rule 2 exists for at RUN time; at AUTHORING time it is
        // simply an incomplete document, and refusing it here is cheaper than a
        // bring-up gate explaining it later.
        await Assert.That(FleetProfile.Validate(
            AProfile() with { Trackers = ["ado=https://forge.example/acme"] })).IsNotNull();

        await Assert.That(FleetProfile.Validate(
            AProfile() with { Triage = ["backlog=https://forge.example/acme"] })).IsNotNull();

        await Assert.That(FleetProfile.Validate(AProfile())).IsNull()
            .Because("and the well-formed one is accepted, or the two above prove nothing.");
    }

    [Test]
    public async Task Adding_to_either_list_widens_and_removing_does_not()
    {
        // READING A TRACKER WIDENS AS MUCH AS WRITING ONE: it points a machine at
        // a host and hands it a credential to open it. Neither is stun-servers,
        // which is data that grants nothing.
        var bare = AProfile() with { Trackers = [], Triage = [] };

        await Assert.That(FleetProfile.Widening(bare, AProfile() with { Triage = [] })?.Field)
            .IsEqualTo("trackers");

        await Assert.That(FleetProfile.Widening(bare, AProfile() with { Trackers = [] })?.Field)
            .IsEqualTo("triage");

        await Assert.That(FleetProfile.Widening(AProfile(), bare)).IsNull()
            .Because("taking one away is a tightening, and a tightening applies at once.");
    }
}
