namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.2-02</b> - a leg is revised and dropped by its subject; a subject the draft does not
/// hold is refused naming the subjects it does.
/// </summary>
/// <remarks>
/// No index (rule 5): the agent and the person reorder the same list, so "leg 2" means a
/// different leg to each of them after either moves one.
/// </remarks>
public class ALegIsAddressedBySubjectTests
{
    private static ItineraryServerHarness TwoLegs() => new ItineraryServerHarness()
        .Call("set_intent", new { text = "three findings in one bug" })
        .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
        .Call("draft_leg", new { subject = "the padding", work_kind = "implement", reason = "shared", after = "the icon" });

    [Test]
    public async Task A_leg_is_revised_by_its_subject()
    {
        using var server = TwoLegs().Call("revise_leg", new { subject = "the padding", reason = "a shared padding change" });

        var answers = await server.RunAsync();

        await Assert.That(ItineraryServerHarness.Result(answers[^1]).IsError).IsFalse();
        var draft = ((Gg.Client.DraftRead.Held)server.Drafts.Read("draft")).Draft;
        await Assert.That(draft.Legs.Single(l => l.Subject == "the padding").Reason)
            .IsEqualTo("a shared padding change");
    }

    [Test]
    public async Task A_leg_is_dropped_by_its_subject()
    {
        using var server = TwoLegs()
            .Call("revise_leg", new { subject = "the padding", after = "" })
            .Call("drop_leg", new { subject = "the icon" });

        var answers = await server.RunAsync();

        await Assert.That(ItineraryServerHarness.Result(answers[^1]).IsError).IsFalse();
        var draft = ((Gg.Client.DraftRead.Held)server.Drafts.Read("draft")).Draft;
        await Assert.That(draft.Legs.Select(l => l.Subject)).IsEquivalentTo((string?[])["the padding"]);
    }

    [Test]
    public async Task A_subject_the_draft_does_not_hold_is_refused_naming_the_ones_it_does()
    {
        using var server = TwoLegs().Call("revise_leg", new { subject = "the colour", reason = "x" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("'the colour'");
        await Assert.That(text).Contains("the icon");
        await Assert.That(text).Contains("the padding");
    }

    [Test]
    public async Task A_leg_another_comes_after_is_not_dropped_out_from_under_it()
    {
        using var server = TwoLegs().Call("drop_leg", new { subject = "the icon" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(isError).IsTrue()
            .Because("dropping it would leave 'the padding' after a leg that no longer exists.");
        await Assert.That(text).Contains("the padding");
    }

    [Test]
    public async Task A_subject_under_two_kinds_is_named_by_its_kind_too()
    {
        // TRIAGING A THING AND IMPLEMENTING IT are two legs about one subject (ItineraryDraft
        // allows it), so the subject alone does not say which.
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "triage", reason = "look first" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "then fix" })
            .Call("drop_leg", new { subject = "the icon" })
            .Call("drop_leg", new { subject = "the icon", of_kind = "triage" });

        var answers = await server.RunAsync();

        var (ambiguous, wasError) = ItineraryServerHarness.Result(answers[2]);
        await Assert.That(wasError).IsTrue();
        await Assert.That(ambiguous).Contains("triage");
        await Assert.That(ambiguous).Contains("implement");
        await Assert.That(ItineraryServerHarness.Result(answers[3]).IsError).IsFalse();
    }

    [Test]
    public async Task The_same_leg_twice_is_refused_by_the_contracts_rule()
    {
        using var server = TwoLegs()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "again" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("one piece of work written twice");
    }
}
