using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.4-01</b> - every result for a draft with an intent and a leg carries each leg's verdict
/// and reason from <c>POST /v1/itineraries/check</c>, so a refused leg is seen in the result that
/// drafted it; before that, the result says what is missing.
/// </summary>
/// <remarks>
/// <b>The criterion slice sixty-three exists for.</b> Every defect on ADR-0038 Decision 9's list -
/// an undeclared kind, a kind the destination does not open, a guard that can never attach - was
/// found after flying. Shown here, it is found while the agent can still change the plan. The
/// verdict is the check's own, never computed here (rule 7).
/// </remarks>
public class EveryResultCarriesThePreviewTests
{
    [Test]
    public async Task A_refused_leg_is_seen_in_the_result_that_drafted_it()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "three findings in one bug" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
            .Call("draft_leg", new { subject = "the guard", work_kind = "implement", reason = "the guard" });
        server.Reads.Verdicts["the guard"] =
            (LegVerdicts.Refused, "Destination 'the-plan' requires plan-in-scope, which can never attach.");

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[2]);
        await Assert.That(isError).IsFalse()
            .Because("the leg was drafted; the preview is news about it, not a refusal of the edit.");
        await Assert.That(text).Contains("refused");
        await Assert.That(text).Contains("plan-in-scope, which can never attach");
        await Assert.That(text).Contains("opens")
            .Because("and the leg that would open says so too, so the two are told apart.");
    }

    [Test]
    public async Task Every_result_after_the_first_leg_is_previewed()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "three findings" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
            .Call("revise_leg", new { subject = "the icon", reason = "smallest first" })
            .Call("show_plan");

        var answers = await server.RunAsync();

        foreach (var answer in answers.Skip(1))
        {
            await Assert.That(ItineraryServerHarness.Result(answer).Text).Contains("would open");
        }

        await Assert.That(server.Reads.Asked.Count(a => a.StartsWith("check", StringComparison.Ordinal)))
            .IsEqualTo(3);
    }

    [Test]
    public async Task Before_an_intent_and_a_leg_it_says_what_the_preview_waits_for()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" });

        var answers = await server.RunAsync();

        var (text, _) = ItineraryServerHarness.Result(answers[0]);
        await Assert.That(text).Contains("set_intent")
            .Because("a plan with no intent has nothing for its legs to be about, so the check "
                   + "would refuse it whole; the result says so instead of asking.");
        await Assert.That(server.Reads.Asked.Any(a => a.StartsWith("check", StringComparison.Ordinal))).IsFalse();
    }
}
