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

    /// <summary>A writer that says it is a terminal, as the real one does on a tty.</summary>
    private sealed class TerminalWriter : IConsoleWriter
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string line = "") => Lines.Add(line);
        public bool Hyperlinks => true;
        public string All => string.Join("\n", Lines);
    }

    private static Task<int> LoginTo(StubControlPlane stub, IConsoleWriter output) =>
        new AuthCommands(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new MemorySessionStore(), output,
            new FixedClock(DateTimeOffset.UtcNow), new RecordedDelays().Delay).LoginAsync("test-device");

    private const string Complete = "https://control-plane.invalid/activate?code=WXYZ-1234";

    [Test]
    public async Task LoginLinksTheAddressWithTheCodeInIt()
    {
        await using var stub = new StubControlPlane { VerificationUriComplete = Complete };
        var output = new TerminalWriter();

        await LoginTo(stub, output);

        await Assert.That(output.All).Contains($"  Open:  \u001b]8;;{Complete}\u001b\\{Complete}\u001b]8;;\u001b\\")
            .Because("one click should land on a page that already has the code, with nothing to copy.");
        await Assert.That(output.All).Contains("  Code:  WXYZ-1234")
            .Because("the page asks a person to confirm the code, so they still need to see it.");
    }

    [Test]
    public async Task LoginWithoutACompleteAddressLinksTheBareOne()
    {
        await using var stub = new StubControlPlane();
        var output = new TerminalWriter();

        await LoginTo(stub, output);

        await Assert.That(output.All).Contains("\u001b]8;;https://control-plane.invalid/activate\u001b\\")
            .Because("a control plane that predates the field still gets a clickable address.");
    }

    [Test]
    public async Task LoginToAPipeWritesNoEscapes()
    {
        await using var stub = new StubControlPlane { VerificationUriComplete = Complete };
        var output = new RecordingWriter();

        await LoginTo(stub, output);

        await Assert.That(output.All).Contains($"  Open:  {Complete}");
        await Assert.That(output.All).DoesNotContain("\u001b")
            .Because("a pipe or a log shows an escape as junk around the address.");
    }

    [Test]
    public async Task ACompleteAddressCannotCloseTheLinkItIsIn()
    {
        await using var stub = new StubControlPlane
        {
            VerificationUriComplete = "https://control-plane.invalid/a\u001b]8;;\u001b\\https://elsewhere.invalid",
        };
        var output = new TerminalWriter();

        await LoginTo(stub, output);

        await Assert.That(output.All.Split('\u001b').Length - 1).IsEqualTo(4)
            .Because("only the four escapes of our own link may reach the terminal.");
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
            // From the contract, not restated here. A literal would have to be
            // edited every time the vocabulary moves, which is how a test
            // stops asserting that both halves read one number and starts
            // asserting that somebody remembered to change two.
            await Assert.That(headers[GgVersions.FactVocabularyHeader])
                .IsEqualTo(Gg.Contracts.FactVocabulary.Version);
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
