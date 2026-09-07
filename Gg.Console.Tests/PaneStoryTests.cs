using Gg.Contracts.Description;
using Gg.Contracts;
using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The flight pane reads as sentences, not as a list of kinds.
/// </summary>
/// <remarks>
/// <para>
/// <b>It printed <c>entry.Kind</c> and dropped the detail entirely.</b> A person
/// reading the pane learned <i>what</i> happened — <c>lease-released</c> — and
/// never <i>what it says</i>: which runner, which disposition, what the runner
/// wrote about how its turn ended. The one line in a stopped flight's history
/// worth reading was the one thrown away.
/// </para>
/// <para>
/// <b>The wording is the contract's, in both surfaces.</b> The CLI and this pane
/// keep their own layouts by decision, and the thing that stops them describing
/// one event two ways is that neither composes prose — they both call
/// <c>FlightStory.Sentence</c>.
/// </para>
/// </remarks>
public class PaneStoryTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private static StoryEntry Entry(string kind, string[] parameters, string? said = null) => new()
    {
        At = At,
        Kind = kind,
        Stage = FlightStages.Of(kind),
        Params = parameters,
        Said = said,
    };

    private static AppState With(params StoryEntry[] entries) => new()
    {
        Story = new FlightStory
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            WorkKind = "audit",
            Stage = FlightStoryStages.Reached(entries),
            State = FlightStates.Open,
            Entries = entries,
        },
    };

    // ---- S32.4-02 ----

    [Test]
    public async Task An_entry_reads_as_a_sentence()
    {
        var text = PaneText.Flight(With(Entry(StoryKinds.LeaseGranted, ["a-runner"])));

        await Assert.That(text).Contains(
            FlightStory.Sentence(StoryKinds.LeaseGranted, ["a-runner"]))
            .Because("a bare kind tells a person what happened and never what it says, and "
                   + "which runner took the flight is the whole content of this one.");
    }

    [Test]
    public async Task What_somebody_wrote_is_shown_rather_than_dropped()
    {
        // THE LINE THAT WAS THROWN AWAY. The pane rendered the kind and dropped
        // the detail, so a halt showed as `obligation-halted` and the diagnosis -
        // the only thing that says what to do about it - went nowhere.
        var text = PaneText.Flight(With(
            Entry(StoryKinds.ObligationHalted, [], "the move bound could not be measured")));

        await Assert.That(text).Contains("the move bound could not be measured");
    }

    [Test]
    public async Task The_pane_names_the_stage_and_the_state()
    {
        var text = PaneText.Flight(With(Entry(StoryKinds.LeaseGranted, ["a-runner"])));

        await Assert.That(text).Contains(FlightStages.Leased);
        await Assert.That(text).Contains(FlightStates.Open)
            .Because("the pane showed neither, so a person could not tell a flight that is "
                   + "still going from one that finished without looking somewhere else.");
    }

    [Test]
    public async Task A_flight_with_no_story_says_so_rather_than_showing_an_empty_pane()
    {
        // AN EMPTY PANE READS AS "NOTHING HAPPENED". A read that has not answered
        // and a flight nothing has happened to are different facts, and a person
        // shown the second when the first is true stops looking.
        var text = PaneText.Flight(new AppState());

        await Assert.That(text).IsNotEmpty();
    }
}
