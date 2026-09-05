using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Why this console cannot take a flight over, said about the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>TWO SENTENCES DESCRIBING A CHECK THAT NEVER HAPPENS.</b>
/// <c>ConsoleStart</c> assigns <c>TakeableTree = null</c> unconditionally and
/// says why — the branch is authoritative, a local tree is a cache this console
/// may not have, and a takeover needing one could only happen on the machine
/// that ran the flight. That is a decision, and a good one.
/// </para>
/// <para>
/// <b>But the surface reads as though it looked.</b> <c>Took</c> answered
/// <i>"There is no held tree for this flight"</i> — about THIS FLIGHT, when the
/// fact is about this console and never varies — and the actions modal said
/// taking a flight <i>"arrives in slice two"</i>, which shipped. A person reads
/// the first as "the tree was cleaned up" and goes looking for one, and the
/// second as a feature still coming.
/// </para>
/// <para>
/// <b>The same shape as `Reloaded`'s remarks</b> promising a keep-the-last-good-
/// model path that never executed: a correct-sounding sentence about something
/// nobody checked. The null stays; the sentences stop lying about it.
/// </para>
/// <para>
/// <b>And the key is right to be absent.</b> <c>Keymap</c> offers `t` only when
/// <c>Takeable</c>, which derives from the same always-null field — so the key
/// is never advertised, which is correct. What was missing is anywhere saying
/// WHY, which is what the actions modal is for.
/// </para>
/// </remarks>
public class TheTakeoverRefusalTellsTheTruthTests
{
    private static AppState AFlight() => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "f-1",
                FlightNumber = "gg-14",
                Name = "something worth taking",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    [Test]
    public async Task The_refusal_is_about_this_console_not_about_this_flight()
    {
        var refused = ConsoleLoop.Took(AFlight(), new ConsoleDoubles.NeverTakes());

        await Assert.That(refused.LastTakeover).IsNotNull();
        await Assert.That(refused.LastTakeover!).DoesNotContain("for this flight")
            .Because("nothing about this flight was examined; the answer is the same for "
                   + "every flight and always will be.");
        await Assert.That(refused.LastTakeover!).Contains("this console")
            .Because("the fact is about where you are standing.");
    }

    [Test]
    public async Task The_refusal_says_where_a_takeover_could_happen()
    {
        // A refusal that names no alternative leaves a person with nowhere to
        // go. The tree exists on the machine that ran the flight.
        var refused = ConsoleLoop.Took(AFlight(), new ConsoleDoubles.NeverTakes());

        await Assert.That(refused.LastTakeover!).Contains("ran the flight")
            .Because("the takeover is possible, just not from here.");
    }

    [Test]
    public async Task The_actions_modal_no_longer_says_the_feature_is_coming()
    {
        // It arrived. A modal still promising it for a future slice is a
        // sentence that was true once and has been wrong ever since.
        var text = PaneText.Modal(
            Reducer.Reduce(AFlight(), Command.ToggleFlightActions));

        await Assert.That(text).DoesNotContain("slice two");
        await Assert.That(text).DoesNotContain("Nothing can be done from here yet");
    }

    [Test]
    public async Task The_actions_modal_says_why_takeover_is_not_offered_here()
    {
        // The key is correctly absent - Keymap offers `t` only when Takeable,
        // which is never. Nothing said why, so the absence read as a bug.
        var text = PaneText.Modal(
            Reducer.Reduce(AFlight(), Command.ToggleFlightActions));

        await Assert.That(text).Contains("gg-14")
            .Because("the modal is about the selected flight and still names it.");
        await Assert.That(text).Contains("ran the flight")
            .Because("a person looking for the takeover key should find out where it lives.");
    }

    [Test]
    public async Task A_console_wired_with_no_take_session_still_says_that_instead()
    {
        // The other branch, and it is a different fact: not configured at all,
        // rather than configured and never holding a tree.
        var refused = ConsoleLoop.Took(AFlight(), take: null);

        await Assert.That(refused.LastTakeover!).Contains("not configured");
    }

    [Test]
    public async Task A_flight_with_a_tree_is_still_taken()
    {
        // THE ANCHOR. The null is a decision, not a deletion - if a tree ever
        // arrives, the path still works and this proves the refusal did not
        // swallow it.
        var takes = new ConsoleDoubles.NeverTakes();

        _ = ConsoleLoop.Took(AFlight() with { TakeableTree = "/tmp/a-tree" }, takes);

        await Assert.That(takes.Asked).IsEqualTo(1)
            .Because("a held tree still reaches the take session.");
    }
}
