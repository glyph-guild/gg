using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Enter on the queue opens what can be done about the row, and an approval
/// offers both answers as buttons.
/// </summary>
/// <remarks>
/// <para>
/// <b>The queue is a list of things waiting on a person, so enter should reach
/// the doing.</b> It opened the flight instead - the reading modal - and from
/// there the way to act was `esc`, then `a`, then a key off the hint line. The
/// tab exists to be worked through, and the first key anybody presses on a row
/// went to the one place that deliberately binds nothing that acts.
/// </para>
/// <para>
/// <b>The flights tab keeps its enter</b>, because that tab IS the reading one:
/// a list of every flight, opened to look at. One key means two things and the
/// tab decides which, which is the shape this console already uses for `a`.
/// </para>
/// <para>
/// <b>And an approval gets two buttons, not one.</b> Approving and going to the
/// decision page are different acts - one is the answer, the other is where the
/// question and the reason field are - and a person on the queue wants whichever
/// the row deserves without learning which letter it is.
/// </para>
/// <para>
/// <b>The safe button is first, because the first is what enter presses.</b>
/// <c>RenderModalButtons</c> marks the first as the default and focuses it, so
/// declaration order decides what a reflex does: enter, enter opens the flight,
/// and approving is something you go and point at.
/// </para>
/// </remarks>
public class TheQueuesEnterOpensWhatCanBeDoneTests
{
    private static QueueRow Row() => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = "GG-118",
        Name = "the login form loses focus",
        Reason = QueueReason.AwaitingDecision,
        Since = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
    };

    private static AppState OnTheQueue(bool waiting) => new()
    {
        ActiveTab = TabId.Queue,
        Queue = [Row()],
        SelectedRow = 0,
        Gates = waiting
            ? new GateList
            {
                Gates =
                [
                    new PendingGate
                    {
                        FlightNumber = "GG-118",
                        ObligationId = "a-human-reviews-it",
                        Approver = "somebody",
                        Branch = "gg/GG-118",
                        Because = "a person reviews what the agent wrote",
                        AwaitingSince = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
                        Attempt = 1,
                        ManifestHash = "sha256:0000",
                    },
                ],
            }
            : null,
    };

    [Test]
    public async Task Enter_on_the_queue_opens_what_can_be_done()
    {
        var context = KeymapContext.For(OnTheQueue(waiting: true));

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, context))
            .IsEqualTo(Command.ToggleFlightActions);
    }

    [Test]
    public async Task And_the_flights_tab_still_opens_the_flight()
    {
        // ASK WHY IT PASSES: moving enter for both tabs would satisfy the test
        // above and take the reading modal's own key away from the tab whose
        // whole purpose is reading.
        var context = KeymapContext.For(OnTheQueue(waiting: true) with { ActiveTab = TabId.Flights });

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, context))
            .IsEqualTo(Command.ShowFlight);
    }

    [Test]
    public async Task An_approval_offers_the_answer_and_the_decision_page()
    {
        var buttons = Keymap.Buttons(
            KeymapContext.For(OnTheQueue(waiting: true) with { Mode = UiMode.FlightActions }));

        var labels = buttons.Select(b => b.Label).ToList();

        await Assert.That(labels).Contains("Approve")
            .Because("the row is waiting on this person to say yes, and saying yes is the act "
                   + "the queue exists for.");
        await Assert.That(labels).Contains("Decide")
            .Because("and the other answer, with the question and the reason field, is one "
                   + "button rather than a letter nobody was told. Found: "
                   + string.Join(", ", labels));
        await Assert.That(labels).Contains("Open flight");
    }

    [Test]
    public async Task The_button_enter_presses_is_the_one_that_reads()
    {
        // THE ORDER IS THE SAFETY. RenderModalButtons focuses the first and
        // marks it as the default, so a person who opened this with enter and
        // pressed enter again must not have approved something.
        var buttons = Keymap.Buttons(
            KeymapContext.For(OnTheQueue(waiting: true) with { Mode = UiMode.FlightActions }));

        await Assert.That(buttons[0].Label).IsEqualTo("Open flight");
        await Assert.That(buttons[^1].Label).IsEqualTo("Approve")
            .Because("and the one that cannot be taken back is the furthest from a reflex.");
    }

    [Test]
    public async Task A_row_with_nothing_waiting_is_offered_neither()
    {
        // ARTICLE XI. A decision page about no decision says "there is no
        // decision waiting on this row any more" - which is a true sentence and
        // a button that should not have been there.
        var buttons = Keymap.Buttons(
            KeymapContext.For(OnTheQueue(waiting: false) with { Mode = UiMode.FlightActions }));

        var labels = buttons.Select(b => b.Label).ToList();

        await Assert.That(labels).DoesNotContain("Approve");
        await Assert.That(labels).DoesNotContain("Decide");
        await Assert.That(labels).Contains("Open flight")
            .Because("what is left is what is always true of a row: there is a flight behind "
                   + "it and it can be read.");
    }

    [Test]
    public async Task What_is_being_approved_is_on_the_screen_that_approves_it()
    {
        // THE CONDITION ON THE BUTTON EXISTING. Approving is decided from the
        // modal that put the question up and named the approver - that rule is
        // written into the keymap next to `d`. A button that answers a question
        // the screen never showed would be the shortcut that rule refuses, so
        // the modal carries the gate.
        var said = PaneText.Modal(OnTheQueue(waiting: true) with { Mode = UiMode.FlightActions });

        await Assert.That(said).Contains("a-human-reviews-it");
        await Assert.That(said).Contains("somebody")
            .Because("who may decide is half of whether you are the one who should.");
    }

    [Test]
    public async Task And_a_row_with_nothing_waiting_says_no_such_thing()
    {
        var said = PaneText.Modal(OnTheQueue(waiting: false) with { Mode = UiMode.FlightActions });

        await Assert.That(said).DoesNotContain("a-human-reviews-it");
        await Assert.That(said).Contains("GG-118")
            .Because("it is still about a flight, and which one is the first thing to say.");
    }
}
