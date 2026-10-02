using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A destination may have conditions and no target: nothing is delivered, and
/// the flight lands when its obligations hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED ON THE DEV TENANT, 2026-10-02, twice.</b> A `preview-probe`
/// flight serves a page from a container, changes no code, and takes no
/// repository — <c>accepts: []</c>. Its destination said <c>pull-request</c>,
/// which has to be opened AGAINST a repository, so there was never anything to
/// push and the delivery step could not happen. The flight never ended: the
/// runner renewed its claim every forty seconds for ever, holding the host and
/// its environment slot out of service, until somebody grounded it. GG-711 and
/// GG-722 both.
/// </para>
/// <para>
/// <b>Every one of the six kinds names a place a thing is delivered</b>, and
/// this work delivers nothing. Its output is a running server at an address and
/// a person's verdict on it. The contract's own definition is that a destination
/// is <i>a target plus its admission conditions</i> — here the conditions are
/// real and the target is genuinely absent, and there was no way to say so.
/// </para>
/// <para>
/// <b>A DECLARED WORD RATHER THAN AN EMPTY LIST, and that is the whole of why
/// this is a seventh kind instead of relaxing the cardinality rule.</b> An
/// envelope carries exactly one destination. Letting it carry none would make
/// the absence silent — "this kind deliberately delivers nothing" and "nobody
/// has written a destination yet" would read the same, which is the one
/// distinction this contract makes everywhere else.
/// </para>
/// <para>
/// <b>It takes no knob of its own</b>, which is why nothing else moves. Every
/// optional key is already written as a sweep refusing the kinds it does not
/// belong to — branch and title to pull-request, <c>opens:</c> to flight,
/// may-write to the tracker — so a seventh kind is refused all of them by
/// construction. What it carries is <c>requires:</c>, which every kind has.
/// </para>
/// </remarks>
public class ADestinationMayDeliverNothingTests
{
    /// <summary>
    /// A valid envelope for a kind that takes nothing and changes nothing.
    /// </summary>
    /// <remarks>
    /// <c>FlightDestinationTests.Classifying</c>'s shape, for its reasons:
    /// <c>accepts: []</c> is the honest answer for work that reads no subject,
    /// and <c>scope: none</c> is what ADR-0020 section 1 requires of it. This is
    /// <c>preview-probe</c>'s own shape.
    /// </remarks>
    private static Envelope Probing(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [],
        Obligations =
        [
            new Obligation
            {
                Id = "preview-reviewed",
                Check = ObligationChecks.Human,
                Approver = "kdeenanauth",
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "serve",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "15m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    [Test]
    public async Task A_destination_that_delivers_nothing_is_a_kind_of_its_own()
    {
        await Assert.That(DestinationKinds.All).Contains(DestinationKinds.None)
            .Because("a work kind whose whole output is a running address and a person's "
                   + "verdict has nowhere to deliver to, and saying so was impossible: the "
                   + "six kinds all name a place a thing goes.");
    }

    [Test]
    public async Task An_envelope_whose_destination_delivers_nothing_is_accepted()
    {
        // THE SHAPE preview-probe NEEDS, and the one that could not be written.
        // Conditions and no target: a person reviews the preview, and when they
        // do the flight has landed, because there is nothing left for it to do.
        var refused = Envelope.Validate(Probing(new Destination
        {
            Id = "none",
            Kind = DestinationKinds.None,
            Requires = ["preview-reviewed"],
        }));

        await Assert.That(refused).IsNull()
            .Because("the conditions are real and the target is absent, which is exactly what "
                   + "this kind is for - and the refusal was the vocabulary not knowing the "
                   + "word, not anything wrong with the document.");
    }

    [Test]
    public async Task It_may_require_nothing_at_all()
    {
        // A FLIGHT THAT SIMPLY FINISHES. Nothing today expresses that either:
        // every kind delivers somewhere, so "do the work and be done" had to be
        // dressed up as a delivery. An empty `requires` is already legal on
        // every other kind and means the same thing here.
        var refused = Envelope.Validate(Probing(new Destination
        {
            Id = "none",
            Kind = DestinationKinds.None,
            Requires = [],
        }));

        await Assert.That(refused).IsNull()
            .Because("a probe nobody needs to review is a real work kind, and it lands when "
                   + "its loop ends.");
    }

    [Test]
    public async Task It_carries_none_of_the_other_kinds_knobs()
    {
        // THE POISON TWIN, and it costs nothing because every knob is already a
        // sweep. A branch belongs to a pull request, `opens:` to a flight,
        // may-write to a tracker - and a destination that delivers nothing has
        // no business with any of them. If a future key is added for this kind,
        // this is the test that has to be argued with first.
        foreach (var (what, destination) in new (string, Destination)[]
        {
            ("branch", new Destination
            {
                Id = "none", Kind = DestinationKinds.None, Requires = [], Branch = "refs/heads/x",
            }),
            ("opens", new Destination
            {
                Id = "none", Kind = DestinationKinds.None, Requires = [], Opens = ["implement"],
            }),
        })
        {
            await Assert.That(Envelope.Validate(Probing(destination))).IsNotNull()
                .Because($"'{what}' belongs to another kind, and a destination that delivers "
                       + "nothing accepting it would be this vocabulary letting a key mean "
                       + "two things.");
        }
    }
}
