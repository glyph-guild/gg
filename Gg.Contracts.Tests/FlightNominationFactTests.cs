namespace Gg.Contracts.Tests;

/// <summary>
/// The fact a classifier produces: a work kind it nominates, and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one fact in the vocabulary an agent DECLARES rather than something
/// measured.</b> Every other family is measured from a tree, from a session or
/// from a registry; <c>handoff.account</c> is a person's own words. This is an
/// agent's request, and what it asks for is that a flight of a particular work
/// kind exist.
/// </para>
/// <para>
/// <b>Stated by construction, because a fact has no voice field.</b>
/// <c>EvidenceVoices</c> lives on <c>GateEvidenceItem</c> and nowhere else, so
/// there is no <c>Voice</c> to set here - and inventing one would be a new
/// vocabulary with a gate behind it. What marks this as a claim is the same
/// thing that marks a person's account as one: its own kind, its own envelope
/// slot, and a name that says what it is. A reader who cannot tell a nomination
/// from a measurement would read a request as a finding.
/// </para>
/// <para>
/// <b>And it decides nothing.</b> The control plane checks it against the menu
/// a person wrote on the destination, and refuses anything outside it. The fact
/// is the ask; admission is the answer.
/// </para>
/// </remarks>
public class FlightNominationFactTests
{
    private static FactEnvelope Carrying(FlightNomination nomination) => new()
    {
        IdempotencyKey = "flight-1:flight.nomination:1",
        Kind = FactKinds.FlightNomination,
        Digest = new string('a', 64),
        Nomination = nomination,
    };

    private static FlightNomination Nominating(
        string workKind = "research", string reason = "the item asks a question") =>
        new() { WorkKind = workKind, Reason = reason };

    [Test]
    public async Task The_kind_is_a_member_and_an_envelope_carrying_it_validates()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.FlightNomination);
        await Assert.That(FactEnvelope.Validate(Carrying(Nominating()))).IsNull()
            .Because("both halves, every time - a kind declared and left out of All is refused "
                   + "by the very vocabulary that declared it.");
    }

    [Test]
    public async Task It_travels_in_its_own_slot_and_only_its_own()
    {
        // EXACTLY ONE PAYLOAD. A nomination arriving beside a manifest would be
        // two facts in one envelope, and the reader could not say which kind
        // the envelope claims to be.
        var two = Carrying(Nominating()) with
        {
            Loop = new LoopOutcome
            {
                LoopId = "classify",
                Outcome = LoopOutcomes.Completed,
                Moves = [LoopMoves.Read],
                StartedAt = DateTimeOffset.UnixEpoch,
                EndedAt = DateTimeOffset.UnixEpoch,
            },
        };

        await Assert.That(FactEnvelope.Validate(two)).IsNotNull();
    }

    [Test]
    public async Task A_kind_that_says_nomination_and_carries_none_is_refused()
    {
        var empty = new FactEnvelope
        {
            IdempotencyKey = "flight-1:flight.nomination:1",
            Kind = FactKinds.FlightNomination,
            Digest = new string('a', 64),
        };

        await Assert.That(FactEnvelope.Validate(empty)).IsNotNull()
            .Because("a nomination nobody can read is a classify flight that looks like it "
                   + "answered and did not.");
    }

    [Test]
    public async Task A_nomination_names_a_work_kind_and_gives_a_reason()
    {
        foreach (var blank in (string[])["", " "])
        {
            await Assert.That(FlightNomination.Validate(Nominating(workKind: blank))).IsNotNull();
            await Assert.That(FlightNomination.Validate(Nominating(reason: blank))).IsNotNull()
                .Because("a nomination with no reason is a decision with no record of why, "
                       + "which is the thing that makes an audit trail worth reading.");
        }
    }

    [Test]
    public async Task A_reason_longer_than_the_bound_is_refused_naming_the_bound()
    {
        // MEASURED, NOT GUESSED. A real classifier's reason for a clear-cut
        // item ran about 700 characters, so the bound is roughly three times
        // what one actually needs. A reason past it is an analysis, and this
        // fact is not the place to put one.
        var refused = FlightNomination.Validate(
            Nominating(reason: new string('x', FlightNomination.MaxReason + 1)));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains($"{FlightNomination.MaxReason}");
    }

    [Test]
    public async Task The_work_kind_is_bounded_too()
    {
        await Assert.That(FlightNomination.Validate(
            Nominating(workKind: new string('k', FlightNomination.MaxWorkKind + 1)))).IsNotNull()
            .Because("a work kind is a name in a topology, and an unbounded one is a string "
                   + "somebody put a document in.");
    }

    [Test]
    public async Task It_measures_the_episode_rather_than_a_tree_or_a_subject()
    {
        // THE CATEGORY DECIDES WHETHER A SUBJECT CAN VETO IT. A classifier
        // accepts no subject and touches no tree, so filing this under Tree
        // would make it unproducible for the only kind that produces it - and
        // every rule reading it inapplicable, silently, for ever.
        await Assert.That(FactCategories.Of(FactKinds.FlightNomination))
            .IsEqualTo(FactCategories.Flight);
    }

    [Test]
    public async Task It_is_registered_everywhere_a_fact_type_has_to_be()
    {
        // The manifest is the guard that knows the full list; this asserts the
        // answer rather than restating it, so a missed registration reads as
        // "flight.nomination is unregistered" rather than as a hash moving.
        await Assert.That(FactManifest.Unregistered([typeof(FlightNomination)])).IsEmpty();
    }
}
