using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg show</c> tells the flight's story, and <c>gg log</c> stays the record.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this is built to, in the owner's words:</b> <i>"gg show tells
/// the story; log stays raw."</i> The story is a second surface and not a
/// replacement — three walks grep <c>gg log --json</c>, the console's queue reads
/// <c>lease-expired</c> off it for every flight at boot, and it is the
/// machine-readable record a support bundle needs.
/// </para>
/// <para>
/// <b>What a person could not answer before.</b> The summary carries the
/// envelope versions and the facts; it never said which stage the flight had
/// reached, always answered <c>state: unknown</c>, and could not say who was
/// holding it right now. Those are the four things somebody asks when a flight
/// stops.
/// </para>
/// <para>
/// <b>A state or a stage outside the vocabulary throws.</b> The plausible guess
/// is <c>open</c>, and guessing it would show a finished flight as one somebody
/// is still working on — the argument the flight renderer already makes about a
/// state with no published name, one field over.
/// </para>
/// </remarks>
public class StoryVerbTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private static StoryEntry Entry(string kind, params string[] parameters) => new()
    {
        At = At,
        Kind = kind,
        Stage = FlightStages.Of(kind),
        Params = parameters,
    };

    private static FlightStory AStory(
        string state = FlightStates.Open,
        Reason? waiting = null,
        Actor? heldBy = null,
        params StoryEntry[] entries) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(42),
        WorkKind = "audit",
        Stage = FlightStoryStages.Reached(entries),
        State = state,
        Waiting = waiting,
        HeldBy = heldBy,
        HeldUntil = heldBy is null ? null : At.AddMinutes(30),
        Entries = entries,
    };

    // ---- S32.4-01 ----

    [Test]
    public async Task It_names_the_stage_and_the_state()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory(
            state: FlightStates.Landed,
            entries: [
                Entry(StoryKinds.LeaseGranted, "a-runner"),
                Entry(StoryKinds.Ended, FlightStates.Landed),
            ])));

        await Assert.That(text).Contains("ended");
        await Assert.That(text).Contains("landed")
            .Because("the two are different axes - how far it got and what became of it - and a "
                   + "surface showing one is a surface a person has to guess the other from.");
    }

    [Test]
    public async Task It_says_who_has_it_right_now()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory(
            heldBy: new Actor { Kind = ActorKinds.Person, Name = "kevin" },
            entries: [Entry(StoryKinds.TakenOver, "kevin")])));

        await Assert.That(text).Contains("kevin")
            .Because("without it a reader learns somebody took the flight over an hour ago and "
                   + "cannot tell whether they still have it, which is the ambiguity the three "
                   + "takeover routes exist to remove.");
    }

    [Test]
    public async Task It_says_what_the_flight_waits_on()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory(
            waiting: Reason.For(ReasonKinds.NoRunnerAdvertises, ["linux-x64"]),
            entries: [Entry(StoryKinds.Created)])));

        await Assert.That(text).Contains(Reason.Sentence(
            ReasonKinds.NoRunnerAdvertises, ["linux-x64"]))
            .Because("the wording is the contract's, so this surface and `gg show`'s summary "
                   + "cannot word one reason two ways.");
    }

    [Test]
    public async Task It_renders_entries_as_sentences_rather_than_as_kinds()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory(
            entries: [Entry(StoryKinds.LeaseGranted, "a-runner")])));

        await Assert.That(text).Contains(
            FlightStory.Sentence(StoryKinds.LeaseGranted, ["a-runner"]));
    }

    [Test]
    public async Task A_state_it_cannot_name_throws_rather_than_guessing()
    {
        // THE PLAUSIBLE GUESS IS `open`, and it would show a finished flight as
        // one somebody is still working on. The flight renderer already makes
        // this argument about a state with no published name; this is the same
        // argument one field over.
        var story = AStory(state: "mostly-fine", entries: [Entry(StoryKinds.Created)]);

        await Assert.That(() => VerbOutput.ToText(new VerbResult.Story(story)))
            .Throws<InvalidOperationException>();
    }

    // ---- S32.4-05 ----

    [Test]
    public async Task The_log_is_unchanged()
    {
        // THE RECORD STAYS THE RECORD. Three walks grep this, the console's queue
        // reads `lease-expired` off it for every flight at boot, and a support
        // bundle carries it. The story is composed FROM it, never instead of it.
        var log = new FlightLog
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            Entries =
            [
                new FlightLogEntry
                {
                    At = At,
                    Kind = "lease-granted",
                    Detail = """{"generation":1}""",
                },
            ],
        };

        var text = VerbOutput.ToText(new VerbResult.Log(log));

        await Assert.That(text).Contains("lease-granted");
        await Assert.That(text).Contains("""{"generation":1}""")
            .Because("`gg log` prints the kind and the raw detail, and a story rendered here "
                   + "instead would take the machine-readable record away from the walks and "
                   + "the support bundle that read it.");
    }
}
