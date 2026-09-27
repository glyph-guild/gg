using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The live view is not a tab in this console.
/// </summary>
/// <remarks>
/// <para>
/// <b>Removed because it was a place a person was SENT to.</b> Watching a
/// runner closed the modal that asked for it and switched tabs, so seeing more
/// of what you were reading cost you what you were reading. The flight modal -
/// where somebody actually is when they want to know how a flight is going -
/// could not watch at all.
/// </para>
/// <para>
/// <b>And a permanent seat was the wrong shape for it.</b> The console's own
/// rule calls the live view "a trust artifact meant to decay": something people
/// need while they are learning to trust a runner and stop opening once they
/// do. A tab that is always on the bar is the opposite of decaying, and
/// <c>LiveVisible</c> defaulting to false was a weaker way of saying it than
/// not being there at all.
/// </para>
/// <para>
/// <b>What is NOT removed, and this is the point of the test.</b> Every line of
/// the machinery survives: <c>LiveTail</c> tailing a file, <c>LiveTails</c>
/// folding it into the state, <c>RemoteLiveSource</c> buffering a watched
/// machine, <c>ConsoleWatchRunner</c> reaching one, and <c>PaneText.Live</c>
/// rendering all of it. Only the TAB is gone; the output moved into
/// <see cref="UiMode.Watching"/>, which is a modal over the thing it is about.
/// </para>
/// </remarks>
public class TheLiveTabIsGoneTests
{
    [Test]
    public async Task No_tab_on_the_bar_is_the_live_view()
    {
        var named = Tabs.All.Select(Tabs.Name).ToList();

        await Assert.That(named).DoesNotContain("live", StringComparer.OrdinalIgnoreCase)
            .Because("the bar's job is to say what there is, so a tab left on it is a "
                   + "promise the console no longer keeps.");

        // THE BAR IS STILL A BAR. Without this the assertion above is satisfied
        // by a console that has no tabs at all.
        await Assert.That(named).Contains("queue");
        await Assert.That(named.Count).IsGreaterThanOrEqualTo(6)
            .Because($"one tab went, not the bar. Saw [{string.Join(", ", named)}]");
    }

    [Test]
    public async Task Nothing_answers_the_key_the_live_view_had()
    {
        // l WAS THE LIVE VIEW'S. A key left bound to a view that no longer
        // exists is the dead-key shape this console has paid for before; a key
        // left FREE is the next feature's to take.
        var bound = Tabs.All
            .Select(Tabs.KeyFor)
            .Where(stroke => stroke is not null)
            .Select(stroke => stroke!.Value.Name)
            .ToList();

        await Assert.That(bound).DoesNotContain("l")
            .Because("the tab it opened is gone, so the key opens nothing.");

        // Anchor: the sweep is reading real bindings, not an empty list.
        await Assert.That(bound).Contains("u")
            .Because("runners keeps its key, so this is a live reading of the bar.");
    }

    [Test]
    public async Task And_neither_the_tab_nor_its_command_is_still_declared()
    {
        // NOT JUST OFF THE BAR. A TabId nothing draws and a Command nothing
        // dispatches are both things the next person has to read past and
        // decide about, and the switches over them are exhaustive - so an arm
        // left behind is an arm somebody maintains for a view that is gone.
        await Assert.That(Enum.GetNames<TabId>()).DoesNotContain("Live");
        await Assert.That(Enum.GetNames<Command>()).DoesNotContain("ToggleLive");

        await Assert.That(Enum.GetNames<TabId>()).Contains("Queue")
            .Because("the enum is being read, not an empty array.");
    }

    // ---- the line this removal does not cross ----

    [Test]
    public async Task The_tail_the_watch_reads_is_untouched()
    {
        // WHAT MOVED IS THE BOX. LiveTails is the only thing in this console
        // that opens a file, it holds the offset that has to survive a session
        // rebuild, and the watch modal is drawn from exactly what it folds in.
        // A removal that took it would have taken the feature, not the tab.
        await Assert.That(ConsoleSource.Text("Gg.Console", "LiveTails.cs"))
            .Contains("WatchedFlightId")
            .Because("the modal names the flight it is watching, and this is what reads "
                   + "that name and decides which tail to advance.");

        await Assert.That(ConsoleSource.Text("Gg.Console", "ConsoleWatchRunner.cs"))
            .Contains("UiMode.Watching")
            .Because("reaching another machine is the half a UI session may not do, and it "
                   + "still lands somewhere - now a modal rather than a tab.");
    }

    [Test]
    public async Task And_the_output_still_reaches_a_person()
    {
        // THE REMOVAL IS ONLY HONEST IF THE WORDS STILL ARRIVE. Without this
        // the three assertions above are satisfied by a console that deleted
        // the tab and shows the output nowhere.
        var watching = new AppState
        {
            Mode = UiMode.Watching,
            LiveVisible = true,
            Live =
            [
                new StreamLine
                {
                    Kind = StreamLineKind.Text,
                    Text = "the runner is still talking",
                    At = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero),
                },
            ],
        };

        await Assert.That(PaneText.Modal(watching)).Contains("the runner is still talking");
    }

    [Test]
    public async Task And_watching_is_still_counted()
    {
        // THE TELEMETRY FOLLOWED THE FEATURE. AttachFacts counted a person
        // watching a flight, and it was written when the queue cursor moved
        // while the pane was open. The cursor cannot move under a modal, so
        // left where it was this would have become a field nothing writes -
        // which is what WatchedFlightId already was.
        var state = new AppState
        {
            Mode = UiMode.FlightDetail,
            Flights = new FlightList { Flights = [AFlight()] },
            FlightSelected = 0,
        };

        var watched = Reducer.Reduce(state, Command.WatchThisFlight);

        await Assert.That(watched.AttachFacts.Select(f => f.FlightId))
            .Contains("flight-counted")
            .Because("opening the watch on a flight IS the attach, and it is a truer one "
                   + "than a cursor passing over a row with a pane open.");
    }

    private static Gg.Contracts.FlightSummary AFlight() => new()
    {
        FlightId = "flight-counted",
        FlightNumber = "GG-9",
        Name = "the watched one",
        Intent = new Gg.Contracts.FlightIntent
        {
            Kind = Gg.Contracts.FlightIntentKinds.Text,
            Text = "do it",
        },
        CreatedAt = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };
}
