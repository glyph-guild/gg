using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A tenant's own document says what every machine here is offered (slice
/// forty-seven, step 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two offer channels and only one of them is a profile's.</b>
/// <c>GET /v1/configuration/offered</c> resolves what is offered to the TENANT,
/// read by any signed-in machine - which is what <c>gg config offered</c> and
/// <c>gg config accept</c> drive. A laptop can already read it. Nothing had ever
/// been written to it because nothing can be: <c>/v1/configuration</c> is a
/// governed prefix, gg declares only the read there, and the control plane may
/// serve nothing under it that gg does not declare.
/// </para>
/// <para>
/// <b>So the author is an airspace document.</b> <c>root.yaml</c> is the only
/// tenant-level one, needs no declared name, and already gates a widening -
/// which is what repointing every machine in a tenant is. Applying it is what
/// writes the offer; no route appears under the prefix gg declares only a read
/// on, and that rule is respected rather than bent.
/// </para>
/// <para>
/// <b>Only OfferableKeys, refused where it is authored.</b> A key a machine
/// would refuse is a document that cannot do what it says, and finding that out
/// at apply - in front of the person who wrote it - is the whole reason an
/// envelope is validated at all.
/// </para>
/// </remarks>
public class ATenantSaysWhatItOffersTests
{
    private static Envelope Offering(params OfferedSetting[] offers) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Offers = offers,
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    private static OfferedSetting A(string key, string value) => new() { Key = key, Value = value };

    [Test]
    public async Task What_a_tenant_offers_survives_being_written_and_read_again()
    {
        // THE ROUND TRIP IS THE PRODUCT, as the description's own test says:
        // pull renders the estate into the working copy and apply parses it
        // back, so a line that does not survive both halves is a line nobody can
        // keep - and an offer nobody can keep is a machine configured by hand.
        var rendered = EnvelopeText.Render(
            Offering(A(OfferableKeys.StunServers, "stun:relay.example:3478")));

        await Assert.That(rendered).Contains("offers:", StringComparison.Ordinal);

        var read = EnvelopeYaml.Parse(rendered);

        await Assert.That(read.Envelope).IsNotNull().Because(read.Diagnosis ?? "no diagnosis");
        await Assert.That(read.Envelope!.Offers.Single().Key)
            .IsEqualTo(OfferableKeys.StunServers);
        await Assert.That(read.Envelope.Offers.Single().Value)
            .IsEqualTo("stun:relay.example:3478");
    }

    [Test]
    public async Task A_key_no_machine_would_take_is_refused_where_it_is_written()
    {
        var refused = Envelope.Validate(Offering(A("gg-take-command", "rm -rf /")));

        await Assert.That(refused).IsNotNull()
            .Because("a machine refuses an offer naming a key it does not know, so a document "
                   + "carrying one cannot do what it says - and in front of whoever wrote it is "
                   + "the only place that is cheap to learn.");

        await Assert.That(refused!).Contains("gg-take-command", StringComparison.Ordinal)
            .Because("naming the key is the difference between a fix and a hunt.");
    }

    [Test]
    public async Task A_secret_where_a_reference_belongs_does_not_become_a_row_anybody_can_read()
    {
        // WIDER THAN A PROFILE'S. This row is read by every signed-in machine in
        // the tenant, not only those enrolled under one document, so the rule
        // `credentials` keeps matters at least as much here.
        var refused = Envelope.Validate(Offering(
            A(OfferableKeys.IntentHosts, "ado=https://forge.example/acme|glpat-0123456789")));

        await Assert.That(refused).IsNotNull()
            .Because("a token written here would be handed to every machine that accepts, and "
                   + "read by every one that merely asks.");

        await Assert.That(Envelope.Validate(Offering(
                A(OfferableKeys.IntentHosts, "ado=https://forge.example/acme|local:ticket"))))
            .IsNull()
            .Because("and the well-formed one is accepted, or the refusal above proves nothing.");
    }

    [Test]
    public async Task A_document_offering_nothing_is_ordinary()
    {
        var rendered = EnvelopeText.Render(Offering());

        await Assert.That(rendered).DoesNotContain("offers:", StringComparison.Ordinal)
            .Because("every tenant that exists offers nothing, and none of them should have to "
                   + "say so - an empty section rendered would be a diff on every estate.");

        await Assert.That(EnvelopeYaml.Parse(rendered).Envelope!.Offers).IsEmpty();
    }
}
