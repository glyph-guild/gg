namespace Gg.Contracts.Tests;

/// <summary>
/// Whether growing what a destination may open reads as a widening.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own file because the guard that should have covered it cannot.</b>
/// <c>EnvelopeDirection.Rules</c> is assigned from
/// <c>EnvelopeComposition.Operators</c>, so the test asserting every composed
/// field has a direction rule asserts that a dictionary contains its own keys.
/// It passes for a field the comparator never reads. That is how <c>accepts:</c>
/// came to sit in the operator table and never in the comparison — recorded in
/// <c>EnvelopeDirection</c> itself — and <c>opens:</c> would have gone the same
/// way, because the destination arms are hand-written rather than driven from
/// the table.
/// </para>
/// <para>
/// <b>And the consequence is not cosmetic.</b> <c>opens:</c> is the menu a
/// classifier may nominate from. If growing it computes as a tightening, an
/// agent's reachable set of governance regimes grows with no approver anywhere
/// near it — which is the whole failure the destination exists to prevent,
/// arriving through the door built to catch it.
/// </para>
/// </remarks>
public class OpensDirectionTests
{
    private static Envelope Doc(IReadOnlyList<string>? opens = null) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [],
        Obligations =
        [
            new Obligation { Id = "human-look", Check = ObligationChecks.Human, Approver = "lead" },
        ],
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
        Destinations =
        [
            new Destination
            {
                Id = "open-the-flight",
                Kind = DestinationKinds.Flight,
                Requires = ["human-look"],
                Opens = opens ?? ["research"],
            },
        ],
    };

    [Test]
    public async Task Two_identical_documents_are_tighter_or_equal()
    {
        // The twin. A comparator that widened on any inequality it cannot read
        // would satisfy the assertion below without reading anything.
        await Assert.That(EnvelopeDirection.Widening(Doc(), Doc())).IsNull();
    }

    [Test]
    public async Task Opening_a_kind_that_could_not_be_opened_before_widens()
    {
        var widening = EnvelopeDirection.Widening(
            Doc(opens: ["research"]), Doc(opens: ["research", "implement"]));

        await Assert.That(widening).IsNotNull()
            .Because("`implement` grants edit, write and a landing destination. A document "
                   + "that can newly nominate it has grown what an agent can reach, and "
                   + "opens intersects: it can only ever narrow.");
        await Assert.That(widening!.Field).IsEqualTo("destinations.open-the-flight.opens");
        await Assert.That(widening.Because).Contains("implement")
            .Because("the sentence names the kind that was gained, because 'this widens opens' "
                   + "sends somebody to diff two lists by hand.");
    }

    [Test]
    public async Task No_longer_opening_a_kind_is_the_tightening_it_is()
    {
        await Assert.That(EnvelopeDirection.Widening(
            Doc(opens: ["research", "implement"]), Doc(opens: ["research"]))).IsNull();
    }

    [Test]
    public async Task Opening_something_where_nothing_could_be_opened_widens()
    {
        // NULL AND EMPTY ARE THE TIGHT END, the same place `moves: []` sits.
        // Validate refuses both on a flight destination, so this pair cannot be
        // authored through the door - but the comparator must not depend on
        // another rule holding, because it is also asked about documents that
        // arrived before that rule existed.
        var widening = EnvelopeDirection.Widening(Doc(opens: []), Doc(opens: ["research"]));

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("destinations.open-the-flight.opens");
    }
}
