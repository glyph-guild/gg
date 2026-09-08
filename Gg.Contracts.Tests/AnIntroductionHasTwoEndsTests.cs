using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Both ends of an introduction can name it, leave something, and collect something.
/// </summary>
/// <remarks>
/// <para>
/// <b>This closes a gap the first signalling commit shipped with, and the gap is
/// the useful part.</b> That commit declared the runner's answer route and gave
/// the console a capability and a key — and nowhere to put the offer it sealed,
/// no way to collect the answer, and no id to file either under. An introduction
/// is a short conversation with two ends; declaring one of them is declaring
/// half a handshake.
/// </para>
/// <para>
/// <b>It was found by a conformance test rather than by review.</b> The control
/// plane's "every declared endpoint is actually served" guard is what said the
/// signalling route had no server — and following that led to the missing half
/// rather than to a missing handler.
/// </para>
/// </remarks>
public class AnIntroductionHasTwoEndsTests
{
    private static Gg.Contracts.Description.Endpoint Route(string method) =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/introductions/{introductionId}" && e.Method == method);

    [Test]
    public async Task An_introduction_is_named_so_both_ends_can_refer_to_it()
    {
        // Without this the console holds a capability and a key with nowhere to
        // put what it sealed.
        await Assert.That(typeof(RunnerIntroduction).GetProperties().Select(p => p.Name))
            .Contains("IntroductionId");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerIntroduction)])
            .Contains("introductionId");
    }

    [Test]
    public async Task The_console_leaves_the_offer_and_collects_the_answer()
    {
        await Assert.That(Route("POST").Request).IsEqualTo(typeof(RunnerSealedOffer));
        await Assert.That(Route("GET").Response).IsEqualTo(typeof(RunnerSealedAnswer));

        foreach (var method in (string[])["POST", "GET"])
        {
            await Assert.That(Route(method).Audience).IsEqualTo(Audience.Developer);
            await Assert.That(Route(method).RequiredHeaders)
                .Contains(ProtocolSurface.SessionHeader);
            await Assert.That(Route(method).RequiredHeaders)
                .DoesNotContain(ProtocolSurface.RunnerHeader)
                .Because("this is the person's side; the runner's half is its own route with "
                       + "its own credential.");
        }
    }

    [Test]
    public async Task Waiting_for_an_answer_is_not_the_same_as_asking_about_nothing()
    {
        // 204 WHILE THERE IS NO ANSWER YET, and 404 for an introduction that
        // never existed or has expired. A console polling has to tell waiting
        // from wrong, and collapsing them is this system's named worst failure.
        await Assert.That(Route("GET").Statuses).Contains(204);
        await Assert.That(Route("GET").Statuses).Contains(404);
        await Assert.That(Route("GET").Statuses).Contains(200);
    }

    [Test]
    public async Task Leaving_an_offer_does_not_wait_for_the_far_end()
    {
        // 202, like the runner's answer route. A route that waited would put the
        // relay inside the conversation, which is the one thing it must not be.
        await Assert.That(Route("POST").Statuses).Contains(202);
        await Assert.That(Route("POST").Statuses).DoesNotContain(200);
    }

    [Test]
    public async Task The_conversation_is_addressed_rather_than_the_runner()
    {
        // BY THIS POINT THE RUNNER IS NOT WHAT IS BEING ADDRESSED. One
        // short-lived conversation is, and it outlives neither end - so it has
        // its own prefix rather than hanging off a machine that will still be
        // there tomorrow.
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/introductions")
            .Because("a route under an undeclared prefix is one the closure does not cover, "
                   + "and this one carries a capability.");
    }
}
