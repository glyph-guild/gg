using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The session object that holds the device code, and the root that wires it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where the polling handle stops.</b> The control plane hands back
/// a device code and four things a person reads; only the four cross into the
/// model. The handle stays in a field here, which is why <c>Wait</c> takes no
/// argument — there is nothing for the model to give back, because the model
/// was never given it.
/// </para>
/// <para>
/// <b>Driven with delegates, not HTTP.</b> What is in question is which value
/// crosses which boundary; a test that stood up a control plane would prove
/// that and a great deal else, and would prove it about the wrong layer.
/// </para>
/// </remarks>
public class TheConsoleSignsInForRealTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 9, 6, 14, 32, 0, TimeSpan.Zero);

    private const string Handle = "the-device-code-nobody-else-may-hold";

    private static DeviceAuthorizationStarted Authorization() => new()
    {
        DeviceCode = Handle,
        UserCode = "WDJB-MJHT",
        VerificationUri = "https://example.test/device",
        PollIntervalSeconds = 1,
        ExpiresAt = Expiry,
    };

    [Test]
    public async Task Starting_hands_the_model_what_a_person_reads_and_keeps_the_handle()
    {
        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." });

        var step = session.Start();

        await Assert.That(step.Pending?.UserCode).IsEqualTo("WDJB-MJHT");
        await Assert.That(step.Pending?.VerificationUri).IsEqualTo("https://example.test/device");
        await Assert.That(step.Pending?.ExpiresAt).IsEqualTo(Expiry);
        await Assert.That(step.SignedIn).IsFalse()
            .Because("nothing has been approved; a code has only been asked for.");
    }

    [Test]
    public async Task Waiting_polls_the_authorization_this_session_started()
    {
        // THE POINT OF THE WHOLE ARRANGEMENT. The loop calls Wait() with no
        // arguments, so the handle can only have come from here - which is what
        // lets it stay out of a record that is written to disk and mailed to us
        // in a bundle.
        var polled = new List<string>();

        var session = new SignInSession(
            Authorization,
            started =>
            {
                polled.Add(started.DeviceCode);
                return new SignInResult { SignedIn = true, Said = "Signed in as somebody." };
            });

        session.Start();
        var step = session.Wait();

        await Assert.That(polled).IsEquivalentTo((string[])[Handle]);
        await Assert.That(step.SignedIn).IsTrue();
        await Assert.That(step.Said).IsEqualTo("Signed in as somebody.");
        await Assert.That(step.Pending).IsNull()
            .Because("a code that has been used is a code nobody should still be reading.");
    }

    [Test]
    public async Task Waiting_on_nothing_says_so_rather_than_throwing()
    {
        // The console and this object each track whether something is pending -
        // one to choose a key, one to hold a handle - and an exception here
        // would make any disagreement between them a crash in the shell rather
        // than a sentence in the modal.
        var session = new SignInSession(
            Authorization,
            _ => throw new InvalidOperationException("nothing was started, so nothing may poll."));

        var step = session.Wait();

        await Assert.That(step.Said).IsNotEmpty();
        await Assert.That(step.SignedIn).IsFalse();
        await Assert.That(step.Pending).IsNull();
    }

    [Test]
    public async Task A_control_plane_that_cannot_be_reached_is_a_sentence_too()
    {
        // The console is drawn over the terminal. An exception out of here ends
        // the process and takes the screen with it, and the person is left
        // looking at a stack trace where their queue was.
        var session = new SignInSession(
            () => throw new HttpRequestException("Connection refused"),
            _ => new SignInResult { Said = "unreachable" });

        var step = session.Start();

        await Assert.That(step.Pending).IsNull();
        await Assert.That(step.Said).Contains("Connection refused");
    }

    [Test]
    public async Task The_handle_is_let_go_of_once_it_has_been_used()
    {
        // A device code is spent when the authorization resolves. Holding it
        // would let a second press poll a completed authorization, and would
        // keep a credential alive in a long-running process for no reason.
        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." });

        session.Start();
        session.Wait();

        await Assert.That(session.Wait().SignedIn).IsFalse()
            .Because("there is nothing left to poll, and saying so beats polling a spent code.");
    }

    private sealed class RecordingWriter : IConsoleWriter
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string line = "") => Lines.Add(line);
        public string All => string.Join("\n", Lines);
    }

    [Test]
    public async Task Waiting_says_what_it_is_waiting_for_because_the_modal_has_gone()
    {
        // THE MOMENT THE CODE LEAVES THE SCREEN. The UI session ends before the
        // shell runs, so the modal that was drawing the code is torn down - and
        // this call then blocks until somebody approves or the code expires.
        // Anybody who pressed approve a moment early is left looking at a blank
        // terminal with nothing on it to approve, and their only move is to
        // interrupt the process.
        var output = new RecordingWriter();

        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { Said = "That code expired before it was approved." },
            output);

        session.Start();
        session.Wait();

        await Assert.That(output.All).Contains("WDJB-MJHT");
        await Assert.That(output.All).Contains("https://example.test/device");
        await Assert.That(output.All).Contains("Waiting");
    }

    [Test]
    public async Task Starting_prints_nothing_because_the_console_is_about_to_redraw()
    {
        // The other half of the same rule. Start returns to the loop, which
        // opens a new UI session immediately - so anything written here is
        // painted over within milliseconds by the modal that draws it properly.
        var output = new RecordingWriter();

        new SignInSession(Authorization, _ => new SignInResult { Said = "" }, output).Start();

        await Assert.That(output.Lines).IsEmpty();
    }

    [Test]
    public async Task The_composition_root_passes_one()
    {
        // The takeover's ports were optional constructor arguments only tests
        // ever supplied, so the console answered "not configured" on every real
        // press for two whole slices. This is the same shape and would fail the
        // same way - and the arm says so out loud, which is exactly what makes
        // it survivable and invisible.
        var root = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(root).Contains("signIn:")
            .Because("available is not wired, and a sign-in modal whose only key answers "
                   + "'this console is not configured to sign in' is worse than no modal.");
        await Assert.That(root).Contains("new SignInSession(");
    }
}
