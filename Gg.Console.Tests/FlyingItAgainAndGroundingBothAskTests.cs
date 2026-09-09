using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The two things the flight modal can do to a flight both ask first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Grounding took the terminal away on one keypress.</b> `x` ended the
/// session and handed the screen to <c>$EDITOR</c> to ask for a reason — so the
/// only way out of a mistyped `x` was to write nothing and have the refusal
/// explain itself. A prompt is not a confirmation: it asks what, not whether.
/// </para>
/// <para>
/// <b>And flying it again is the same shape as opening one.</b> The console
/// cannot reproduce a flight from its summary — <c>FlightSummary</c> carries
/// the intent and no repository at all, and 48 of the 78 flights on this
/// control plane are text intents WITH a repository. So this does not fly
/// anything directly: it seeds the editor the way `n` does and lets the
/// ordinary open path decide everything else, repository included. Reproducing
/// the flight from the model would have needed a contract change to carry a
/// field the open path already has.
/// </para>
/// <para>
/// <b>`y` in both, because the console already confirms that way</b> — and the
/// confirming key is deliberately not the key that opened the question, so
/// pressing one twice in quick succession cannot answer it.
/// </para>
/// </remarks>
public class FlyingItAgainAndGroundingBothAskTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 9, 2, 30, 0, TimeSpan.Zero);

    private static AppState Showing(FlightIntent intent, UiMode mode = UiMode.FlightDetail) =>
        new()
        {
            Mode = mode,
            ActiveTab = TabId.Flights,
            FlightSelected = 0,
            Flights = new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = "01a08388-4474-7733-b566-d5c2bf369645",
                        FlightNumber = "GG-77",
                        Name = "the one on the screen",
                        Intent = intent,
                        CreatedAt = T0,
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.25.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "v6",
                        Attempts = 1,
                        Facts = [],
                    },
                ],
            },
        };

    private static FlightIntent Text(string text) =>
        new() { Kind = FlightIntentKinds.Text, Text = text };

    [Test]
    public async Task Neither_acts_on_the_flight_without_asking()
    {
        var keys = Keymap.Bindings(KeymapContext.For(Showing(Text("do the thing"))));

        await Assert.That(keys.Any(k => k.Command == Command.GroundFlight)).IsFalse()
            .Because("`x` used to ground on one keypress, and the only way back out of a "
                   + "mistyped one was to write no reason and read the refusal. Offered: "
                   + string.Join(", ", keys.Select(k => k.Command.ToString())));

        await Assert.That(keys.Any(k => k.Command == Command.AskToGround)).IsTrue();
        await Assert.That(keys.Any(k => k.Command == Command.AskToFlyAgain)).IsTrue();
    }

    [Test]
    public async Task Each_question_is_answered_by_a_key_that_did_not_ask_it()
    {
        // THE ACCIDENT THIS EXISTS TO CATCH is a key pressed twice, so the
        // answer must not be the same letter as the question. `y` is what this
        // console already confirms with.
        var ground = Keymap.Bindings(
            KeymapContext.For(Showing(Text("t"), UiMode.ConfirmGround)));

        await Assert.That(ground.Any(k => k.Key.Name == "y" && k.Command == Command.GroundFlight))
            .IsTrue();
        await Assert.That(ground.Any(k => k.Key.Name == "x")).IsFalse()
            .Because("the key that asked must not also answer.");
        await Assert.That(ground.Any(k => k.Command == Command.CloseModal)).IsTrue()
            .Because("a modal with no way out is worse than one with nothing to do, and "
                   + "escaping a confirmation is a real answer.");

        var again = Keymap.Bindings(
            KeymapContext.For(Showing(Text("t"), UiMode.ConfirmFlyAgain)));

        await Assert.That(again.Any(k => k.Key.Name == "y" && k.Command == Command.FlyAgain))
            .IsTrue();
        await Assert.That(again.Any(k => k.Key.Name == "f")).IsFalse();
        await Assert.That(again.Any(k => k.Command == Command.CloseModal)).IsTrue();
    }

    [Test]
    [Arguments("text", "count to ten", "count to ten")]
    [Arguments("uri", "https://forge.example/acme/widgets/issues/1",
               "https://forge.example/acme/widgets/issues/1")]
    public async Task The_editor_opens_on_what_the_flight_was_about(
        string kind, string value, string expected)
    {
        // RAW, AND NOT WHAT THE PANE RENDERS. The modal shows a uri wrapped in
        // backticks because that is how it reads on a screen; seeding the editor
        // with that would hand the open path something nobody would have typed.
        var intent = kind switch
        {
            "uri" => new FlightIntent { Kind = FlightIntentKinds.Uri, Uri = value },
            _ => Text(value),
        };

        await Assert.That(FlightDetails.IntentToFlyAgain(Showing(intent))).IsEqualTo(expected);
    }

    [Test]
    public async Task A_ticket_is_seeded_the_way_a_person_would_type_it()
    {
        var intent = new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "ado",
            Id = "1234",
        };

        await Assert.That(FlightDetails.IntentToFlyAgain(Showing(intent))).IsEqualTo("ado#1234");
    }

    [Test]
    public async Task Nothing_on_the_screen_seeds_nothing()
    {
        // AND THE KEY IS NOT OFFERED EITHER, but the two are derived in
        // different places and a seed invented for an empty pane is a flight
        // opened about the words "No flight is selected."
        await Assert.That(FlightDetails.IntentToFlyAgain(new AppState())).IsEmpty();
    }
}
