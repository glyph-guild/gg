using System.Net;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The control plane's sentence reaching the person who was refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>On this route the refusal IS the answer.</b> A grant succeeds with 204
/// and no body, so everything worth reading arrives as a 403, a 404 or a 409 —
/// and every one of those carries a sentence the control plane composed
/// specifically to be acted on. "This tenant already has an administrator, ask
/// them" and "that is the last one, grant somebody else first" are different
/// instructions, and <c>EnsureSuccessStatusCode</c> renders both as `Response
/// status code does not indicate success'.
/// </para>
/// <para>
/// <b>Which is the assembled-and-discarded shape, one layer down.</b> The
/// endpoint writes each diagnosis on purpose; the client threw the body away
/// and kept the number. The far end had already done the work.
/// </para>
/// <para>
/// <b>A 404 stays a domain answer here.</b> The declaration lists 404, so
/// <c>ControlPlaneTooOldException</c>'s rule — "only for a route whose
/// declaration has no 404" — says this one does not get that reading. A
/// principal the tenant does not have is a real thing to be told.
/// </para>
/// </remarks>
public class ARefusedGrantSaysWhyTests
{
    private const string Detail =
        "Only an administrator may grant administration, and this tenant already has one.";

    private sealed class Refuses(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                // A REAL ProblemDetails, because that is what Results.Problem
                // sends and the whole point is reading the member it puts the
                // sentence in. A hand-written string would test a shape the
                // control plane never produces.
                Content = new StringContent(
                    body ?? Problem((int)status, Detail),
                    System.Text.Encoding.UTF8, "application/problem+json"),
                RequestMessage = request,
            });
    }

    /// <summary>What <c>Results.Problem</c> puts on the wire, member for member.</summary>
    private static string Problem(int status, string detail) =>
        "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.4\","
      + "\"title\":\"An error occurred while processing your request.\","
      + $"\"status\":{status},"
      + $"\"detail\":{System.Text.Json.JsonSerializer.Serialize(detail)}}}";

    private static ControlPlaneClient Answering(HttpStatusCode status, string? body = null) =>
        new(new HttpClient(new Refuses(status, body)) { BaseAddress = new Uri("http://cp.test/") });

    [Test]
    public async Task A_403_carries_the_sentence_that_says_what_to_do_instead()
    {
        var refusing = Answering(HttpStatusCode.Forbidden);

        try
        {
            await refusing.GrantAdminAsync("session", "p-7", granted: true);

            Assert.Fail("a 403 is a refusal and has to be raised as one.");
        }
        catch (AdminRefusedException refused)
        {
            await Assert.That(refused.Message).Contains("already has one", StringComparison.Ordinal)
                .Because("the control plane composed that sentence to be acted on - ask the "
                       + "administrator this tenant has - and a status code says none of it.");
        }
    }

    [Test]
    public async Task A_409_is_the_same_shape_and_a_different_instruction()
    {
        var refusing = Answering(
            HttpStatusCode.Conflict,
            """
            {"status":409,"detail":"That is this tenant's last administrator. Grant somebody else first."}
            """);

        try
        {
            await refusing.GrantAdminAsync("session", "p-7", granted: false);

            Assert.Fail("a 409 is a refusal and has to be raised as one.");
        }
        catch (AdminRefusedException refused)
        {
            await Assert.That(refused.Message).Contains("Grant somebody else first", StringComparison.Ordinal);
        }
    }

    [Test]
    public async Task A_404_says_which_id_was_not_found_rather_than_looking_like_an_old_control_plane()
    {
        var refusing = Answering(
            HttpStatusCode.NotFound,
            """
            {"status":404,"detail":"This tenant has no principal with that id."}
            """);

        try
        {
            await refusing.GrantAdminAsync("session", "p-7", granted: true);

            Assert.Fail("a 404 on this route is a domain answer and has to be raised as one.");
        }
        catch (AdminRefusedException refused)
        {
            await Assert.That(refused.Message).Contains("no principal with that id", StringComparison.Ordinal)
                .Because("the declaration lists 404, so ControlPlaneTooOldException's own "
                       + "rule keeps this a real answer rather than a protocol gap.");
        }
    }

    [Test]
    public async Task A_refusal_with_no_body_still_says_what_was_refused()
    {
        var refusing = Answering(HttpStatusCode.Forbidden, "");

        try
        {
            await refusing.GrantAdminAsync("session", "p-7", granted: true);

            Assert.Fail("a bodyless 403 is still a refusal.");
        }
        catch (AdminRefusedException refused)
        {
            await Assert.That(refused.Message).Contains("403", StringComparison.Ordinal)
                .Because("a proxy or an older control plane may refuse with nothing in it, "
                       + "and an empty message would be worse than the status code this "
                       + "replaced.");
        }
    }

    [Test]
    public async Task A_401_is_still_the_session_answer_rather_than_this_one()
    {
        var refusing = Answering(HttpStatusCode.Unauthorized);

        await Assert.That(async () => await refusing.GrantAdminAsync("stale", "p-7", granted: true))
            .Throws<NotSignedInException>()
            .Because("the console offers to sign in for that exception and no other, and a "
                   + "new refusal type on this route must not take the 401 away from it.");
    }
}
