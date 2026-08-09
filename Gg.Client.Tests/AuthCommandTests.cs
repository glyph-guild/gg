namespace Gg.Client.Tests;

/// <summary>
/// The three auth verbs, driven against a stub control plane speaking the
/// published contract. No network beyond loopback, and no provider anywhere.
/// </summary>
public class AuthCommandTests
{
    private sealed class RecordingWriter : IConsoleWriter
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string line = "") => Lines.Add(line);
        public string All => string.Join("\n", Lines);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class MemorySessionStore : ISessionStore
    {
        public StoredSession? Stored { get; private set; }
        public int ClearCount { get; private set; }
        public StoredSession? Read() => Stored;
        public void Write(StoredSession session) => Stored = session;
        public void Clear() { Stored = null; ClearCount++; }
    }

    /// <summary>Records how long the command was asked to wait, without waiting.</summary>
    private sealed class RecordedDelays
    {
        public List<TimeSpan> Waits { get; } = [];
        public Task Delay(TimeSpan span, CancellationToken _)
        {
            Waits.Add(span);
            return Task.CompletedTask;
        }
    }

    private static (AuthCommands Commands, RecordingWriter Output, MemorySessionStore Sessions, RecordedDelays Delays)
        Build(StubControlPlane stub)
    {
        var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var output = new RecordingWriter();
        var sessions = new MemorySessionStore();
        var delays = new RecordedDelays();
        var commands = new AuthCommands(
            new ControlPlaneClient(http), sessions, output,
            new FixedClock(DateTimeOffset.UtcNow), delays.Delay);
        return (commands, output, sessions, delays);
    }

    [Test]
    public async Task LoginShowsTheCodeAndUrlThenStoresTheSession()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, sessions, _) = Build(stub);

        var exit = await commands.LoginAsync("test-device");

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(output.All).Contains("WXYZ-1234")
            .Because("the human cannot approve a code they were never shown.");
        await Assert.That(output.All).Contains("https://control-plane.invalid/activate");
        await Assert.That(sessions.Stored?.SessionToken).IsEqualTo(StubControlPlane.IssuedSessionToken);
    }

    [Test]
    public async Task LoginPollsAtTheServerSuppliedInterval()
    {
        await using var stub = new StubControlPlane { PendingPolls = 3 };
        var (commands, _, _, delays) = Build(stub);

        await commands.LoginAsync("test-device");

        // The stub advertises 1 second; the client must use that rather than a
        // cadence of its own choosing.
        await Assert.That(delays.Waits).IsNotEmpty();
        await Assert.That(delays.Waits.Distinct()).IsEquivalentTo(new[] { TimeSpan.FromSeconds(1) })
            .Because("polling faster than the server asked earns a rate limit for every client.");
    }

    [Test]
    public async Task LoginKeepsPollingWhilePendingIsAnsweredWith202()
    {
        await using var stub = new StubControlPlane { PendingPolls = 2 };
        var (commands, _, sessions, delays) = Build(stub);

        var exit = await commands.LoginAsync("test-device");

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(delays.Waits).Count().IsEqualTo(3)
            .Because("two pending answers then the completion - 202 is a wait, not a failure.");
        await Assert.That(sessions.Stored).IsNotNull();
    }

    [Test]
    public async Task LoginStopsWhenTheAuthorizationIsDeclined()
    {
        await using var stub = new StubControlPlane { Declined = true };
        var (commands, output, sessions, _) = Build(stub);

        var exit = await commands.LoginAsync("test-device");

        await Assert.That(exit).IsEqualTo(1);
        await Assert.That(sessions.Stored).IsNull()
            .Because("a declined authorization must not leave a session behind.");
        await Assert.That(output.All).Contains("expired or was declined");
    }

    [Test]
    public async Task EveryRequestCarriesAllThreeVersionHeaders()
    {
        await using var stub = new StubControlPlane();
        var (commands, _, _, _) = Build(stub);

        await commands.LoginAsync("test-device");

        await Assert.That(stub.ObservedHeaders).IsNotEmpty();
        foreach (var headers in stub.ObservedHeaders)
        {
            await Assert.That(headers.ContainsKey(GgVersions.ProtocolHeader)).IsTrue();
            await Assert.That(headers.ContainsKey(GgVersions.RunnerVersionHeader)).IsTrue();
            await Assert.That(headers.ContainsKey(GgVersions.FactVocabularyHeader)).IsTrue()
                .Because("the fact-vocabulary version is the one nobody remembers to send.");
            await Assert.That(headers[GgVersions.ProtocolHeader]).IsEqualTo("1");
            await Assert.That(headers[GgVersions.FactVocabularyHeader]).IsEqualTo("0.1.0");
        }
    }

    [Test]
    public async Task LogoutRevokesServerSideBeforeDeletingLocally()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, sessions, _) = Build(stub);
        await commands.LoginAsync("test-device");

        var exit = await commands.LogoutAsync();

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(stub.RevokedTokens).Contains(StubControlPlane.IssuedSessionToken)
            .Because("a local delete that leaves a live server session is a lie.");
        await Assert.That(sessions.Stored).IsNull();
        await Assert.That(output.All).Contains("Signed out");
    }

    [Test]
    public async Task ARevokedSessionIsRefusedAfterwards()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, sessions, _) = Build(stub);
        await commands.LoginAsync("test-device");
        await commands.LogoutAsync();

        // Put the revoked token back to prove the SERVER refuses it, not just
        // that the local file was removed.
        sessions.Write(new StoredSession
        {
            SessionToken = StubControlPlane.IssuedSessionToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            TenantId = "t",
            PrincipalDisplay = "stub-principal",
        });

        var exit = await commands.WhoAmIAsync();

        await Assert.That(exit).IsEqualTo(1);
        await Assert.That(output.All).Contains("no longer valid");
    }

    [Test]
    public async Task LogoutKeepsTheLocalSessionWhenRevocationFails()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, sessions, _) = Build(stub);
        await commands.LoginAsync("test-device");

        // Revocation now fails: the control plane refuses everything.
        stub.ProtocolFloorMessage = "supported protocol versions: 2-3";

        var exit = await commands.LogoutAsync();

        await Assert.That(exit).IsEqualTo(1);
        await Assert.That(sessions.ClearCount).IsEqualTo(0)
            .Because("deleting locally after a failed revoke leaves a live session nobody can revoke.");
        await Assert.That(output.All).Contains("kept");
    }

    [Test]
    public async Task WhoAmIReportsPrincipalTenantAndExpiry()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, _, _) = Build(stub);
        await commands.LoginAsync("test-device");

        var exit = await commands.WhoAmIAsync();

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(output.All).Contains("stub-principal");
        await Assert.That(output.All).Contains("019fe062-d000-730c-a37d-7247342cd810");
        await Assert.That(output.All).Contains("Expires:");
    }

    [Test]
    public async Task WhoAmIWithoutASessionSaysSoRatherThanFailing()
    {
        await using var stub = new StubControlPlane();
        var (commands, output, _, _) = Build(stub);

        var exit = await commands.WhoAmIAsync();

        await Assert.That(exit).IsEqualTo(1);
        await Assert.That(output.All).Contains("gg login");
    }

    [Test]
    public async Task ARequestBelowTheProtocolFloorIsSurfacedActionably()
    {
        await using var stub = new StubControlPlane { ProtocolFloorMessage = "supported protocol versions: 2-3" };
        var (commands, _, _, _) = Build(stub);

        var refusal = await Assert.ThrowsAsync<ProtocolTooOldException>(
            async () => await commands.LoginAsync("test-device"));

        await Assert.That(refusal!.Message).Contains("too old");
        await Assert.That(refusal.Message).Contains("2-3")
            .Because("a refusal that does not name the supported range leaves the developer guessing.");
    }
}
