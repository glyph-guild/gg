using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `based-on:` is a third class of key: consumed, not stored and not refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>The parser knew two classes and this is neither.</b> A key is part of the
/// document, or it is refused — <c>provenance:</c> is refused by name because the
/// composer assigns it, <i>"not yours to set."</i> A precondition is different in
/// kind: it is a claim about the STREAM that the applier states, honoured once and
/// then gone. ADR-0016 § 4 puts it exactly: asserting where you stand versus
/// asserting what you saw, and the second is what optimistic concurrency needs
/// said out loud.
/// </para>
/// <para>
/// <b>It must never reach the model, and the reason is mechanical.</b> The stored
/// form of a document is its idempotence key, its field-by-field comparison
/// decides whether an apply gates, and its bytes are what the composition digest
/// hashes. A member that changed on every pull would mint a version per document
/// per pull, divert every one of them to a human gate, and move every pin — the
/// contract-bump minting hazard slice nine closed, walking back in through a new
/// door.
/// </para>
/// </remarks>
public class ConsumedKeyTests
{
    private static string WithPrecondition(string text, string version) =>
        $"based-on: {version}\n{text}";

    [Test]
    public async Task An_envelope_states_its_precondition_and_the_model_never_sees_it()
    {
        var envelope = StrategyRoundTripTests.AnEnvelope();
        var parsed = EnvelopeYaml.Parse(
            WithPrecondition(EnvelopeText.Render(envelope), "root@v4"));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"based-on is admitted, not refused: {parsed.Diagnosis}");
        await Assert.That(parsed.BasedOn).IsEqualTo("root@v4");
        await Assert.That(EnvelopeText.Render(parsed.Envelope!)).IsEqualTo(
            EnvelopeText.Render(envelope))
            .Because("the precondition is consumed - what is left is the document, "
                   + "unchanged, or every pull-and-reapply would mint a version.");
    }

    [Test]
    public async Task A_narrowing_states_its_precondition_too()
    {
        var narrowing = new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "pci-review",
                    Check = ObligationChecks.Human,
                    Approver = "an-architect",
                },
            ],
        };

        var parsed = EnvelopeYaml.ParseNarrowing(
            WithPrecondition(EnvelopeText.Render(narrowing), "pci@v2"));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.BasedOn).IsEqualTo("pci@v2");
        await Assert.That(EnvelopeText.Render(parsed.Narrowing!)).IsEqualTo(
            EnvelopeText.Render(narrowing));
    }

    [Test]
    public async Task A_strategy_states_its_precondition_too()
    {
        var strategy = StrategyRoundTripTests.AStrategy();
        var parsed = EnvelopeYaml.ParseStrategy(
            WithPrecondition(EnvelopeText.Render(strategy), "payments-pool@v7"));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.BasedOn).IsEqualTo("payments-pool@v7");
        await Assert.That(parsed.Strategy).IsEqualTo(strategy);
    }

    [Test]
    public async Task Stating_no_precondition_is_a_document_that_states_no_precondition()
    {
        // ABSENCE MEANS NO PRECONDITION, not "any version". A hand-written file
        // has no version to have been based on, and refusing it would make the
        // tool the only way to author a document - which is the working copy
        // becoming mandatory rather than convenient.
        var parsed = EnvelopeYaml.Parse(
            EnvelopeText.Render(StrategyRoundTripTests.AnEnvelope()));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.BasedOn).IsNull();
    }

    [Test]
    public async Task The_precondition_is_not_a_member_of_any_document_model()
    {
        // THE STRUCTURAL HALF. If it ever became a member, the round trip would
        // still pass and the damage would be entirely elsewhere: a version per
        // document per pull, every apply diverted to a gate, every pin moved.
        var members = new[]
            {
                typeof(Envelope), typeof(EnvelopeNarrowing), typeof(EnvironmentStrategy),
            }
            .SelectMany(t => t.GetProperties())
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members.Any(
            m => m.Contains("BasedOn", StringComparison.OrdinalIgnoreCase))).IsFalse()
            .Because("the stored form is the idempotence key, so a precondition inside it "
                   + "would mint a version every time somebody re-applied what they pulled.");
    }

    [Test]
    public async Task The_renderer_never_writes_it_back_from_a_model()
    {
        // It is not in the model, so it cannot come out of one. This is the
        // assertion that keeps the pull-side prepend honest: the tree carries
        // the precondition because PULL puts it there, not because rendering a
        // document produces one.
        await Assert.That(EnvelopeText.Render(StrategyRoundTripTests.AnEnvelope()))
            .DoesNotContain("based-on");
    }

    [Test]
    public async Task A_pulled_file_carries_the_version_it_was_rendered_from()
    {
        // And the tree is where it comes from: pull knows the version, the
        // renderer does not, so pull writes the line.
        var root = Path.Combine(Path.GetTempPath(), $"gg-basedon-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            _ = AirspaceTree.Write(root, PullTests.Estate());

            var text = await File.ReadAllTextAsync(
                Path.Combine(root, "airspace", "narrowings", "pci.yaml"));

            await Assert.That(text).StartsWith("based-on: pci@v1\n");
            await Assert.That(EnvelopeYaml.ParseNarrowing(text).BasedOn).IsEqualTo("pci@v1");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
