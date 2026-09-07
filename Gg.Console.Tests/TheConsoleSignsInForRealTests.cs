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

    /// <summary>Written as escapes, because a raw one in source is invisible.</summary>
    private const string Esc = "\u001b";

    private const string Bel = "\u0007";

    private static DeviceAuthorizationStarted Authorization() => new()
    {
        DeviceCode = Handle,
        UserCode = "WDJB-MJHT",
        VerificationUri = "https://example.test/device",
        PollIntervalSeconds = 1,
        ExpiresAt = Expiry,
    };

    /// <summary>Runs the wait on the spot, so no test ever waits for a task.</summary>
    /// <remarks>
    /// The wait is handed to a runner rather than to <c>Task.Run</c> precisely
    /// so these can drive it; what is under test here is which value crosses
    /// which boundary, not that .NET can schedule work.
    /// </remarks>
    private static Task<SignInStep> AtOnce(Func<SignInStep> work) =>
        Task.FromResult(work());

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
        // THE POINT OF THE WHOLE ARRANGEMENT. The loop asks Arrived() with no
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
            },
            AtOnce);

        session.Start();
        var step = session.Arrived()!;

        await Assert.That(polled).IsEquivalentTo((string[])[Handle]);
        await Assert.That(step.SignedIn).IsTrue();
        await Assert.That(step.Said).IsEqualTo("Signed in as somebody.");
        await Assert.That(step.Pending).IsNull()
            .Because("a code that has been used is a code nobody should still be reading.");
    }

    [Test]
    public async Task Asking_before_anything_started_says_nothing_rather_than_throwing()
    {
        // The console and this object each track whether something is pending -
        // one to choose a key, one to hold a handle - and an exception here
        // would make any disagreement between them a crash in the shell rather
        // than a sentence in the modal. This is asked once a second, so it is
        // also the call most likely to find them disagreeing.
        var session = new SignInSession(
            Authorization,
            _ => throw new InvalidOperationException("nothing was started, so nothing may poll."),
            AtOnce);

        await Assert.That(session.Arrived()).IsNull()
            .Because("nothing was started, so nothing has landed - and the screen reads that "
                   + "as 'keep the modal up', which is exactly right.");
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
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            AtOnce);

        session.Start();

        await Assert.That(session.Arrived()!.SignedIn).IsTrue();

        await Assert.That(session.Arrived()).IsNull()
            .Because("the answer was handed to the model, which is where it lives now. "
                   + "Holding it would have the screen end a second session over the top "
                   + "of the console the first one just signed in.");
    }

    [Test]
    public async Task Nothing_is_printed_at_all_because_the_modal_never_leaves()
    {
        // WHAT THIS REPLACED, AND WHY IT HAD TO GO. Waiting used to print the
        // code and "waiting for you to approve it", which was right while it
        // ran between UI lifetimes: the UI session had ended to get there and
        // the screen was provably nobody's. The wait now runs WHILE the modal
        // is drawing the code, so the same WriteLine would go through the
        // middle of what Terminal.Gui is painting - and the person can still
        // read the code, because the modal never went away.
        var source = ConsoleSource.Text("Gg.Console", "SignInSession.cs");

        await Assert.That(source).DoesNotContain("WriteLine")
            .Because("there is no longer any moment in this flow when the terminal is ours.");

        await Assert.That(source).DoesNotContain("ShowCode")
            .Because("the modal shows the code; printing it underneath is a second copy on a "
                   + "screen somebody else is painting.");
    }

    /// <summary>A code and an address with a screen-clear and a title-set in them.</summary>
    /// <remarks>
    /// Shaped like what would actually arrive: values a control plane composes,
    /// carrying sequences a terminal ACTS on rather than prints. The first
    /// wipes the screen; the second retitles the window and never terminates,
    /// which is how a crafted string takes the rest of a line with it.
    /// </remarks>
    private static DeviceAuthorizationStarted Crafted() => Authorization() with
    {
        UserCode = Esc + "[2JWDJB-MJHT",
        VerificationUri = "https://example.test/device" + Esc + "]0;owned" + Bel,
    };

    [Test]
    public async Task Text_from_the_control_plane_is_stripped_before_it_is_stored()
    {
        // AT INGRESS, WHICH IS HERE. The console's rule is that external text is
        // cleaned before storage rather than at render, because the model is
        // written to disk, handed to the diagnostics bundle, and read back by
        // things that are not PaneText. This is the doorway those two values
        // come through.
        var step = new SignInSession(Crafted, _ => new SignInResult { Said = "" }, AtOnce).Start();

        await Assert.That(step.Pending!.UserCode).DoesNotContain(Esc);
        await Assert.That(step.Pending!.VerificationUri).DoesNotContain(Esc);
        await Assert.That(step.Pending!.UserCode).Contains("WDJB-MJHT")
            .Because("stripping removes the sequence, not the code a person has to type.");
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
