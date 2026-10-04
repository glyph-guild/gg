using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// When the preview forwards nowhere, the refusal says where the stack actually is.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.4-02 and -03, the (a) half of step 4.</b> gg tells a hook which port
/// to serve on (S58.4-01) and the stack may still answer somewhere else — a
/// compose file with a hard-coded mapping, an orchestrator that assigns its own,
/// a traefik in front. Today that is <i>"nothing answers at
/// http://localhost:8080"</i>, which is true and tells a reader nothing about
/// where to look.
/// </para>
/// <para>
/// <b>`ready` already knows.</b> It reports <c>url=…</c> — the address the stack
/// is actually serving — so when the origin does not answer and the report
/// disagrees with it, the refusal can name both. That turns ADR-0033 Decision
/// 8's example from a silence into a sentence: <c>jdapp.yaml</c> forwards to
/// 8080, the Angular app binds 4200, and a person opening the preview reaches
/// traefik.
/// </para>
/// <para>
/// <b>It does not change what is refused, only what the refusal says.</b> A
/// stack answering where the connector looks is still fine, and a flight with no
/// exposure is still not this loop's failure — <c>ExposureServed</c>'s own rule:
/// a preview that could not be served is a flight that still did its work.
/// </para>
/// </remarks>
public class APreviewThatForwardsNowhereSaysSoTests
{
    private static readonly IReadOnlyList<string> Serves = [FactKinds.PreviewUrl];

    private static Func<string, CancellationToken, Task<string?>> Answers(string? why = null) =>
        (_, _) => Task.FromResult(why);

    [Test]
    public async Task A_reported_address_that_disagrees_is_named_in_the_refusal()
    {
        var refusal = await PreviewAnswers.RefusalAsync(
            Serves, "http://localhost:8080", Answers("connection refused"),
            reported: "http://localhost:4200");

        await Assert.That(refusal).IsNotNull();

        await Assert.That(refusal!).Contains("4200")
            .Because("the stack said where it is, so a refusal that only names the address "
                   + "nobody is listening on makes a reader go and find out what `ready` "
                   + "already reported.");

        await Assert.That(refusal!).Contains("8080")
            .Because("and both, because the fix is one of the two moving - either the stack "
                   + "binds where the connector looks or the exposure document changes.");
    }

    [Test]
    public async Task A_reported_address_that_agrees_is_not_turned_into_a_mismatch()
    {
        // THE SAME ADDRESS IS NOT A DISAGREEMENT. Here the stack is where it
        // should be and simply is not answering - a crash, a slow boot - and
        // saying "it is at 8080 rather than 8080" would be nonsense in front of
        // somebody already confused.
        var refusal = await PreviewAnswers.RefusalAsync(
            Serves, "http://localhost:8080", Answers("connection refused"),
            reported: "http://localhost:8080");

        await Assert.That(refusal).IsNotNull()
            .Because("nothing answers, which is still a refusal.");

        await Assert.That(refusal!).DoesNotContain("rather than")
            .Because("there is no mismatch to report, so the refusal stays the plain one.");
    }

    [Test]
    public async Task Nothing_reported_leaves_the_refusal_as_it_was()
    {
        // EVERY ENVIRONMENT WITH NO HOOKS, which is every one in the field. The
        // generic refusal is what they get, unchanged.
        var refusal = await PreviewAnswers.RefusalAsync(
            Serves, "http://localhost:8080", Answers("connection refused"), reported: null);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("8080");
    }

    [Test]
    public async Task A_stack_that_answers_where_the_connector_looks_is_not_diagnosed()
    {
        // S58.4-03's first half. A reported address that differs is NOT a
        // problem while the origin answers - traefik forwarding to the app is
        // exactly that shape, and a person opening the preview gets the app.
        var refusal = await PreviewAnswers.RefusalAsync(
            Serves, "http://localhost:8080", Answers(),
            reported: "http://localhost:4200");

        await Assert.That(refusal).IsNull()
            .Because("the address a person opens answers, so what the stack reports about its "
                   + "own internals is not a fault - something in front of it is forwarding, "
                   + "which is what traefik is for.");
    }

    [Test]
    public async Task A_flight_with_no_exposure_is_not_diagnosed()
    {
        // S58.4-03's second half, and ExposureServed's own rule: a preview that
        // could not be served is a flight that still did its work. Refusing here
        // would fail a loop for something that happened before it started.
        foreach (var unserved in (string?[]) [null, "", "  "])
        {
            await Assert.That(await PreviewAnswers.RefusalAsync(
                    Serves, unserved, Answers("connection refused"),
                    reported: "http://localhost:4200"))
                .IsNull();
        }
    }

    [Test]
    public async Task A_kind_that_serves_no_preview_is_not_diagnosed()
    {
        await Assert.That(await PreviewAnswers.RefusalAsync(
                [FactKinds.LoopOutcome], "http://localhost:8080", Answers("refused"),
                reported: "http://localhost:4200"))
            .IsNull()
            .Because("a kind that publishes no address is asked no question about one.");
    }
}
