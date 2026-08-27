using Gg.Contracts.Authoring;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The envelope-change envelope, through the text form a person actually writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorable, not merely representable.</b> The contract can hold this shape;
/// that is a different claim from a person being able to write it. The parser
/// lives here and only here - every YAML library is a package reference and the
/// control plane holds none - so this is the only place the claim can be tested.
/// </para>
/// <para>
/// <b>An empty <c>moves</c> is the risk.</b> It is exactly the sequence an
/// emitter drops and a parser refuses, and the human rung is the first loop that
/// is REQUIRED to have one - so a shape that governs the tenant's own rules would
/// be the one shape nobody can author.
/// </para>
/// </remarks>
public class EnvelopeChangeParseTests
{
    private static Envelope Change() => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "envelope-change-approved",
                Check = ObligationChecks.Human,
                Approver = "platform-oncall",
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "propose",
                Executor = ExecutorRungs.Human,
                Discharges = [],
                Moves = [],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "the-envelope",
                Kind = DestinationKinds.EnvelopeChange,
                Requires = ["envelope-change-approved"],
            },
        ],
    };

    [Test]
    public async Task It_round_trips_through_the_text_form()
    {
        var parsed = EnvelopeYaml.Parse(EnvelopeText.Render(Change()));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("a shape the emitter writes and the parser refuses is a document nobody "
                   + "can edit twice.");
        await Assert.That(parsed.Envelope!.Loops[0].Executor).IsEqualTo(ExecutorRungs.Human);
        await Assert.That(parsed.Envelope.Loops[0].Moves).IsEmpty();
        await Assert.That(parsed.Envelope.Destinations[0].Kind)
            .IsEqualTo(DestinationKinds.EnvelopeChange);
    }

    [Test]
    public async Task A_human_loop_that_declares_a_move_is_refused_by_the_parser_too()
    {
        // THE SAME RULE ON THE SIDE THAT AUTHORS. Refusing it only at the control
        // plane would mean an author finds out after the document left their
        // machine, which is the round trip this parser exists to shorten.
        var parsed = EnvelopeYaml.Parse(EnvelopeText.Render(Change() with
        {
            Loops = [Change().Loops[0] with { Moves = [LoopMoves.Edit] }],
        }));

        await Assert.That(parsed.Diagnosis!).Contains($"'{LoopMoves.Edit}'");
    }
}
