namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.2-03</b> - every tool result, including a refusal, carries the whole draft rendered as text.
/// </summary>
/// <remarks>
/// Outside the mux the result is the only panel there is (ADR-0038 Decision 7). A result that
/// said "ok" would leave the agent describing a plan from memory - GG-380's "nominated three
/// flights" beside a board holding one.
/// </remarks>
public class EveryResultIsTheWholeDraftTests
{
    [Test]
    public async Task Each_result_holds_the_intent_and_every_leg()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "three findings in one bug" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
            .Call("draft_leg", new { subject = "the padding", work_kind = "implement", reason = "shared" })
            .Call("revise_leg", new { subject = "the icon", reason = "named first, and smallest" })
            .Call("show_plan")
            .Call("revise_leg", new { subject = "nothing like this", reason = "x" });

        var answers = await server.RunAsync();

        foreach (var answer in answers.Skip(2))
        {
            var (text, _) = ItineraryServerHarness.Result(answer);
            await Assert.That(text).Contains("three findings in one bug");
            await Assert.That(text).Contains("the icon");
            await Assert.That(text).Contains("the padding");
        }
    }

    [Test]
    public async Task An_unfinished_draft_says_what_is_missing()
    {
        using var server = new ItineraryServerHarness().Call("show_plan");

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[0]);
        await Assert.That(isError).IsFalse();
        await Assert.That(text).Contains("set_intent");
        await Assert.That(text).Contains("draft_leg");
    }

    [Test]
    public async Task A_refused_intent_is_answered_with_the_contracts_sentence_and_the_draft()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "kept" })
            .Call("set_intent", new { text = "words", uri = "https://example.invalid/issues/7" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[1]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("one payload");
        await Assert.That(text).Contains("kept")
            .Because("the refused change wrote nothing, and the draft shown is the one that stands.");
    }
}
