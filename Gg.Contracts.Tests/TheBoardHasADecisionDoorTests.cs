using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// How a person answers a nomination that is waiting for one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sub-resource, the way a flight's decisions are.</b>
/// <c>/v1/flights/{ref}/decisions</c> is how somebody answers a gate, and this
/// is the same act one noun earlier — so it is spelled the same way rather than
/// as a pair of verb-suffixed routes. One door, one request, and the outcome is
/// a word.
/// </para>
/// <para>
/// <b>THE OUTCOME IS AN ENDING, SO IT REUSES THE ENDING VOCABULARY.</b> A
/// person opening a nomination ends it <c>opened</c> and declining ends it
/// <c>declined</c>; a second set of words for the same two states would be two
/// spellings that have to be kept agreeing, and the day they stop is the day a
/// board row says something the door cannot produce.
/// </para>
/// <para>
/// <b>Developer audience and the session header, never a runner's.</b> A runner
/// able to answer a nomination could decide the work it is about to be handed —
/// which is the containment property the whole platform rests on, stated one
/// door over as "a runner able to record a decision could answer for the person
/// it is meant to be waiting on".
/// </para>
/// </remarks>
public class TheBoardHasADecisionDoorTests
{
    private static Endpoint Door() =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/board/{id}/decisions" && e.Method == "POST");

    [Test]
    public async Task Only_a_person_answers_a_nomination()
    {
        var door = Door();

        await Assert.That(door.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(door.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        await Assert.That(door.RequiredHeaders).DoesNotContain(ProtocolSurface.RunnerHeader)
            .Because("a runner able to answer a nomination could decide the work it is about to "
                   + "be handed, which is the containment property the platform rests on.");
    }

    [Test]
    public async Task It_answers_with_nothing_and_says_so()
    {
        var door = Door();

        await Assert.That(door.Request).IsEqualTo(typeof(NominationDecision));
        await Assert.That(door.Response).IsNull()
            .Because("ADR-0012: the write is a command, so the control plane takes the decision "
                   + "and the caller learns what happened by reading. Answering inline would "
                   + "mean the door waited for its own effect.");
        await Assert.That(door.Statuses).Contains(202);
        await Assert.That(door.Statuses).DoesNotContain(200);
    }

    [Test]
    public async Task A_decision_names_an_outcome_and_a_reason_and_nothing_else()
    {
        var members = typeof(NominationDecision)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(NominationDecision.Outcome),
            nameof(NominationDecision.Because),
        });
    }

    [Test]
    public async Task The_outcomes_are_the_endings_a_person_can_cause()
    {
        // ONE VOCABULARY, NOT TWO. A person can cause exactly two of the six
        // endings; the other four are the board's, the world's and the clock's.
        // Minting a second set of words for these two would be two spellings to
        // keep agreeing, and the day they stop is the day the door produces a
        // word no row can carry.
        await Assert.That(NominationDecisions.All).IsEquivalentTo((string[])
            [NominationEndings.Opened, NominationEndings.Declined]);

        foreach (var outcome in NominationDecisions.All)
        {
            await Assert.That(NominationEndings.All).Contains(outcome)
                .Because($"'{outcome}' is an answer the door accepts and must therefore be an "
                       + "ending a row can record.");
        }
    }

    [Test]
    public async Task A_person_cannot_cause_the_endings_that_are_not_theirs()
    {
        // The other half, and it is what stops the door becoming a way to write
        // any state onto a row. Superseding is the board's, withdrawal is the
        // world's, lapsing is the clock's, and refusal is the rules' - none of
        // them is something a person decides, and a door that accepted them
        // would let somebody record that the clock did what they did.
        foreach (var notTheirs in (string[])
                 [NominationEndings.Superseded, NominationEndings.Withdrawn,
                  NominationEndings.Refused, NominationEndings.Lapsed])
        {
            await Assert.That(NominationDecisions.All).DoesNotContain(notTheirs);
        }
    }
}
