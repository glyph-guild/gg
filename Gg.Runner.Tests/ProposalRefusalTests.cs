using System.Net;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// <i>No credential was sent</i> and <i>the credential was refused</i> are two
/// facts, and they had one sentence between them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The error path was well written, fired every time, and named the wrong
/// cause.</b> Every proposal went out anonymous, came back 401, and produced
/// <i>"The credential registered for {slug} would not write to it… Register a
/// credential with write scope"</i> — about a credential that was resolved,
/// scope-checked at the lease, used successfully to push the branch, and then
/// never presented. A developer following that sentence rotates a credential
/// that was never used, and the envelope's destination declaration gets blamed
/// for a wiring gap.
/// </para>
/// <para>
/// <b>Slice one's lease finding, one context over.</b> <i>"Two different facts
/// collapse into one value… the fence answered unknown where the truth was you
/// were replaced."</i> The remedy is the same: give each fact its own sentence,
/// and prove neither can be reached by the other's condition.
/// </para>
/// <para>
/// <b>And the branch stays stated.</b> Slice three settled that the branch is
/// pushed before the gate is asked, so <i>a person has a commit to decide
/// about</i>. A proposal that cannot open must leave that intact and say so —
/// <c>Unsupported</c> already words it, and a refusal is not more final than an
/// unsupported provider.
/// </para>
/// </remarks>
public class ProposalRefusalTests
{
    private sealed class Answering(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private static HttpsDestinationAdapter Adapter(
        Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new("fixture", "forge.example",
            new HttpClient(new Answering(answer))
            {
                BaseAddress = new Uri("https://api.forge.example/"),
            });

    private static LandingRequest Landing(string secret) => new()
    {
        WorkingDirectory = Path.GetTempPath(),
        Slug = "acme/widgets",
        Branch = "gg/GG-42",
        BaseRef = "main",
        Title = "GG-42: a change",
        Secret = secret,
    };

    private static HttpResponseMessage Refuses(HttpRequestMessage _) =>
        new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"message":"Resource not accessible by integration"}"""),
        };

    private static async Task<string> DiagnosisOf(
        Func<HttpRequestMessage, HttpResponseMessage> answer, string secret)
    {
        var outcome = await Adapter(answer).ProposeAsync(Landing(secret), CancellationToken.None);

        return outcome is LandingOutcome.CredentialRefused(_, var diagnosis)
            ? diagnosis
            : throw new InvalidOperationException($"expected a refusal, got {outcome}");
    }

    // ---- two facts, two sentences ----

    [Test]
    public async Task No_credential_says_the_proposal_was_not_attempted()
    {
        var diagnosis = await DiagnosisOf(
            _ => throw new InvalidOperationException("nothing should have been sent"),
            secret: "");

        await Assert.That(diagnosis).Contains("not attempted")
            .Because("this is the fact: nothing was asked. A sentence that describes a refusal "
                   + "is describing something that did not happen.");
        await Assert.That(diagnosis).Contains("local:acme/widgets")
            .Because("it names the LOCATOR, because what is missing is an entry there.");
    }

    [Test]
    public async Task A_refused_credential_keeps_the_sentence_it_always_had()
    {
        var diagnosis = await DiagnosisOf(Refuses, secret: "a-narrow-credential");

        await Assert.That(diagnosis).Contains("would not write to it")
            .Because("today's wording is correct once it can only be reached when a credential "
                   + "really was presented and really was refused.");
        await Assert.That(diagnosis).Contains("Resource not accessible by integration")
            .Because("and the provider's own words are carried, because a refusal that "
                   + "paraphrases the provider is one nobody can act on.");
    }

    [Test]
    public async Task Neither_sentence_can_be_reached_by_the_others_condition()
    {
        // THE ASSERTION THAT MATTERS, in both directions. A refusal reachable
        // two ways is the collapse this slice exists to undo, and asserting
        // only that each says its own thing would not notice the overlap.
        var notAttempted = await DiagnosisOf(
            _ => throw new InvalidOperationException("nothing should have been sent"),
            secret: "");
        var refused = await DiagnosisOf(Refuses, secret: "a-narrow-credential");

        await Assert.That(notAttempted).DoesNotContain("would not write to it")
            .Because("the sentence about a refused credential must be unreachable when none was "
                   + "sent - which is the exact defect this slice found.");
        await Assert.That(refused).DoesNotContain("not attempted")
            .Because("and the sentence about nothing being asked must be unreachable when the "
                   + "provider really answered.");
        await Assert.That(notAttempted).DoesNotContain("write scope")
            .Because("scope advice on a missing credential sends somebody to widen a credential "
                   + "that does not exist. Naming the locator is the whole difference.");
    }

    // ---- and the branch stays stated ----

    [Test]
    public async Task Both_refusals_say_the_branch_is_on_the_remote()
    {
        // Slice three's guarantee, kept by whichever way the proposal fails. A
        // developer told only "refused" reasonably assumes the push is gone too,
        // and the commit they were meant to decide about is sitting there.
        foreach (var (what, diagnosis) in new[]
        {
            ("no credential", await DiagnosisOf(
                _ => throw new InvalidOperationException("nothing should have been sent"),
                secret: "")),
            ("a refused credential", await DiagnosisOf(Refuses, secret: "a-narrow-credential")),
        })
        {
            await Assert.That(diagnosis).Contains("branch is on the remote")
                .Because($"with {what}, the push already succeeded - and Unsupported has said "
                       + "so since slice three, while a refusal did not.");
            await Assert.That(diagnosis).Contains("did not land");
        }
    }
}
