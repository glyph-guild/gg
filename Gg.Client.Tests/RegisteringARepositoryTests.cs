using System.Net;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A repository can be registered from gg, and the answer says whether it is
/// live or waiting on somebody.
/// </summary>
/// <remarks>
/// <para>
/// <b>The door had no caller on this side.</b>
/// <c>POST /v1/airspace/repositories</c> has been declared and served since
/// slice ten, three refusals in <c>FlightIngress</c> point a person at it by
/// name, and nothing in gg could knock on it — so the answer to "my envelope
/// names a repository the console does not list" was to go and use something
/// that was not gg. The same <i>port nothing calls</i> shape as
/// <c>configure-this-runner</c>.
/// </para>
/// <para>
/// <b>202 IS THE ORDINARY ANSWER, not an edge.</b> Registering a NEW repository
/// widens what the tenant can reach, so it rides a flight to a gate and the
/// answer names who decides. A verb that reported 202 as success would tell
/// somebody their repository was registered when a person still has to agree;
/// one that reported it as failure would send them to fix something that is
/// working. It is its own outcome.
/// </para>
/// <para>
/// <b>200 is the narrow one</b> — an entry already live and identical, which is
/// the idempotent re-run rather than the usual path.
/// </para>
/// <para>
/// <b>Read by STATUS CODE, not by which body parsed</b>, which is
/// <c>DeclareNameAsync</c>'s rule and its reason: the two bodies are different
/// types, and a reader that tried the live one first would deserialize a
/// pending answer into a shape with none of its fields set and report a
/// repository as registered.
/// </para>
/// </remarks>
public class RegisteringARepositoryTests
{
    /// <summary>One canned answer, and what was sent to get it.</summary>
    private sealed class Answering(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = body is null
                    ? new StringContent("")
                    : new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ControlPlaneClient Against(Answering handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    private const string Live = """
        {"name":"payments","provider":"github","id":"R_123","path":"acme/payments",
         "credential":"required",
         "registeredBy":"Dana","registeredAt":"2026-09-13T09:00:00+00:00"}
        """;

    private const string Pending = """
        {"flight":"GG-42","awaiting":"platform-oncall",
         "widens":"the registry gaining the name payments"}
        """;

    private static RegisterRepositoryRequest ARegistration() => new()
    {
        Name = "payments",
        Provider = "github",
        Id = "R_123",
        Path = "acme/payments",
    };

    [Test]
    public async Task An_entry_already_live_comes_back_as_live()
    {
        var handler = new Answering(HttpStatusCode.OK, Live);

        var (live, pending) = await Against(handler)
            .RegisterRepositoryAsync("a-session", ARegistration());

        await Assert.That(pending).IsNull();
        await Assert.That(live!.Name).IsEqualTo("payments");
    }

    [Test]
    public async Task A_new_one_rides_a_flight_and_says_who_decides()
    {
        var handler = new Answering(HttpStatusCode.Accepted, Pending);

        var (live, pending) = await Against(handler)
            .RegisterRepositoryAsync("a-session", ARegistration());

        await Assert.That(live).IsNull()
            .Because("202 is not a quieter 200. A repository nobody has agreed to yet is not "
                   + "registered, and saying it is sends somebody to fly against it.");

        await Assert.That(pending!.Flight).IsEqualTo("GG-42");

        await Assert.That(pending.Awaiting).IsEqualTo("platform-oncall")
            .Because("a display a person can read rather than an id - the point of the "
                   + "answer is that somebody can go and ask them.");

        await Assert.That(pending.Widens).IsNotEmpty()
            .Because("what a gate is about is the half a person needs to approve it.");
    }

    [Test]
    public async Task A_refusal_the_tenant_cannot_act_on_is_raised_as_one()
    {
        // 403 IS THE ROLE ANSWER AND IT COMES FROM THERE, never from a flag
        // this side read. IsAdmin is a hint about what a surface may show -
        // "never a permission: every route checks the principal itself" - so a
        // console that decided for itself would be a console guessing.
        var refused = new Answering(HttpStatusCode.Forbidden, "not yours to register");

        await Assert.That(async () => await Against(refused)
                .RegisterRepositoryAsync("a-session", ARegistration()))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task A_malformed_entry_says_what_is_wrong_rather_than_throwing_a_status()
    {
        // THE DOOR'S OWN SENTENCE, carried rather than reworded. It names which
        // of provider, id and path was blank, and a client that replaced that
        // with "bad request" would be a second opinion about somebody else's
        // rule.
        var refused = new Answering(
            HttpStatusCode.BadRequest,
            "A registry entry names a provider, a forge id and a path");

        await Assert.That(async () => await Against(refused)
                .RegisterRepositoryAsync("a-session", ARegistration()))
            .Throws<EnvelopeRefusedException>();
    }
}
