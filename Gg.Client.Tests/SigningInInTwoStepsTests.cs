namespace Gg.Client.Tests;

/// <summary>
/// Starting a device authorization and waiting on it are separable, and the
/// verb is the two of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the console cannot use the verb.</b> <c>LoginAsync</c> fetches a
/// code, prints it, and blocks until it is approved — which is exactly right in
/// a terminal and unusable from a console that owns the screen: the code would
/// appear only in whatever was printed before the UI redrew over it. The
/// console needs to be handed the code, draw it, and come back to wait.
/// </para>
/// <para>
/// <b>And there is still ONE polling loop.</b> Two would be the drift this
/// repository keeps finding in pairs of lists that agree until somebody edits
/// one — and the thing they would drift about is the cadence the server asked
/// for, where being wrong earns a rate limit for every client.
/// </para>
/// </remarks>
public class SigningInInTwoStepsTests
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
        public StoredSession? Read() => Stored;
        public void Write(StoredSession session) => Stored = session;
        public void Clear() => Stored = null;
    }

    private static (AuthCommands Commands, RecordingWriter Output, MemorySessionStore Sessions)
        Build(StubControlPlane stub)
    {
        var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var output = new RecordingWriter();
        var sessions = new MemorySessionStore();

        return (
            new AuthCommands(
                new ControlPlaneClient(http), sessions, output,
                new FixedClock(DateTimeOffset.UtcNow),
                (_, _) => Task.CompletedTask),
            output,
            sessions);
    }

    [Test]
    public async Task Starting_answers_with_the_code_and_prints_nothing()
    {
        // PRINTS NOTHING, which is the whole reason this half exists. A console
        // caller draws the code in a modal; anything written to standard output
        // here lands on a terminal Terminal.Gui is about to paint over.
        await using var stub = new StubControlPlane();
        var (commands, output, _) = Build(stub);

        var started = await commands.StartAsync("test-device");

        await Assert.That(started.UserCode).IsEqualTo("WXYZ-1234");
        await Assert.That(started.VerificationUri).IsEqualTo("https://control-plane.invalid/activate");
        await Assert.That(output.Lines).IsEmpty()
            .Because("the caller decides where this is shown; one of them has no terminal to "
                   + "show it on.");
    }

    [Test]
    public async Task Waiting_stores_the_session_and_says_who()
    {
        await using var stub = new StubControlPlane { PendingPolls = 2 };
        var (commands, _, sessions) = Build(stub);

        var result = await commands.AwaitApprovalAsync(await commands.StartAsync("test-device"));

        await Assert.That(result.SignedIn).IsTrue();
        await Assert.That(result.Said).Contains("Signed in as");
        await Assert.That(sessions.Stored?.SessionToken).IsEqualTo(StubControlPlane.IssuedSessionToken);
    }

    [Test]
    public async Task An_authorization_that_is_declined_comes_back_as_a_sentence()
    {
        // Not an exception. Declined, expired and still-pending are ordinary
        // answers to "has this been approved yet", and a caller that had to
        // catch them would invent a policy for each - the policy invented under
        // time pressure being to carry on as though it had worked.
        await using var stub = new StubControlPlane { Declined = true };
        var (commands, _, sessions) = Build(stub);

        var result = await commands.AwaitApprovalAsync(await commands.StartAsync("test-device"));

        await Assert.That(result.SignedIn).IsFalse();
        await Assert.That(result.Said).IsNotEmpty();
        await Assert.That(sessions.Stored).IsNull()
            .Because("nothing was issued, so nothing may be written where a session lives.");
    }

    [Test]
    public async Task There_is_one_polling_loop_and_the_verb_goes_through_it()
    {
        // The cadence the server asked for is decided in exactly one place. A
        // second loop would agree with the first until somebody edited one of
        // them, and polling faster than asked earns a rate limit for every
        // client of this control plane.
        var source = SourceOf("Gg.Client", "AuthCommands.cs");

        var polls = source.Split("PollDeviceAuthorizationAsync").Length - 1;

        await Assert.That(polls).IsEqualTo(1)
            .Because($"the poll is written {polls} times, so the interval, the expiry check "
                   + "and the three answers are too.");
        await Assert.That(source).Contains("AwaitApprovalAsync(started")
            .Because("the verb has to BE the two halves rather than a third copy of them.");
    }

    private static string SourceOf(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }
}
