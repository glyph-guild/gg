using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The text form carries the permission, both ways.
/// </summary>
/// <remarks>
/// <b>Here rather than beside the contract's own tests, because the parser is
/// here.</b> <c>EnvelopeText.Render</c> writes the canonical form and
/// <c>EnvelopeYaml.Parse</c> reads it, and the two live in different assemblies -
/// so the only place the round trip can be asserted is the one that can see both.
/// <para>
/// A member the writer emits and the parser refuses, or the other way round, is an
/// envelope somebody cannot save. The parser's map is CLOSED, which is what makes
/// that a build failure rather than a silently dropped knob.
/// </para>
/// </remarks>
public class PreserveUnadmittedParseTests
{
    private static Envelope Governing(bool? preserve) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "scope-respected",
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
                Discharges = ["scope-respected"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["scope-respected"],
                PreserveUnadmitted = preserve,
            },
        ],
    };

    [Test]
    public async Task An_envelope_that_declares_it_round_trips()
    {
        var text = EnvelopeText.Render(Governing(true));

        await Assert.That(text).Contains("preserve-unadmitted: true");

        var parsed = EnvelopeYaml.Parse(text);

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"the canonical form must parse: {parsed.Diagnosis}");
        await Assert.That(parsed.Envelope!.Destinations[0].PreserveUnadmitted).IsEqualTo(true);

        await Assert.That(EnvelopeText.Render(parsed.Envelope)).IsEqualTo(text)
            .Because("a second render must produce the same document, or applying what "
                   + "`envelope show` printed changes the envelope.");
    }

    [Test]
    public async Task An_envelope_that_omits_it_round_trips_unchanged()
    {
        // THE COMPATIBILITY HALF, and the one that matters most: every envelope
        // written before this member existed omits it and must come back byte for
        // byte. A writer that emitted `preserve-unadmitted: false` everywhere would
        // rewrite every tenant's document on the next show, and a diff nobody made
        // is how a review practice gets abandoned.
        var text = EnvelopeText.Render(Governing(null));

        await Assert.That(text).DoesNotContain("preserve-unadmitted");

        var parsed = EnvelopeYaml.Parse(text);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Destinations[0].PreserveUnadmitted).IsNull()
            .Because("absent has to stay absent. Reading it back as false would be the same value "
                   + "to the engine and a different document on disk.");
        await Assert.That(EnvelopeText.Render(parsed.Envelope)).IsEqualTo(text);
    }

    [Test]
    public async Task A_misspelling_is_refused_rather_than_ignored()
    {
        // The parser's map is closed, and this is what that buys: `preserve-unadmited`
        // is a governance permission somebody believes they granted and did not.
        var text = EnvelopeText.Render(Governing(true))
            .Replace("preserve-unadmitted", "preserve-unadmited", StringComparison.Ordinal);

        var parsed = EnvelopeYaml.Parse(text);

        await Assert.That(parsed.Diagnosis).IsNotNull()
            .Because("a knob that silently does nothing is worse than one that is absent: somebody "
                   + "set it and believes unadmitted work is being kept.");
    }
}
