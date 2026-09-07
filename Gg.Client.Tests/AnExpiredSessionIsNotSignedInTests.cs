using System.Net;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A session the control plane refuses is a session nobody is signed in with.
/// </summary>
/// <remarks>
/// <para>
/// <b>An expired session read as a network error, so nothing offered to sign
/// in.</b> Every verb's <c>Session()</c> asks whether a session FILE exists,
/// never whether it is still good - so a stale token goes out, the control
/// plane answers 401, and <c>EnsureSuccessStatusCode</c> turns that into an
/// <c>HttpRequestException</c>. The console opens its sign-in modal for
/// <c>NotSignedInException</c> and nothing else, so a person whose twelve hours
/// were up got `Response status code does not indicate success: 401
/// (Unauthorized)' in a pane and no way forward.
/// </para>
/// <para>
/// <b>The comment above that branch already claimed this worked.</b>
/// <c>ConsoleStart</c> says the modal is opened there because "a session that
/// expires while somebody is watching the console is the same fact arriving
/// later, and the refresh key comes back through this line". It is the shape
/// this estate keeps finding: prose asserting a property the code beside it
/// does not have.
/// </para>
/// <para>
/// <b>Answered on the wire rather than by the clock.</b> A local expiry check
/// would save a round trip and would not catch a session revoked, a tenant
/// removed, or a laptop whose clock is wrong - and those are the same fact to
/// the person sitting there. 401 is the control plane saying so, which is the
/// only authority on it.
/// </para>
/// </remarks>
public class AnExpiredSessionIsNotSignedInTests
{
    private sealed class Refuses(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static ControlPlaneClient Answering(HttpStatusCode status) =>
        new(new HttpClient(new Refuses(status)) { BaseAddress = new Uri("http://cp.test/") });

    [Test]
    public async Task A_401_is_not_signed_in_rather_than_a_network_error()
    {
        var refusing = Answering(HttpStatusCode.Unauthorized);

        await Assert.That(async () => await refusing.ListFlightsAsync("stale-token"))
            .Throws<NotSignedInException>()
            .Because("the console offers to sign in for this exception and no other, so a "
                   + "401 that arrives as an HttpRequestException is a dead end with a "
                   + "status code in it.");
    }

    [Test]
    public async Task And_it_says_what_to_do()
    {
        var refusing = Answering(HttpStatusCode.Unauthorized);

        try
        {
            await refusing.ListFlightsAsync("stale-token");
        }
        catch (NotSignedInException refused)
        {
            await Assert.That(refused.Message).Contains("no longer valid")
                .Because("`not signed in' and `signed in with something the control plane "
                       + "will not take' are different facts, and the second is the one a "
                       + "person whose twelve hours ran out is looking at.");
        }
    }

    [Test]
    public async Task Every_door_answers_the_same_way()
    {
        // ONE PLACE, because thirty-six calls pass through it. A per-verb check
        // would be thirty-six chances to forget, and the one forgotten would be
        // the verb somebody happened to press.
        var refusing = Answering(HttpStatusCode.Unauthorized);

        await Assert.That(async () => await refusing.GetFlightAsync("stale", "GG-1"))
            .Throws<NotSignedInException>();
        await Assert.That(async () => await refusing.ListRunnersAsync("stale"))
            .Throws<NotSignedInException>();
        await Assert.That(async () => await refusing.GatesAsync("stale"))
            .Throws<NotSignedInException>();
    }

    [Test]
    public async Task A_403_is_left_alone_because_it_is_a_different_fact()
    {
        // SIGNED IN AND NOT ALLOWED is not signed out, and offering to sign in
        // again would send somebody round a loop that cannot help them.
        var forbidding = Answering(HttpStatusCode.Forbidden);

        await Assert.That(async () => await forbidding.ListFlightsAsync("good-token"))
            .Throws<HttpRequestException>()
            .Because("a runner token on a developer door is refused for a reason signing in "
                   + "again does not change.");
    }
}
