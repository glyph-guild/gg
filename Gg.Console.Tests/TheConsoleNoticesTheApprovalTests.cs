using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Approving in a browser is the whole of signing in; coming back to press a
/// key is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this replaced.</b> Signing in was two presses: <c>y</c> fetched a
/// code, and <c>a</c> — "I have approved it" — was what actually polled for it.
/// The second press existed because the poll BLOCKED: it ran in the shell
/// between UI lifetimes, so it could not be started while a modal was on the
/// screen. A person who approved in their browser and came back to a console
/// that had not noticed read that as a sign-in that did not work, which is what
/// it was indistinguishable from.
/// </para>
/// <para>
/// <b>The exception this rests on is <see cref="AutoRefresh"/>'s, argued
/// there.</b> The poll runs on a task owned outside every UI lifetime and the
/// session only ever asks whether it has finished. Nothing here waits, so the
/// keyboard is never frozen for as long as a person takes to find their browser
/// — which is the thing the rule against reading in a session protects.
/// </para>
/// <para>
/// <b>Driven through an injected runner rather than a clock.</b> What is in
/// question is that the wait is handed over at all and that asking is
/// non-blocking; a test that let a real task run would be asserting that by
/// waiting, which is the one thing this must never do.
/// </para>
/// </remarks>
public class TheConsoleNoticesTheApprovalTests
{
    private static DeviceAuthorizationStarted Authorization() => new()
    {
        DeviceCode = "the-device-code-nobody-else-may-hold",
        UserCode = "WDJB-MJHT",
        VerificationUri = "https://example.test/device",
        PollIntervalSeconds = 1,
        ExpiresAt = new DateTimeOffset(2026, 9, 7, 14, 32, 0, TimeSpan.Zero),
    };

    /// <summary>A runner that hands back the work instead of running it.</summary>
    private sealed class HeldRunner
    {
        private readonly TaskCompletionSource<SignInStep> _gate = new();

        public Func<SignInStep>? Work { get; private set; }

        public Task<SignInStep> Run(Func<SignInStep> work)
        {
            Work = work;
            return _gate.Task;
        }

        /// <summary>Runs what was handed over, as a real task eventually would.</summary>
        public void Land() => _gate.SetResult(Work!());
    }

    [Test]
    public async Task The_poll_starts_with_the_code_rather_than_with_a_second_press()
    {
        var runner = new HeldRunner();

        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            runner.Run);

        var step = session.Start();

        await Assert.That(step.Pending?.UserCode).IsEqualTo("WDJB-MJHT")
            .Because("the code still has to reach the modal; what changes is who waits for it.");

        await Assert.That(runner.Work).IsNotNull()
            .Because("the authorization is polled from the moment the code is on the screen. "
                   + "A poll that begins on a keypress cannot notice an approval, because "
                   + "noticing is the thing the keypress was standing in for.");
    }

    [Test]
    public async Task Asking_before_it_lands_answers_nothing_rather_than_waiting()
    {
        var runner = new HeldRunner();

        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            runner.Run);

        session.Start();

        await Assert.That(session.Arrived()).IsNull()
            .Because("this is asked once a second from inside a UI session. Answering by "
                   + "waiting would freeze the keyboard for as long as a person takes to "
                   + "find their browser, which is AutoRefresh's rule and the reason the "
                   + "poll is on a task at all.");
    }

    [Test]
    public async Task What_the_browser_approved_is_what_the_console_reads()
    {
        var runner = new HeldRunner();

        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as Kevin Deenanauth." },
            runner.Run);

        session.Start();
        runner.Land();

        var arrived = session.Arrived();

        await Assert.That(arrived).IsNotNull();
        await Assert.That(arrived!.SignedIn).IsTrue();
        await Assert.That(arrived.Said).Contains("Kevin Deenanauth");
        await Assert.That(arrived.Pending).IsNull()
            .Because("there is nothing left for a person to do, and a modal still drawing a "
                   + "code is a modal saying the opposite.");
    }

    [Test]
    public async Task A_code_that_expired_lands_as_a_sentence_and_offers_the_key_again()
    {
        // Every way out of the wait returns to the offer: approved, declined,
        // expired, unreachable. The one that must not happen is a modal drawing
        // a dead code with nothing watching it any more.
        var runner = new HeldRunner();

        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { Said = "That code expired before it was approved." },
            runner.Run);

        session.Start();
        runner.Land();

        var arrived = session.Arrived();

        await Assert.That(arrived!.SignedIn).IsFalse();
        await Assert.That(arrived.Said).Contains("expired");
        await Assert.That(arrived.Pending).IsNull()
            .Because("the code is spent, so the modal falls back to the offer rather than "
                   + "drawing something nobody can approve.");
    }

    [Test]
    public async Task Nothing_is_written_to_a_terminal_the_console_is_drawn_on()
    {
        // THE HALF THAT BECAME UNSAFE. Waiting used to print the code and
        // "waiting for you to approve it", which was right when the UI session
        // had ended to get here: the screen was provably nobody's. The poll now
        // runs WHILE the modal is up, and a WriteLine into a terminal
        // Terminal.Gui is painting is a line through the middle of it.
        var source = ConsoleSource.Text("Gg.Console", "SignInSession.cs");

        await Assert.That(source).DoesNotContain("IConsoleWriter")
            .Because("the one path that wrote straight to the terminal is gone, and a port "
                   + "left wired is a port something writes through again.");
    }

    [Test]
    public async Task No_key_says_it_has_been_approved()
    {
        var showing = Keymap.Bindings(new KeymapContext(UiMode.SignIn) { SignInStarted = true });

        await Assert.That(showing.Any(b => b.Command == Command.SignIn)).IsFalse()
            .Because("the console is watching for the approval, so a key that means 'I have "
                   + "approved it' is a key that does what would have happened anyway - and "
                   + "one somebody presses and waits on when the answer is already coming.");

        var offered = Keymap.Bindings(new KeymapContext(UiMode.SignIn));

        await Assert.That(offered.Any(b => b.Command == Command.SignIn)).IsTrue()
            .Because("asking for a code is still a press; it is only approving that is not.");
    }

    [Test]
    public async Task The_session_is_told_how_to_notice()
    {
        // EveryPortIsPassedTests' shape. The screen cannot ask a session it was
        // never handed, and a console that polls in the background while nothing
        // looks at the answer is exactly the console this replaced.
        var root = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(root).Contains("Arrived")
            .Because("the composition root is the only place that holds both the session "
                   + "doing the polling and the screen that has to notice it land.");
    }
}
