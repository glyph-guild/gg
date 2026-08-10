using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// The four flight verbs, against a server that speaks the declared contract.
/// </summary>
/// <remarks>
/// Every one of them produces a structured result and nothing else. None of
/// them writes to a console, which is what makes "human output is a rendering
/// of the JSON" true by construction rather than by discipline - there is no
/// second path available to write.
/// </remarks>
public class FlightCommandTests
{
    private sealed class HeldSession(StoredSession? session) : ISessionStore
    {
        public StoredSession? Read() => session;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub, StoredSession? session = null) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSession(session ?? ASession()));

    [Test]
    public async Task Flights_lists_what_the_control_plane_returned()
    {
        await using var stub = new StubControlPlane();

        var result = await Build(stub).ListAsync();

        await Assert.That(result).IsTypeOf<VerbResult.Flights>();
        await Assert.That(((VerbResult.Flights)result).Value.Flights).IsNotEmpty();
    }

    [Test]
    public async Task Show_asks_for_the_reference_it_was_given()
    {
        // Not for a uuid it resolved itself. The control plane owns resolution,
        // and a client that translated GG-42 into an id first would need its
        // own copy of the tenant's numbering.
        await using var stub = new StubControlPlane();

        await Build(stub).ShowAsync("GG-42");

        await Assert.That(stub.ObservedPaths).Contains("/v1/flights/GG-42");
    }

    [Test]
    public async Task Show_passes_a_uuid_through_unchanged_too()
    {
        await using var stub = new StubControlPlane();

        await Build(stub).ShowAsync("019fe815-6136-7518-bb57-b06d6d3f411a");

        await Assert.That(stub.ObservedPaths).Contains("/v1/flights/019fe815-6136-7518-bb57-b06d6d3f411a");
    }

    [Test]
    public async Task A_reference_gg_cannot_read_is_refused_before_a_request_is_made()
    {
        // The one thing the client SHOULD decide locally: a round trip to be
        // told a typo is not a flight is a round trip nobody needed.
        await using var stub = new StubControlPlane();

        await Assert.That(async () => await Build(stub).ShowAsync("frobnicate"))
            .Throws<FlightReferenceException>();
        await Assert.That(stub.ObservedPaths.Any(p => p.StartsWith("/v1/flights/", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task A_flight_that_does_not_exist_is_reported_rather_than_thrown_as_a_status()
    {
        await using var stub = new StubControlPlane { FlightNotFound = true };

        await Assert.That(async () => await Build(stub).ShowAsync("GG-999"))
            .Throws<FlightNotFoundException>();
    }

    [Test]
    public async Task Log_reads_the_log_of_the_reference_it_was_given()
    {
        await using var stub = new StubControlPlane();

        var result = await Build(stub).LogAsync("GG-42");

        await Assert.That(stub.ObservedPaths).Contains("/v1/flights/GG-42/log");
        await Assert.That(result).IsTypeOf<VerbResult.Log>();
    }

    [Test]
    public async Task Runners_reads_the_fleet()
    {
        await using var stub = new StubControlPlane();

        var result = await Build(stub).RunnersAsync();

        await Assert.That(stub.ObservedPaths).Contains("/v1/runners");
        await Assert.That(result).IsTypeOf<VerbResult.Runners>();
    }

    [Test]
    public async Task Fly_sends_free_text_as_a_text_intent()
    {
        await using var stub = new StubControlPlane();

        await Build(stub).FlyAsync(text: "fix the login bug", uri: null);

        await Assert.That(stub.ObservedPaths).Contains("/v1/flights");
        await Assert.That(stub.LastBody).Contains("\"kind\":\"text\"");
        await Assert.That(stub.LastBody).Contains("fix the login bug");
    }

    [Test]
    public async Task Fly_sends_a_uri_as_a_uri_intent()
    {
        await using var stub = new StubControlPlane();

        await Build(stub).FlyAsync(text: null, uri: "https://example.invalid/issues/7");

        await Assert.That(stub.LastBody).Contains("\"kind\":\"uri\"");
        await Assert.That(stub.LastBody).Contains("issues/7");
    }

    [Test]
    public async Task Fly_refuses_an_intent_the_contract_would_refuse()
    {
        // Validated by the contract's own rule, so gg and the control plane
        // agree on what an intent is rather than each deciding.
        await using var stub = new StubControlPlane();

        await Assert.That(async () => await Build(stub).FlyAsync(text: "   ", uri: null))
            .Throws<FlightIntentException>();
        await Assert.That(stub.ObservedPaths).IsEmpty()
            .Because("a request the contract already says is malformed should not be sent.");
    }

    [Test]
    public async Task Every_flight_verb_needs_a_session_and_says_so()
    {
        // Not a 401 from the server. A person who is not signed in should be
        // told to sign in, by name.
        await using var stub = new StubControlPlane();
        var signedOut = Build(stub, session: null);

        foreach (var call in (Func<Task>[])
                 [() => signedOut.ListAsync(),
                  () => signedOut.ShowAsync("GG-42"),
                  () => signedOut.LogAsync("GG-42"),
                  () => signedOut.RunnersAsync(),
                  () => signedOut.FlyAsync("words", null)])
        {
            await Assert.That(call).Throws<NotSignedInException>();
        }

        await Assert.That(stub.ObservedPaths).IsEmpty();
    }

    [Test]
    public async Task Every_flight_verb_sends_the_session_header_and_the_version_headers()
    {
        await using var stub = new StubControlPlane();
        var commands = Build(stub);

        await commands.ListAsync();
        await commands.ShowAsync("GG-42");
        await commands.LogAsync("GG-42");
        await commands.RunnersAsync();

        await Assert.That(stub.ObservedHeaders).IsNotEmpty();
        foreach (var headers in stub.ObservedHeaders)
        {
            await Assert.That(headers.ContainsKey(ProtocolSurface.SessionHeader)).IsTrue();
            foreach (var required in ProtocolSurface.VersionHeaders)
            {
                await Assert.That(headers.ContainsKey(required)).IsTrue();
            }
        }
    }

    [Test]
    public async Task Every_path_these_verbs_call_is_a_declared_endpoint()
    {
        // The same conformance the identity verbs are held to. A client
        // calling a path the control plane does not serve is a divergence the
        // declaration exists to catch.
        await using var stub = new StubControlPlane();
        var commands = Build(stub);

        await commands.ListAsync();
        await commands.ShowAsync("GG-42");
        await commands.LogAsync("GG-42");
        await commands.RunnersAsync();
        await commands.FlyAsync("words", null);

        var undeclared = stub.ObservedPaths
            .Where(p => ProtocolSurface.Find("GET", p) is null && ProtocolSurface.Find("POST", p) is null)
            .Distinct()
            .ToList();

        await Assert.That(stub.ObservedPaths).IsNotEmpty();
        await Assert.That(undeclared).IsEmpty()
            .Because($"undeclared: {string.Join(", ", undeclared)}");
    }
}
