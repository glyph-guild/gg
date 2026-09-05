using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Flying a work item that already has a flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.5-03, and the disposition is a warning rather than a refusal.</b> Two
/// flights on one work item is legal, occasionally wanted, and usually a
/// mistake. A console that refused would be deciding something the control
/// plane allows; one that said nothing would let a person open a duplicate by
/// pressing a key twice.
/// </para>
/// <para>
/// <b>ASKED BEFORE, NOT DISCOVERED AFTER.</b> The check is a read, so it
/// happens on the way in — the same shape the browse read has, in the loop,
/// because a UI session may not make the call.
/// </para>
/// <para>
/// <b>This is what <c>FlownAsync</c> was for.</b> It has been on
/// <c>ConsoleDataReachTests</c>' exemption list naming this slice's step 4;
/// wiring it here is what takes it off, which is the removal condition that
/// list was written with.
/// </para>
/// </remarks>
public class ASecondFlightIsWarnedAboutTests
{

    private static AppState Picked() =>
        Reducer.Browsed(
            new AppState { BrowseVisible = true },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

    [Test]
    public async Task An_item_with_no_flight_yet_just_flies()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);

        var state = ConsoleLoop.FlewPicked(Picked(), actions);

        await Assert.That(actions.Asked).IsEqualTo(1)
            .Because("the question is asked every time, or the warning is a coin toss.");
        await Assert.That(actions.Flown).Count().IsEqualTo(1);
        await Assert.That(state.PendingFlight).IsNull();
    }

    [Test]
    public async Task An_item_that_already_flew_asks_before_opening_a_second()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: "gg-14 is already open for this item.");

        var state = ConsoleLoop.FlewPicked(Picked(), actions);

        await Assert.That(actions.Flown).IsEmpty()
            .Because("the whole point is that it has not happened yet.");
        await Assert.That(state.PendingFlight).IsNotNull();
        await Assert.That(state.PendingFlight!.Why).Contains("gg-14");
        await Assert.That(state.PendingFlight.Provider).IsEqualTo("a-tracker");
        await Assert.That(state.PendingFlight.Id).IsEqualTo("18398");
    }

    [Test]
    public async Task Confirming_opens_the_second_flight()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: "gg-14 is already open for this item.");

        var asked = ConsoleLoop.FlewPicked(Picked(), actions);
        var opened = ConsoleLoop.ConfirmedFlight(asked, actions);

        await Assert.That(actions.Flown).Count().IsEqualTo(1);
        await Assert.That(actions.Flown[0].Id).IsEqualTo("18398");
        await Assert.That(opened.PendingFlight).IsNull()
            .Because("the question is answered, so it stops being asked.");
    }

    [Test]
    public async Task Declining_opens_nothing_and_clears_the_question()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: "gg-14 is already open for this item.");

        var asked = ConsoleLoop.FlewPicked(Picked(), actions);
        var dropped = Reducer.FlightDeclined(asked);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(dropped.PendingFlight).IsNull();
    }

    [Test]
    public async Task Confirming_when_nothing_was_asked_does_nothing()
    {
        // A key that reaches this path with no pending question is a key that
        // would otherwise open a flight for whatever was last selected.
        var actions = new ConsoleDoubles.Records();

        var state = ConsoleLoop.ConfirmedFlight(new AppState(), actions);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(state.PendingFlight).IsNull();
    }

    [Test]
    public async Task The_question_names_the_item_it_is_about()
    {
        // A modal that says "this already has a flight" while the list scrolled
        // underneath is a modal about nothing in particular.
        var actions = new ConsoleDoubles.Records(alreadyFlown: "gg-14 is already open for this item.");

        var state = ConsoleLoop.FlewPicked(Picked(), actions);

        var shown = PaneText.Modal(state with { Mode = UiMode.ConfirmFlight });

        await Assert.That(shown).Contains("18398");
        await Assert.That(shown).Contains("a-tracker");
        await Assert.That(shown).Contains("gg-14");
    }

    [Test]
    public async Task A_console_that_cannot_check_does_not_silently_skip_the_check()
    {
        // A control plane that cannot answer is not the same as an item with no
        // flights. Treating it as "no flights" would turn an outage into
        // duplicate flights nobody meant to open.
        var actions = new ConsoleDoubles.Records(alreadyFlown:
            "This console could not check whether it has already been flown.");

        var state = ConsoleLoop.FlewPicked(Picked(), actions);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(state.PendingFlight).IsNotNull()
            .Because("unknown is asked about, not assumed away.");
    }

}
