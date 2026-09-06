namespace Gg.Contracts.Tests;

/// <summary>
/// Which stage a flight reached, read off its own narrative.
/// </summary>
/// <remarks>
/// <para>
/// <b>"The boxes are stages, not the flight's state."</b> A flight records one
/// word about how it ended and until it ends that word is <c>open</c>; how far
/// it got is a different axis entirely. Conflating them is how a halted flight
/// — stopped at Evaluated, still open — comes to read as finished.
/// </para>
/// <para>
/// <b><see cref="Reason.Family"/>'s arrangement.</b> The stage is derivable from
/// the entries and is carried anyway, because a reader should not need the kind
/// table; and a carried stage that disagrees with the entries is a lie one of
/// them must be telling, so <c>Validate</c> refuses the disagreement.
/// </para>
/// </remarks>
public class StoryStageTests
{
    private static StoryEntry An(string kind, int minute) => new()
    {
        At = new DateTimeOffset(2026, 9, 6, 12, minute, 0, TimeSpan.Zero),
        Kind = kind,
    };

    // ---- S32.1-03 ----

    [Test]
    public async Task The_stage_reached_is_the_furthest_one_any_entry_belongs_to()
    {
        var reached = FlightStoryStages.Reached(
        [
            An(StoryKinds.Created, 0),
            An(StoryKinds.LeaseGranted, 1),
            An(StoryKinds.LoopRan, 2),
        ]);

        await Assert.That(reached).IsEqualTo(FlightStages.Worked)
            .Because("the agent ran, so the flight got at least that far - and how far it "
                   + "got is not what became of it.");
    }

    [Test]
    public async Task A_carried_stage_that_disagrees_with_the_entries_is_refused()
    {
        // ONE OF THEM IS LYING AND VALIDATE DOES NOT GUESS WHICH. Carrying a
        // derivable value is a kindness to a reader; carrying a WRONG one is
        // worse than making them derive it.
        var story = AStory(FlightStages.Ended, [An(StoryKinds.Created, 0)]);

        await Assert.That(FlightStory.Validate(story)).IsNotNull()
            .Because("nothing in these entries reached Ended, so the carried stage and the "
                   + "narrative under it cannot both be true.");
    }

    [Test]
    public async Task A_stage_that_agrees_with_the_entries_validates()
    {
        var story = AStory(FlightStages.Created, [An(StoryKinds.Created, 0)]);

        await Assert.That(FlightStory.Validate(story)).IsNull();
    }

    // ---- S32.1-04 ----

    [Test]
    public async Task A_kind_that_belongs_to_no_stage_says_so_rather_than_picking_one()
    {
        // A TAKEOVER IS ORTHOGONAL TO THE SIX BOXES. A person can take a flight
        // over while it is ready, leased or evaluated, and assigning it one of
        // those would be inventing a reading. The same is true of a hold lapsing
        // and of a pool's maintenance storm.
        foreach (var kind in new[]
                 { StoryKinds.TakenOver, StoryKinds.HoldExpired, StoryKinds.PoolIncident })
        {
            await Assert.That(FlightStages.Of(kind)).IsNull()
                .Because($"'{kind}' interrupts at any stage and belongs to none of them.");
        }
    }

    [Test]
    public async Task An_interruption_does_not_move_the_stage_a_flight_reached()
    {
        var reached = FlightStoryStages.Reached(
        [
            An(StoryKinds.Created, 0),
            An(StoryKinds.TakenOver, 1),
        ]);

        await Assert.That(reached).IsEqualTo(FlightStages.Created)
            .Because("somebody taking the flight over says nothing about how far the work "
                   + "got, so a stage read off it would be read off the wrong thing.");
    }

    [Test]
    public async Task Every_other_kind_belongs_to_exactly_one_stage()
    {
        // THE SWEEP, so a kind added later must decide. A kind with no stage and
        // no argument for having none is the quiet default this whole shape is
        // against.
        var interruptions = new[]
            { StoryKinds.TakenOver, StoryKinds.HoldExpired, StoryKinds.PoolIncident };

        foreach (var kind in StoryKinds.All.Where(k => !interruptions.Contains(k)))
        {
            await Assert.That(FlightStages.Of(kind)).IsNotNull()
                .Because($"'{kind}' happens somewhere in the six stages, and which one is "
                       + "part of declaring it.");
            await Assert.That(FlightStages.All).Contains(FlightStages.Of(kind)!);
        }
    }

    private static FlightStory AStory(string stage, IReadOnlyList<StoryEntry> entries) => new()
    {
        FlightId = "019ff8aa-1111-7000-8000-000000000001",
        FlightNumber = "GG-42",
        Stage = stage,
        State = FlightStates.Open,
        Entries = entries,
    };
}
