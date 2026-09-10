using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The checklist is not a tab in this console.
/// </summary>
/// <remarks>
/// <para>
/// <b>Withdrawn because the pane answered two questions and committed to
/// neither.</b> A checklist's requirements are PINNED to what the flight
/// compiled at creation; its satisfiers are computed LIVE against today's
/// fleet. Before a lease those agree and the pane is a job list - this label is
/// unmet, and here is who can satisfy it. After one they come apart: the flight
/// was leased because a runner matched, so the satisfier column stops being
/// about this flight and becomes a reading of the estate. On a finished flight
/// it is neither a historical record nor a current diagnosis.
/// </para>
/// <para>
/// <b>And nothing gated it.</b> <c>p</c> opened it on any selected row -
/// queued, leased, running or long finished - so the pane was equally reachable
/// where it could not mean anything.
/// </para>
/// <para>
/// <b>What is NOT removed, and this is the point of the test.</b> The
/// <see cref="Checklist"/> contract stays, <c>gg plan</c> stays, and
/// <c>ConsoleHandFlight</c> still reads a checklist to refuse a hand-flown
/// flight the fleet cannot serve. Only the TAB is gone. A removal that took
/// the wire type with it would drag the control plane into a coordinated
/// release for a UI decision.
/// </para>
/// </remarks>
public class TheChecklistTabIsGoneTests
{
    [Test]
    public async Task No_tab_on_the_bar_is_the_checklist()
    {
        var named = Tabs.All.Select(Tabs.Name).ToList();

        await Assert.That(named).DoesNotContain("Checklist")
            .Because("the bar's job is to say what there is, so a tab left on it is a "
                   + "promise the console no longer keeps.");

        // THE BAR IS STILL A BAR. Without this the assertion above is satisfied
        // by a console that has no tabs at all.
        await Assert.That(named).Contains("Queue");
        await Assert.That(named.Count).IsGreaterThanOrEqualTo(6)
            .Because($"one tab went, not the bar. Saw [{string.Join(", ", named)}]");
    }

    [Test]
    public async Task Nothing_answers_the_key_the_checklist_had()
    {
        // p WAS THE CHECKLIST'S, for `plan`. A key left bound to a view that no
        // longer exists is the dead-key shape this console has paid for four
        // times; a key left FREE is the next feature's to take.
        var bound = Tabs.All
            .Select(Tabs.KeyFor)
            .Where(stroke => stroke is not null)
            .Select(stroke => stroke!.Value.Name)
            .ToList();

        await Assert.That(bound).DoesNotContain("p")
            .Because("the tab it opened is gone, so the key opens nothing.");

        // Anchor: the sweep is reading real bindings, not an empty list.
        await Assert.That(bound).Contains("u")
            .Because("runners keeps its key, so this is a live reading of the bar.");
    }

    [Test]
    public async Task The_contract_and_the_hand_flight_refusal_are_untouched()
    {
        // THE LINE THIS REMOVAL DOES NOT CROSS. ConsoleHandFlight refuses a
        // hand-flown flight whose labels the fleet cannot serve, and it reads a
        // Checklist to do it. That is not the tab and must survive it.
        var source = ConsoleSource.Text("Gg.Console", "ConsoleHandFlight.cs");

        await Assert.That(source).Contains("Checklist")
            .Because("refusing to fly what cannot be served is a different feature from "
                   + "a pane, and it is the one that stops a person waiting on a flight "
                   + "no runner will ever take.");
    }
}
