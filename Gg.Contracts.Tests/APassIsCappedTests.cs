using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// S53.4-01. A pass may not open more flights than its destination permits,
/// and the refusal says the number.
/// </summary>
/// <remarks>
/// <para>
/// <b>A CAP, NOT A RATE</b> (rule 15). What bounds a pass is how many flights
/// it may open, not how many in a window - a pass is one act, already bounded
/// by its own wall clock, so a window would be a second ceiling on something
/// that happens once. <c>WatchBounds.CapPerPass</c> is the same member one
/// nominator over, and this is deliberately the same name: a sweep's cap and a
/// pass's cap are one idea, and two words for it would be two things to explain.
/// </para>
/// <para>
/// <b>ABSENT MEANS UNBOUNDED, which is what every destination in force says
/// today.</b> The alternative - a default of one - would silently cap every
/// classifier that exists, and a bound nobody wrote is a bound nobody can be
/// asked about.
/// </para>
/// <para>
/// <b>ON A FLIGHT DESTINATION OR NOWHERE</b>, for <c>Opens</c>' reason: only a
/// flight destination opens anything, so on any other kind the number bounds
/// nothing and is a line somebody will read as doing something.
/// </para>
/// <para>
/// <b>Raising it is a widening and lowering it is not</b>, which is
/// <c>WatchBounds</c>' arm and this one is the same shape: more flights an
/// agent may cause is more reach, and deleting the cap is the largest widening
/// the member can express because absent is unbounded.
/// </para>
/// </remarks>
public class APassIsCappedTests
{
    /// <summary>The smallest work kind that carries a destination.</summary>
    private static Envelope Carrying(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Accepts = [SubjectKinds.Repository],
        Produces = [],
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
                Id = "work",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination Flight(int? cap = null, string kind = DestinationKinds.Flight) =>
        new()
        {
            Id = "classified",
            Kind = kind,
            Requires = [],
            Opens = kind == DestinationKinds.Flight ? ["research"] : null,
            CapPerPass = cap,
        };

    [Test]
    public async Task A_flight_destination_may_cap_how_many_one_pass_opens()
    {
        await Assert.That(DestinationOpening.Refused(Flight(cap: 5))).IsNull();

        // ABSENT IS UNBOUNDED, and it is what every destination in force says.
        await Assert.That(DestinationOpening.Refused(Flight())).IsNull();
    }

    [Test]
    public async Task A_cap_on_anything_but_a_flight_destination_is_refused()
    {
        foreach (var kind in (string[])
            [DestinationKinds.PullRequest, DestinationKinds.CheckRun,
             DestinationKinds.EnvelopeChange])
        {
            var refused = DestinationOpening.Refused(Flight(cap: 5, kind: kind));

            await Assert.That(refused).IsNotNull()
                .Because($"a '{kind}' opens nothing, so a cap on it bounds nothing - and a "
                       + "line that bounds nothing is one somebody will read as doing "
                       + "something.");

            await Assert.That(refused!).Contains(kind);
        }
    }

    [Test]
    public async Task A_cap_of_nothing_is_refused_and_the_refusal_says_the_number()
    {
        foreach (var none in (int[])[0, -1, -7])
        {
            var refused = DestinationOpening.Refused(Flight(cap: none));

            await Assert.That(refused).IsNotNull()
                .Because($"a cap of {none} is a destination that opens nothing, which is what "
                       + "leaving `opens` empty already means and is refused there too.");

            await Assert.That(refused!).Contains(
                none.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Because("a refusal that did not say the number would leave an author "
                       + "guessing which line it meant.");
        }
    }

    [Test]
    public async Task A_cap_survives_the_round_trip()
    {
        // WRITTEN AND READ BACK, because a member parsed and never rendered
        // ships twice in this repository's history: `gg envelope show` after an
        // apply would quietly drop the line somebody wrote.
        var envelope = Carrying(Flight(cap: 3));

        var text = EnvelopeText.Render(envelope);

        await Assert.That(text).Contains("cap-per-pass: 3");

        var read = EnvelopeYaml.Parse(text);

        await Assert.That(read.Envelope).IsNotNull()
            .Because("this says nothing at all if what was rendered does not parse: " + read.Diagnosis);

        await Assert.That(read.Envelope!.Destinations[0].CapPerPass).IsEqualTo(3);
    }

    [Test]
    public async Task A_destination_with_no_cap_gains_no_line()
    {
        // ABSENT STAYS ABSENT, on its neighbours' terms. Rendering a default
        // would put a line into a tenant's file that nobody wrote, which is
        // what `opens-as` says in its own remark.
        var text = EnvelopeText.Render(Carrying(Flight()));

        await Assert.That(text).DoesNotContain("cap-per-pass");
    }

    [Test]
    public async Task Raising_or_removing_the_cap_is_a_widening_and_lowering_it_is_not()
    {
        var bounded = Carrying(Flight(cap: 3));

        await Assert.That(EnvelopeDirection.Widening(
            bounded, Carrying(Flight(cap: 10)))).IsNotNull()
            .Because("more flights one pass may open is more reach, whoever wrote the prompt.");

        await Assert.That(EnvelopeDirection.Widening(
            bounded, Carrying(Flight()))).IsNotNull()
            .Because("absent is unbounded, so deleting the cap raises it to every flight the "
                   + "menu can name - the largest widening this member can express.");

        await Assert.That(EnvelopeDirection.Widening(
            bounded, Carrying(Flight(cap: 1)))).IsNull()
            .Because("lowering a bound is a tightening, and a tightening needs no gate.");
    }
}
