using System.Net;
using System.Text;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The three calls that make up an introduction, and what each refusal means.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four statuses on one route, and they are four different errands.</b> A
/// runner that is not there is a typo or a retirement; one somebody else
/// registered is a conversation with a person; one that registered before keys
/// existed is a machine to restart. A client that returned null for all three
/// would send somebody through them in turn, starting with the wrong one.
/// </para>
/// <para>
/// <b>And on the collect route, 204 against 404 is the distinction the whole
/// type exists for.</b> The contract says it in as many words: a console polling
/// has to tell WAITING from WRONG. Collapsing those two silences is this
/// system's named worst failure, and a nullable answer is exactly how it would
/// be collapsed here.
/// </para>
/// </remarks>
public class IntroductionCallTests
{
    /// <summary>Answers as the control plane would, and remembers being asked.</summary>
    private sealed class Answering(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = body is null
                    ? new StringContent("")
                    : new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ControlPlaneClient Against(Answering handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    private const string Runner = "01a06385-322f-7371-93a2-ce35db5c4fbe";

    private const string AnIntroduction = """
        {"introductionId":"intro-1","runnerId":"01a06385-322f-7371-93a2-ce35db5c4fbe",
         "runnerPublicKey":"a-key","capability":"a-capability",
         "expiresAt":"2026-09-08T08:01:00+00:00"}
        """;

    // ---- being introduced ----

    [Test]
    public async Task An_introduction_carries_the_key_it_was_asked_to_carry()
    {
        var handler = new Answering(HttpStatusCode.OK, AnIntroduction);

        var introduced = await Against(handler)
            .IntroduceRunnerAsync(
                "a-session", Runner, "the-console-key", WatchARunner.Purpose);

        await Assert.That(introduced.Refusal).IsEqualTo(IntroductionRefusal.None);
        await Assert.That(introduced.Introduction!.IntroductionId).IsEqualTo("intro-1");

        await Assert.That(handler.Seen!.RequestUri!.AbsolutePath)
            .IsEqualTo($"/v1/runners/{Runner}/introduction");
        await Assert.That(handler.SentBody).Contains("the-console-key")
            .Because("the control plane stores the hash of this key against the row, so a "
                   + "console that sends one key and seals with another has declared something "
                   + "it did not do.");

        // AND WHAT IT IS FOR, which used to be unsayable. Every introduction was
        // minted for tailing a log because the request had nowhere to name
        // anything else, so the second purpose existed in the build and in
        // nothing that crossed.
        await Assert.That(handler.SentBody).Contains(RunnerCapabilityPurposes.TailYourOwnLog)
            .Because("a purpose the control plane never receives is a capability it cannot "
                   + "narrow, whatever the vocabulary says.");
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound, IntroductionRefusal.NoSuchRunner, "gg runners")]
    [Arguments(HttpStatusCode.Forbidden, IntroductionRefusal.NotYoursToReach, "registered it")]
    [Arguments(HttpStatusCode.Conflict, IntroductionRefusal.RegisteredBeforeKeys, "Restarting it")]
    public async Task Each_refusal_is_its_own_answer_and_names_the_next_move(
        HttpStatusCode status, IntroductionRefusal refusal, string names)
    {
        var introduced = await Against(new Answering(status))
            .IntroduceRunnerAsync(
                "a-session", Runner, "the-console-key", WatchARunner.Purpose);

        await Assert.That(introduced.Refusal).IsEqualTo(refusal);
        await Assert.That(introduced.Introduction).IsNull();
        await Assert.That(introduced.Said).Contains(names)
            .Because("a refusal that names no way forward is a dead end, and these three end "
                   + "in three different places.");
    }

    // ---- leaving the offer ----

    [Test]
    public async Task An_accepted_offer_is_left_where_the_runner_will_find_it()
    {
        var handler = new Answering(HttpStatusCode.Accepted);

        var left = await Against(handler).LeaveOfferAsync(
            "a-session", "intro-1", new RunnerSealedOffer { Sealed = [1, 2, 3] });

        await Assert.That(left).IsTrue();
        await Assert.That(handler.Seen!.RequestUri!.AbsolutePath)
            .IsEqualTo("/v1/introductions/intro-1");
        await Assert.That(handler.Seen.Method).IsEqualTo(HttpMethod.Post);
    }

    [Test]
    public async Task An_offer_left_at_an_introduction_that_is_gone_says_so()
    {
        var left = await Against(new Answering(HttpStatusCode.NotFound)).LeaveOfferAsync(
            "a-session", "intro-1", new RunnerSealedOffer { Sealed = [1, 2, 3] });

        await Assert.That(left).IsFalse();
    }

    // ---- collecting the answer ----

    [Test]
    public async Task Not_answered_yet_and_no_such_introduction_are_different_answers()
    {
        // THE ONE THIS TYPE EXISTS FOR. Both are an absent answer and only one is
        // a reason to keep asking.
        var waiting = await Against(new Answering(HttpStatusCode.NoContent))
            .CollectAnswerAsync("a-session", "intro-1");

        var wrong = await Against(new Answering(HttpStatusCode.NotFound))
            .CollectAnswerAsync("a-session", "intro-1");

        await Assert.That(waiting.State).IsEqualTo(AnswerState.NotYet);
        await Assert.That(wrong.State).IsEqualTo(AnswerState.Gone);

        await Assert.That(waiting.State).IsNotEqualTo(wrong.State)
            .Because("a console that cannot tell these apart waits out its whole patience on a "
                   + "conversation that ended, then reports the runner as silent - which blames "
                   + "a machine for a clock.");

        await Assert.That(waiting.Answer).IsNull();
        await Assert.That(wrong.Answer).IsNull();
    }

    [Test]
    public async Task An_answer_that_arrived_comes_back_whole()
    {
        var handler = new Answering(
            HttpStatusCode.OK,
            $$"""{"runnerId":"{{Runner}}","sealed":"{{Convert.ToBase64String([7, 8, 9])}}"}""");

        var collected = await Against(handler).CollectAnswerAsync("a-session", "intro-1");

        await Assert.That(collected.State).IsEqualTo(AnswerState.Arrived);
        await Assert.That(collected.Answer!.Sealed).IsEquivalentTo(new byte[] { 7, 8, 9 })
            .Because("the bytes are the conversation; anything that reshapes them here is a "
                   + "relay that read what it carried.");

        await Assert.That(handler.Seen!.Method).IsEqualTo(HttpMethod.Get);
    }

    [Test]
    public async Task Every_one_of_these_states_its_protocol_and_its_session()
    {
        // The liveness half: the assertions above all pass on a request that
        // carries no headers at all, and an unauthenticated call would be
        // refused by the real control plane with a sentence about versions.
        var handler = new Answering(HttpStatusCode.NoContent);

        await Against(handler).CollectAnswerAsync("a-session", "intro-1");

        await Assert.That(handler.Seen!.Headers.Contains(GgVersions.ProtocolHeader)).IsTrue();
        await Assert.That(handler.Seen.Headers.GetValues(GgVersions.SessionHeader))
            .Contains("a-session");
    }
}
