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
    private static string AnEnvelopeOffering(string body) =>
        $"""
        context:
          scope: "**"
          constitution: "1.0.0"
        offers:
        {body}
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - anything
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    [Test]
    public async Task A_tenants_document_carries_what_it_offers_and_renders_it_back()
    {
        var parsed = EnvelopeYaml.Parse(AnEnvelopeOffering(
            "  stun-servers: \"stun:relay.example:3478\""));

        await Assert.That(parsed.Envelope).IsNotNull()
            .Because("a document refused here offers nothing, and the assertions below would "
                   + "then be about a null.");

        await Assert.That(parsed.Envelope!.Offers.Single().Key).IsEqualTo("stun-servers");
        await Assert.That(parsed.Envelope.Offers.Single().Value)
            .IsEqualTo("stun:relay.example:3478");

        // AND BACK OUT AGAIN, because show-after-apply is what a person reads to
        // see what is in force, and a section that parses and does not render is
        // a value that disappears the first time anybody looks.
        var again = EnvelopeYaml.Parse(EnvelopeText.Render(parsed.Envelope));

        await Assert.That(again.Envelope!.Offers.Single().Value)
            .IsEqualTo("stun:relay.example:3478");
    }

    [Test]
    public async Task A_key_no_machine_would_take_is_refused_where_it_is_written()
    {
        var refused = EnvelopeYaml.Parse(AnEnvelopeOffering(
            "  gg-take-command: \"rm -rf /\""));

        await Assert.That(refused.Diagnosis).IsNotNull()
            .Because("a machine refuses an offer naming a key it does not know, so a document "
                   + "carrying one cannot do what it says - and the place to find that out is "
                   + "in front of whoever wrote it.");

        await Assert.That(refused.Diagnosis!).Contains("gg-take-command", StringComparison.Ordinal)
            .Because("naming the key is the difference between a fix and a hunt.");
    }

    [Test]
    public async Task A_secret_where_a_reference_belongs_does_not_become_a_row_anybody_can_read()
    {
        // THE OFFER IS READ BY EVERY SIGNED-IN MACHINE IN THE TENANT, which is a
        // wider audience than a profile's - so the rule `credentials` keeps is at
        // least as important here: a reference names where a secret is, and a
        // bare value IS one.
        var refused = EnvelopeYaml.Parse(AnEnvelopeOffering(
            "  intent-hosts: \"ado=https://forge.example/acme|glpat-0123456789\""));

        await Assert.That(refused.Diagnosis).IsNotNull()
            .Because("a token written here would be handed to every machine that accepts, and "
                   + "read by every one that merely asks.");
    }

    [Test]
    public async Task A_document_offering_nothing_is_ordinary()
    {
        var parsed = EnvelopeYaml.Parse(AnEnvelopeOffering("").Replace("offers:\n\n", ""));

        await Assert.That(parsed.Envelope).IsNotNull();
        await Assert.That(parsed.Envelope!.Offers).IsEmpty()
            .Because("every tenant that exists today offers nothing, and none of them should "
                   + "have to say so.");
    }
}
