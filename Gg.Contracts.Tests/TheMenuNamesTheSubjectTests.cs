namespace Gg.Contracts.Tests;

using Gg.Contracts;

/// <summary>
/// A menu that admits several flights says how to keep them apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because everywhere else has asked and been ignored.</b> The nomination
/// tool's description has asked for <c>subject</c> since it was written, and
/// <c>plan.yaml</c>'s instructions ask again — sharpened on 2026-09-29 to say
/// it is a separate argument and that prose does not count. Three measured
/// passes set it anyway: GG-380 wrote <i>"Subject: AB#18291 leg 3 —"</i> into
/// the REASON, GG-389 and GG-407 omitted it entirely.
/// </para>
/// <para>
/// <b>And the cost is the whole itinerary feature.</b> An itinerary is minted
/// only when a nomination names a subject, so none has ever existed — thirty
/// nominations on the dev tenant, not one <c>itinerary:</c>. The menu is the one
/// place an agent is guaranteed to read, and it listed work kinds, environments,
/// repositories and the cap while never mentioning the field all of it depends
/// on.
/// </para>
/// <para>
/// <b>Only where several are admitted.</b> A destination that opens one flight
/// per pass has nothing to tell apart, and every classifier is in that case —
/// telling it to distinguish nominations it will only make one of is advice
/// about a situation it cannot be in.
/// </para>
/// </remarks>
public class TheMenuNamesTheSubjectTests
{
    private static Envelope Opening(int? cap) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Obligations = [],
        Loops = [],
        Destinations =
        [
            new Destination
            {
                Id = "the-plan",
                Kind = DestinationKinds.Flight,
                Requires = [],
                Opens = ["implement", "review"],
                CapPerPass = cap,
            },
        ],
    };

    [Test]
    public async Task A_menu_admitting_several_says_to_give_each_one_a_subject()
    {
        var menu = EnvelopeText.RenderMenu(Opening(5));

        await Assert.That(menu).IsNotNull();
        await Assert.That(menu!).Contains("subject")
            .Because("an itinerary is what a subject creates, and none has ever been minted - "
                   + "so the one place an agent is guaranteed to read must name the field "
                   + "everything depends on, having been ignored everywhere else.");
    }

    [Test]
    public async Task It_says_prose_in_the_reason_does_not_count()
    {
        var menu = EnvelopeText.RenderMenu(Opening(5));

        await Assert.That(menu!).Contains("ARGUMENT")
            .Because("GG-380 wrote 'Subject: ...' into the reason and lost two of three legs, "
                   + "so saying 'give a subject' is not enough - it has to say where.");
    }

    [Test]
    public async Task A_menu_admitting_one_says_nothing_about_subjects()
    {
        // THE GUARD. Every classifier opens one flight per pass and has nothing
        // to tell apart; advice about distinguishing nominations it will only
        // make one of is advice about a situation it cannot be in.
        var menu = EnvelopeText.RenderMenu(Opening(1));

        await Assert.That(menu!).DoesNotContain("subject")
            .Because("a destination that opens a single flight per pass has nothing to keep "
                   + "apart, and three measured triage runs read this menu.");
    }

    [Test]
    public async Task The_predicate_and_the_menu_are_one_decision()
    {
        // THE POINT OF EXPOSING IT AT ALL. The control plane must set
        // LeaseGranted.NominatesSeveral, and the one thing it must never do is
        // decide it a second time: a schema that REQUIRES what the menu did not
        // explain refuses an agent for a rule it was never given, and a menu
        // that asks where the schema does not is the fourth way of asking
        // politely. So the menu reads this predicate, and this test is what
        // keeps them one decision rather than two that currently agree.
        foreach (var cap in (int?[])[null, 1, 2, 7])
        {
            var envelope = Opening(cap);
            var menu = EnvelopeText.RenderMenu(envelope);
            var asks = menu is not null && menu.Contains("GIVE EACH ONE", StringComparison.Ordinal);

            await Assert.That(EnvelopeText.NominatesSeveral(envelope)).IsEqualTo(asks)
                .Because($"cap {cap?.ToString() ?? "absent"}: what the runner is told to "
                       + "require is exactly what the agent was told to give.");
        }
    }

    [Test]
    public async Task An_envelope_that_opens_no_flight_expects_nothing()
    {
        // The degenerate case, which RenderMenu answers with null rather than
        // an empty heading. A predicate that threw here, or said true, would
        // put `subject` in the schema of a flight with no menu at all.
        var envelope = Opening(3) with { Destinations = [] };

        await Assert.That(EnvelopeText.RenderMenu(envelope)).IsNull();
        await Assert.That(EnvelopeText.NominatesSeveral(envelope)).IsFalse();
    }
}
