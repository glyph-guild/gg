namespace Gg.Console.Tests;

/// <summary>
/// The offer is answered by the shell, in two steps, with the terminal free.
/// </summary>
/// <remarks>
/// <para>
/// <b>A UI session may read a local file and nothing else</b>, and a device
/// authorization is two network calls and a credential written to disk. So the
/// session ends, the loop asks, and the next session is rebuilt from the model
/// - the shape <c>$EDITOR</c>, the takeover and the browser already have.
/// </para>
/// <para>
/// <b>Two steps rather than one, because the code has to be READ.</b> Starting
/// and waiting in a single arm would fetch the code and immediately block on
/// approval, so the only place a person could see what to type is whatever the
/// shell printed before the console redrew over it. Starting first puts the
/// code in the model, where the modal draws it; the second press says it has
/// been approved.
/// </para>
/// <para>
/// <b>And the two are different keys on purpose.</b> ConfirmFlight's rule, for
/// the same reason: one key for both would let somebody press it twice in a
/// second and arrive at "waiting for you to approve" having never been shown
/// the code they are meant to approve.
/// </para>
/// </remarks>
public class SigningInIsTheShellsWorkTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 9, 6, 14, 32, 0, TimeSpan.Zero);

    private static PendingSignIn Pending() => new()
    {
        UserCode = "WDJB-MJHT",
        VerificationUri = "https://example.test/device",
        ExpiresAt = Expiry,
    };

    /// <summary>A console showing the offer, the way the loader leaves it.</summary>
    private static AppState Offered() => new() { Mode = UiMode.SignIn };

    /// <summary>
    /// A sign-in that answers whatever it is told to, and records which half
    /// was asked.
    /// </summary>
    /// <remarks>
    /// <b>Waiting before anything is started throws</b> rather than answering.
    /// Which half the loop calls is the whole subject here, and a double that
    /// answered both the same way would pass whichever one it called.
    /// </remarks>
    private sealed class Signs(SignInStep started, SignInStep? waited = null) : ISignInSession
    {
        internal int Starts { get; private set; }

        internal int Waits { get; private set; }

        public SignInStep Start()
        {
            Starts++;
            return started;
        }

        public SignInStep Wait()
        {
            Waits++;
            return waited ?? throw new InvalidOperationException(
                "waited on an authorization this test never started.");
        }
    }

    private static AppState Ran(AppState from, ISignInSession? signIn, ConsoleDoubles.Reloads? reload = null) =>
        new ConsoleLoop(
            new ConsoleDoubles.TypesKeys(Command.SignIn),
            new ConsoleDoubles.NoEditor(),
            reload: reload is null ? null : reload.Load,
            signIn: signIn)
            .Run(from);

    [Test]
    public async Task The_offer_is_a_key_the_shell_handles()
    {
        // Bound, advertised, and inert is the shape this console has hit four
        // times. The declaration is what the screen and the loop both read.
        await Assert.That(ShellCommands.Handled).Contains(Command.SignIn)
            .Because("it makes two network calls and writes a credential, so it cannot "
                   + "happen inside a UI session.");

        var offered = Keymap.Bindings(new KeymapContext(UiMode.SignIn));

        await Assert.That(offered.Any(b => b.Command == Command.SignIn)).IsTrue()
            .Because("a modal that states a problem and offers no way to fix it is a "
                   + "diagnosis with a border drawn round it.");
    }

    [Test]
    public async Task Approving_is_not_the_key_that_asked()
    {
        // ConfirmFlight's rule. The second press means "I have approved it in a
        // browser", and somebody who reached it by pressing the first key twice
        // has approved nothing - they would be waiting on a code they were
        // never shown.
        var asks = Keymap.Bindings(new KeymapContext(UiMode.SignIn))
            .Single(b => b.Command == Command.SignIn);

        var approves = Keymap.Bindings(new KeymapContext(UiMode.SignIn) { SignInStarted = true })
            .Single(b => b.Command == Command.SignIn);

        await Assert.That(approves.Key).IsNotEqualTo(asks.Key)
            .Because("one key for both is a double-press away from waiting on a code "
                   + "nobody read.");

        await Assert.That(approves.Description).IsNotEqualTo(asks.Description)
            .Because("the hint line is the only thing telling a person what the key means "
                   + "now, and the two steps ask for opposite things.");
    }

    [Test]
    public async Task Starting_puts_what_a_person_must_do_in_the_model()
    {
        var signIn = new Signs(new SignInStep { Pending = Pending(), Said = "Waiting on you." });

        var after = Ran(Offered(), signIn);

        await Assert.That(signIn.Starts).IsEqualTo(1);
        await Assert.That(after.SignIn?.UserCode).IsEqualTo("WDJB-MJHT");
        await Assert.That(after.SignIn?.VerificationUri).IsEqualTo("https://example.test/device");
        await Assert.That(after.Mode).IsEqualTo(UiMode.SignIn)
            .Because("nothing has been approved yet, so the modal stays and shows the code.");
    }

    [Test]
    public async Task The_polling_handle_is_not_in_the_model()
    {
        // THE ONE VALUE IN THIS FLOW THAT IS A CREDENTIAL. Whoever holds the
        // device code can poll for the session token and be handed it, so it is
        // a bearer capability exactly like the invitation link - and AppState is
        // source-generated JSON written to disk under GG_STATE_DUMP and fed to
        // the diagnostics bundle. It lives on the session object outside the
        // model, which is where the live tails and the process handles already
        // are.
        var members = typeof(PendingSignIn).GetProperties().Select(p => p.Name).Order().ToList();

        await Assert.That(members)
            .IsEquivalentTo((string[])["ExpiresAt", "UserCode", "VerificationUri"])
            .Because("these three are what a person READS off a screen. A fourth member is "
                   + "how the handle gets into a bundle somebody emails us. Found: "
                   + string.Join(", ", members));
    }

    [Test]
    public async Task A_second_press_waits_rather_than_starting_over()
    {
        // Starting again would abandon the code on the screen and fetch another,
        // so a person who pressed approve a moment early would be handed a new
        // code every time they tried - and the one they had already approved
        // would be the one nobody was polling.
        var signIn = new Signs(
            new SignInStep { Pending = Pending(), Said = "started" },
            new SignInStep { SignedIn = true, Said = "Signed in as somebody." });

        var after = Ran(Offered() with { SignIn = Pending() }, signIn, new ConsoleDoubles.Reloads(new AppState()));

        await Assert.That(signIn.Starts).IsEqualTo(0);
        await Assert.That(signIn.Waits).IsEqualTo(1);
        await Assert.That(after.LastSignIn).IsEqualTo("Signed in as somebody.");
    }

    [Test]
    public async Task Signing_in_closes_the_modal_and_reads_what_it_could_not()
    {
        // Rule 4: a write refreshes what it invalidated, and this one
        // invalidated every read the console makes. The modal is over the
        // console it exists because of, so leaving it open would hide the queue
        // that just became fetchable.
        var reload = new ConsoleDoubles.Reloads(new AppState { Principal = "somebody" });

        var after = Ran(
            Offered() with { SignIn = Pending() },
            new Signs(
                new SignInStep { Said = "started" },
                new SignInStep { SignedIn = true, Said = "Signed in as somebody." }),
            reload);

        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(after.SignIn).IsNull()
            .Because("a code that has been used is a code nobody should be reading.");
        await Assert.That(reload.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task An_authorization_nobody_approved_returns_to_the_offer()
    {
        // Expired, declined, or pressed too early. All three are the same fact
        // to the person - it did not work - and all three leave them somewhere
        // they can try again rather than in a modal holding a code that has
        // stopped meaning anything.
        var after = Ran(
            Offered() with { SignIn = Pending() },
            new Signs(
                new SignInStep { Said = "started" },
                new SignInStep { Said = "That code expired before it was approved." }));

        await Assert.That(after.Mode).IsEqualTo(UiMode.SignIn);
        await Assert.That(after.SignIn).IsNull()
            .Because("the next press has to START one rather than wait on a dead code.");
        await Assert.That(after.LastSignIn).IsEqualTo("That code expired before it was approved.");
    }

    [Test]
    public async Task A_console_that_cannot_sign_in_says_so()
    {
        // The port is optional, which is how the takeover keys answered "not
        // configured" on every real press for two slices. A key that reaches its
        // arm and returns the state unchanged is indistinguishable from one that
        // is not bound at all.
        var after = Ran(Offered(), signIn: null);

        await Assert.That(after.LastSignIn).IsNotNull();
        await Assert.That(after.Mode).IsEqualTo(UiMode.SignIn)
            .Because("nothing has changed about being signed out.");
    }

    [Test]
    public async Task What_it_said_reaches_the_line_a_person_reads()
    {
        // Every arm records its outcome in its own field and Said takes
        // whichever moved. An arm missing from that derivation does its work in
        // silence, which is the same thing to a person as doing nothing.
        var before = Offered();
        var after = before with { LastSignIn = "Signed in as somebody." };

        await Assert.That(ConsoleLoop.Said(before, after)).IsEqualTo("Signed in as somebody.");
    }

    [Test]
    public async Task Being_refused_writes_the_reason_and_the_mode_and_nothing_else()
    {
        // The ratchet next door, for the failure it does not cover. A refusal
        // is entitled to say why and to open the modal that answers it; every
        // other field is still the person's, including the queue they were
        // looking at when their session ran out.
        for (var seed = 1; seed <= 12; seed++)
        {
            var held = StateGenerator.Next(new Random(seed));

            var after = await ConsoleStart.LoadAsync(
                SigningInFromTheConsoleTests.SignedOut(), held.Principal, held);

            var expected = held with { Diagnosis = after.Diagnosis, Mode = UiMode.SignIn };

            await Assert.That(after).IsEqualTo(expected)
                .Because($"seed {seed}: being signed out says why and offers the way back, "
                       + "and knows nothing about any other field.");
        }
    }
}
