using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Whether a nomination opens by itself or waits for somebody.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second bound on the flight destination, beside the menu.</b>
/// <c>opens:</c> says WHICH work kinds a nomination may name; this says whether
/// one that named a permitted kind becomes a flight on its own. They are
/// different questions and a tenant answers them separately: a menu of one kind
/// that opens unattended is a narrower grant than a menu of six that a person
/// sees first.
/// </para>
/// <para>
/// <b>OPTIONAL, AND ABSENT MEANS <c>auto</c> — which is an exception this
/// vault's usual rule would refuse, so it is stated rather than defaulted
/// past.</b> The rule reached five times elsewhere is that a line which can be
/// dropped is a constraint that can be dropped silently. Here absent is not a
/// dropped constraint: it is exactly what every flight destination already
/// does, because opening on admission is the only behaviour that has ever
/// existed. <c>gated</c> is the word that ADDS a constraint, so the silence has
/// nothing to hide.
/// </para>
/// <para>
/// <b>Refused on any other kind</b>, by the rule <c>opens:</c> already has: only
/// a <c>flight</c> destination opens anything, so a mode for opening means
/// nothing anywhere else and a key that parses and does nothing is a promise
/// standing where a control was needed.
/// </para>
/// </remarks>
public class AFlightDestinationSaysHowItOpensTests
{
    private static Destination Flight(string? opensAs) => new()
    {
        Id = "open-the-flight",
        Kind = DestinationKinds.Flight,
        Requires = [],
        Opens = ["research"],
        OpensAs = opensAs,
    };

    /// <summary>A classifier whose one destination opens flights.</summary>
    private static Envelope Classifier(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [FactKinds.FlightNomination],
        Obligations = [],
        Loops =
        [
            new Loop
            {
                Id = "classify",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "10m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    [Test]
    public async Task Absent_means_auto_and_the_rule_says_so_rather_than_the_default()
    {
        // The exception stated. Absent is today's exact behaviour - every
        // flight destination that exists opens on admission - so reading it as
        // auto changes no document that anybody has written.
        await Assert.That(DestinationOpening.Of(Flight(opensAs: null)))
            .IsEqualTo(DestinationOpening.Auto);

        await Assert.That(DestinationOpening.Of(Flight(DestinationOpening.Gated)))
            .IsEqualTo(DestinationOpening.Gated);
    }

    [Test]
    public async Task Only_auto_and_gated_are_words_this_reads()
    {
        var refused = Envelope.Validate(Classifier(Flight("sometimes")));

        await Assert.That(refused).IsNotNull()
            .Because("a third word would be a mode nothing implements, and a destination that "
                   + "parses into a behaviour nobody wrote is worse than one that is refused.");
        await Assert.That(refused!).Contains("sometimes");
    }

    [Test]
    public async Task A_destination_that_opens_nothing_may_not_say_how_it_opens()
    {
        // `opens:`' own rule, one member over. A pull request opens no flight,
        // so a mode for opening one is a key that parses and does nothing -
        // which reads to an author as a control they have set.
        var refused = Envelope.Validate(Classifier(new Destination
        {
            Id = "ship-it",
            Kind = DestinationKinds.PullRequest,
            Requires = [],
            OpensAs = DestinationOpening.Gated,
        }));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(DestinationKinds.PullRequest)
            .Because("the refusal names the kind, because an author looking at it has to decide "
                   + "whether the kind is wrong or the key is.");
    }

    [Test]
    public async Task Auto_to_gated_is_a_tightening_and_gated_to_auto_is_a_widening()
    {
        // THE ASYMMETRY IS THE WHOLE POINT. Putting a person in front of an
        // opening takes reach away from the agent and needs no approval;
        // taking one out means a nomination that used to wait now becomes a
        // flight unattended, which is exactly what a reviewer must be shown.
        var tightened = EnvelopeDirection.Widening(
            Classifier(Flight(opensAs: null)),
            Classifier(Flight(DestinationOpening.Gated)));

        await Assert.That(tightened).IsNull()
            .Because("a person added in front of an opening is reach removed, and a gate for "
                   + "removing reach is how a review practice gets abandoned.");

        var widened = EnvelopeDirection.Widening(
            Classifier(Flight(DestinationOpening.Gated)),
            Classifier(Flight(DestinationOpening.Auto)));

        await Assert.That(widened).IsNotNull()
            .Because("a nomination that waited for somebody now becomes a flight without one, "
                   + "which is new reach and is exactly what a comparator exists to show.");
        await Assert.That(widened!.Field).Contains("opens-as");
    }

    [Test]
    public async Task Absent_to_gated_is_a_tightening_rather_than_a_change_of_meaning()
    {
        // The exception's consequence, asserted. Absent reads as auto, so
        // writing `gated` where there was nothing is the same tightening as
        // writing it where `auto` stood - and a comparator that called it a
        // widening would make the safest edit in the document the expensive
        // one.
        var written = EnvelopeDirection.Widening(
            Classifier(Flight(opensAs: null)),
            Classifier(Flight(DestinationOpening.Gated)));

        await Assert.That(written).IsNull();
    }
}
