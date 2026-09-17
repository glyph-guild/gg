using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Which way a change to a watch goes, decided by hand and not by a table.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-05.</b> An <c>EnvelopeDirection</c> arm per member, written out —
/// because an operator in the composition table is not a direction rule, which
/// is what thirty-six learned from <c>MayPerform</c> and thirty-seven paid for
/// again with <c>opens-as</c>.
/// </para>
/// <para>
/// <b>Three arms are shared with a repository's bound and are not rewritten
/// here.</b> Reach added to <c>opens:</c>, a gate removed, and a budget raised
/// or deleted mean the same thing at both doors, so they live in
/// <c>NominationDirection</c> and the watch's comparator calls them. A copy
/// would be the second spelling ADR-0022 § 5 warns about, arriving through the
/// direction arms rather than through the validation.
/// </para>
/// <para>
/// <b>AND THE FILTER IS WHY THIS CRITERION'S FIRST WORDING WAS WRONG.</b> The
/// slice doc said <i>"loosening its filter is a widening; narrowing it is a
/// tightening"</i>, and nothing can tell those apart: a filter is a query in
/// the shape's own language, and whether one query returns a superset of
/// another is not a question this side can answer. So <b>any change to a
/// filter is a widening</b> — conservative on purpose, because the alternative
/// is a comparator that guesses and is quietly wrong in the ungated direction.
/// </para>
/// </remarks>
public class AWatchsDirectionIsWrittenByHandTests
{
    private static WatchDocument A() => AWatchDeclaresReferencesTests.AWatch();

    [Test]
    public async Task Nothing_changed_is_not_a_widening()
    {
        await Assert.That(WatchDirection.Widening(A(), A())).IsNull();
    }

    [Test]
    public async Task Reach_added_to_the_menu_is_a_widening_and_taking_it_away_is_not()
    {
        var wider = WatchDirection.Widening(
            A(),
            A() with { Nominates = AWatchDeclaresReferencesTests.ABound()
                with { Opens = ["review", "implement"] } });

        await Assert.That(wider).IsNotNull();
        await Assert.That(wider!.Field).Contains("opens");

        var narrower = WatchDirection.Widening(
            A() with { Nominates = AWatchDeclaresReferencesTests.ABound()
                with { Opens = ["review", "implement"] } },
            A());

        await Assert.That(narrower).IsNull()
            .Because("a gate for removing reach is how a review practice gets abandoned.");
    }

    [Test]
    public async Task A_gate_removed_is_reach_added()
    {
        var opened = WatchDirection.Widening(
            A() with { Nominates = AWatchDeclaresReferencesTests.ABound()
                with { OpensAs = DestinationOpening.Gated } },
            A());

        await Assert.That(opened).IsNotNull()
            .Because("a nomination that used to wait for somebody now becomes a flight "
                   + "without one - `opens-as`' own asymmetry, one nominator over.");
    }

    [Test]
    public async Task A_shorter_period_is_a_widening_because_it_is_more_sweeps()
    {
        var faster = WatchDirection.Widening(
            A(), A() with { Trigger = new WatchTrigger { Every = "5m" } });

        await Assert.That(faster).IsNotNull();
        await Assert.That(faster!.Field).Contains("trigger");

        var slower = WatchDirection.Widening(
            A(), A() with { Trigger = new WatchTrigger { Every = "24h" } });

        await Assert.That(slower).IsNull()
            .Because("sweeping less often spends less and reaches less, which is somebody "
                   + "deciding to do less rather than reach nobody granted.");
    }

    [Test]
    public async Task Any_change_to_the_filter_is_a_widening()
    {
        // THE ONE THAT CANNOT BE COMPARED, and the criterion's first wording
        // asked for the impossible. A filter is a query in the shape's own
        // language; whether one returns a superset of another is undecidable
        // here. So the conservative answer is the only sound one - a
        // comparator that guessed would be wrong in the ungated direction
        // roughly half the time, and nobody would know which half.
        var changed = WatchDirection.Widening(
            A(),
            A() with { Filter = "SELECT [System.Id] FROM WorkItems" });

        await Assert.That(changed).IsNotNull();
        await Assert.That(changed!.Field).Contains("filter");

        await Assert.That(changed.Because).Contains("cannot")
            .Because("the sentence has to say WHY this took a gate, or an author narrowing a "
                   + "filter and being sent to a reviewer will read it as a bug and work "
                   + "around it.");
    }

    [Test]
    public async Task A_cap_raised_or_removed_is_a_widening_and_lowering_it_is_not()
    {
        var bounded = A() with
        {
            Bounds = new WatchBounds { CapPerPass = 10 },
        };

        await Assert.That(WatchDirection.Widening(
            bounded, bounded with { Bounds = new WatchBounds { CapPerPass = 100 } }))
            .IsNotNull();

        await Assert.That(WatchDirection.Widening(bounded, A()))
            .IsNotNull()
            .Because("absent is unbounded, so deleting a cap raises it to every subject the "
                   + "shape has - the largest widening the member can express.");

        await Assert.That(WatchDirection.Widening(
            bounded, bounded with { Bounds = new WatchBounds { CapPerPass = 1 } }))
            .IsNull();
    }

    [Test]
    public async Task Longer_active_hours_are_a_widening_and_removing_them_is_the_most()
    {
        var windowed = A() with
        {
            Bounds = new WatchBounds { ActiveHours = "09:00-17:00Z" },
        };

        await Assert.That(WatchDirection.Widening(
            windowed, windowed with { Bounds = new WatchBounds { ActiveHours = "06:00-22:00Z" } }))
            .IsNotNull();

        await Assert.That(WatchDirection.Widening(windowed, A())).IsNotNull()
            .Because("absent means always, so deleting the window is the whole day.");

        await Assert.That(WatchDirection.Widening(
            windowed, windowed with { Bounds = new WatchBounds { ActiveHours = "10:00-16:00Z" } }))
            .IsNull();
    }

    [Test]
    public async Task A_budget_raised_is_a_widening_through_the_shared_arm()
    {
        // NOT REWRITTEN HERE. The same comparison a repository's budget gets,
        // including the rate arithmetic that makes five-a-day and five-a-week
        // five times apart rather than equal - asserted so a copy of it
        // appearing in this class would have to justify itself.
        var five = A() with
        {
            Bounds = new WatchBounds
            {
                Budget = new NominationBudget { Flights = 5, Window = "24h" },
            },
        };

        var fifty = five with
        {
            Bounds = new WatchBounds
            {
                Budget = new NominationBudget { Flights = 50, Window = "24h" },
            },
        };

        await Assert.That(WatchDirection.Widening(five, fifty)).IsNotNull();
        await Assert.That(WatchDirection.Widening(fifty, five)).IsNull();

        await Assert.That(WatchDirection.Widening(five, A())).IsNotNull()
            .Because("absent means unbounded, so removing a budget is the largest widening "
                   + "this member can express - the arm a comparator gets wrong by omission.");
    }

    [Test]
    public async Task The_shared_arms_are_shared_rather_than_copied()
    {
        // STRUCTURAL, because "we reused it" is a claim a comment can make and
        // a copy can pass. A repository's comparator and a watch's must reach
        // the same method for the three arms they have in common, or the two
        // will disagree the day one of them gains a member - which is exactly
        // what happened to `MayPerform` and to `opens-as`.
        var shared = typeof(NominationDirection)
            .GetMethods(System.Reflection.BindingFlags.Public
                      | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .ToList();

        await Assert.That(shared).Contains(nameof(NominationDirection.BoundWidening));
        await Assert.That(shared).Contains(nameof(NominationDirection.BudgetWidening));
    }
}
