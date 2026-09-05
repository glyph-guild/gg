using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Opening a flight, registering a credential and issuing an invitation, from the
/// console.
/// </summary>
/// <remarks>
/// <para>
/// <b>All three are writes, and writes belong to the shell.</b> They are performed
/// between UI lifetimes with the terminal provably free - the same arrangement
/// <c>$EDITOR</c> has always used - which is what lets them prompt without a modal
/// and therefore without adding a keyboard path that has to be proven escapable.
/// </para>
/// <para>
/// <b>Two of the three carry something that must not be stored.</b> A credential's
/// value and an invitation link are both capabilities: whoever holds the link
/// becomes a principal in the tenant. <c>AppState</c> is source-generated JSON that
/// is written to disk whenever <c>GG_STATE_DUMP</c> is set and is handed to the
/// diagnostics bundle, so neither value may reach it - and the tests that matter
/// most here are the ones that check the failure path, because a diagnostic is
/// where a secret leaks.
/// </para>
/// </remarks>
public class ConsoleWriteVerbsTests
{
    private static ConsoleData Unreachable()
    {
        // A control plane that is not there. Every call fails, which is the case
        // worth testing: the sentence a person is shown after a failure is the one
        // most likely to have something in it that should not be.
        var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var client = new ControlPlaneClient(http);
        var sessions = new HeldSession();

        return new ConsoleData(
            new FlightCommands(client, sessions),
            new CredentialCommands(client, sessions, new NoStore(), new Answers("s3cret-value")),
            new TakeCommands(client, sessions),
            new IdentityCommands(client, sessions),
            new EnvelopeCommands(client, sessions));
    }

    [Test]
    public async Task All_three_commands_are_the_shell_s_work()
    {
        foreach (var command in (Command[])
            [Command.OpenFlight, Command.AddCredential, Command.Invite])
        {
            await Assert.That(ShellCommands.Handled).Contains(command)
                .Because($"{command} talks to the control plane, and the reducer is pure - so its "
                       + "effect belongs where the terminal is free.");
        }
    }

    [Test]
    public async Task All_three_are_bound_in_normal_mode_and_nowhere_else()
    {
        var normal = new KeymapContext(UiMode.Normal);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('n'), normal)).IsEqualTo(Command.OpenFlight);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), normal))
            .IsEqualTo(Command.AddCredential);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('i'), normal)).IsEqualTo(Command.Invite);

        // NOT INSIDE A MODAL. A modal has the keyboard while it is open, and a
        // tenant-level write reachable from a gate decision would be a key doing
        // something unrelated to the question on the screen.
        foreach (var mode in (UiMode[])[UiMode.Help, UiMode.GateDecision, UiMode.FlightActions])
        {
            var context = new KeymapContext(mode);

            foreach (var key in (char[])['n', 'c', 'i'])
            {
                await Assert.That(Keymap.Resolve(KeyStroke.Char(key), context)).IsNull()
                    .Because($"'{key}' must mean nothing while a modal holds the keyboard.");
            }
        }
    }

    [Test]
    public async Task The_credential_secret_never_reaches_the_console_at_all()
    {
        // THE STRONGEST FORM OF THIS, and it is available because of where the
        // prompt lives: CredentialCommands reads the value itself, so the secret is
        // never a parameter, a return value or a local anywhere in Gg.Console. The
        // port says so by having nowhere to put one.
        var add = typeof(IConsoleActions).GetMethod(nameof(IConsoleActions.AddCredential))!;

        await Assert.That(add.GetParameters()).IsEmpty()
            .Because("a secret passed across this boundary is a secret in a stack frame the "
                   + "console owns, and the console is the thing that serializes itself to disk.");
        await Assert.That(add.ReturnType).IsEqualTo(typeof(string))
            .Because("what comes back is the sentence a person reads, and a string is the only "
                   + "shape that cannot accidentally carry the reference's value.");
    }

    [Test]
    public async Task A_failed_credential_registration_says_so_without_saying_the_secret()
    {
        // THE LEAK PATH THAT MATTERS. An exception message is where a value ends up
        // when nobody was thinking about it, and this one is built from a refusal
        // against a control plane that is not there.
        var actions = new VerbConsoleActions(Unreachable(), new Answers("s3cret-value"));

        var said = actions.AddCredential();

        await Assert.That(said).DoesNotContain("s3cret-value")
            .Because("the sentence is shown on a screen, kept in AppState, dumped under "
                   + "GG_STATE_DUMP and handed to the diagnostics bundle.");
        await Assert.That(said).IsNotEmpty()
            .Because("and it still has to say that nothing was registered, or the key looks like "
                   + "it worked.");
    }

    [Test]
    public async Task The_state_the_console_keeps_holds_no_secret_after_a_registration()
    {
        // The same claim one layer out, over the serialized model rather than the
        // sentence - because the sentence is only one of the places it could land.
        var loop = new ConsoleLoop(
            new PressesThen(Command.AddCredential),
            new NoEditing(),
            actions: new VerbConsoleActions(Unreachable(), new Answers("s3cret-value")));

        var after = loop.Run(new AppState());

        await Assert.That(AppStateJson.Serialize(after)).DoesNotContain("s3cret-value");
    }

    [Test]
    public async Task An_invitation_link_is_placed_rather_than_kept()
    {
        // WHOEVER HOLDS THE LINK BECOMES A PRINCIPAL, so it is a capability and
        // belongs in the same category as the secret. What the model records is
        // WHERE it went; the URL itself goes to the clipboard or to a named file,
        // which is exactly what SeedPlacer was built for one slice ago.
        var invite = typeof(IConsoleActions).GetMethod(nameof(IConsoleActions.Invite))!;

        await Assert.That(invite.ReturnType).IsEqualTo(typeof(string));

        var source = Sources.Read("Gg.Console", "VerbConsoleActions.cs");

        await Assert.That(source).Contains("SeedPlacer")
            .Because("clipboard first, a named file otherwise, never failing - a link a person "
                   + "cannot get at is worse than no link.");
        await Assert.That(source).DoesNotContain("return issued.InvitationUrl")
            .Because("returning it would put it in AppState, which is dumped and bundled.");
    }

    [Test]
    public async Task Opening_a_flight_with_nothing_typed_opens_nothing()
    {
        // A flight opened by accident is worse than one not opened: it is a record
        // somebody has to explain and a number that is now taken.
        var loop = new ConsoleLoop(
            new PressesThen(Command.OpenFlight),
            new NoEditing(),
            actions: new Recording());

        var after = loop.Run(new AppState());

        await Assert.That(after.LastFlightOpened).IsNotNull();
        await Assert.That(after.LastFlightOpened!.ToLowerInvariant()).Contains("nothing")
            .Because("an empty buffer is a person changing their mind, and it has to read as "
                   + "that rather than as silence.");
    }

    [Test]
    public async Task Every_write_says_what_it_did_on_a_line_a_person_sees()
    {
        // THE HALF THAT WAS MISSING EVEN AFTER THE KEYS WORKED. LastTakeover and
        // LastHandBack were written, asserted in tests, and rendered by no view - so
        // a working key produced silence, which a person cannot tell from a key that
        // does nothing. That is the same defect one layer out.
        foreach (var command in (Command[])
            [Command.OpenFlight, Command.AddCredential, Command.Invite])
        {
            var after = new ConsoleLoop(
                new PressesThen(command), new NoEditing(), actions: new Recording())
                .Run(new AppState());

            await Assert.That(PaneText.Activity(after)).IsNotEmpty()
                .Because($"{command} did something and the screen has to say so.");
        }
    }

    [Test]
    public async Task A_key_that_changes_nothing_says_nothing_new()
    {
        // The twin. An activity line that always had something in it would be
        // furniture rather than information, and quitting is not an event.
        var after = new ConsoleLoop(
            new PressesThen(Command.Quit), new NoEditing(), actions: new Recording())
            .Run(new AppState());

        await Assert.That(PaneText.Activity(after)).IsEmpty();
    }

    [Test]
    public async Task An_invitation_does_not_overwrite_a_takeover_seed()
    {
        // FOUND BY RUNNING IT. SeedPlacer hardcoded gg-takeover-seed.txt, which was
        // right while a takeover was its only caller. The console's invitation is
        // the second, and both pass a temp directory - so an invitation issued while
        // somebody had a seed waiting destroyed the document they needed to pick the
        // flight up. Both were "working" and no unit test would have shown it.
        await Assert.That(SeedPlacer.InvitationFile)
            .IsNotEqualTo(SeedPlacer.TakeoverSeedFile);

        var into = Path.Combine(Path.GetTempPath(), $"gg-placer-{Guid.NewGuid():N}");

        try
        {
            var seed = SeedPlacer.Place("the seed", new NoClipboardHere(), into);
            var invite = SeedPlacer.Place(
                "the invitation", new NoClipboardHere(), into, SeedPlacer.InvitationFile);

            var seedPath = ((SeedPlacement.File)seed).Path;

            await Assert.That(((SeedPlacement.File)invite).Path).IsNotEqualTo(seedPath);
            await Assert.That(File.ReadAllText(seedPath)).IsEqualTo("the seed")
                .Because("the seed is what somebody reads to take a flight over, and losing it to "
                       + "an unrelated key press is losing the handoff.");
        }
        finally
        {
            if (Directory.Exists(into))
            {
                Directory.Delete(into, recursive: true);
            }
        }
    }

    private sealed class NoClipboardHere : IClipboard
    {
        public string? Copy(string text) => "no clipboard in a test";
    }

    // ---- doubles ----

    /// <summary>A UI session that exits once with the given command, then quits.</summary>
    private sealed class PressesThen(Command command) : IUiSession
    {
        private bool _pressed;

        public UiOutcome Run(AppState state)
        {
            if (_pressed)
            {
                return new UiOutcome(Command.Quit, state);
            }

            _pressed = true;
            return new UiOutcome(command, state);
        }
    }

    /// <summary>An editor a person closed without typing anything.</summary>
    private sealed class NoEditing : IEditorSession
    {
        public string Edit(string initialText) => "";
    }

    private sealed class Recording : IConsoleActions
    {
        public string Decide(string flight, string obligation, bool approved, string? reason) => "";
        public string Fly(string intent) => $"opened for {intent}";

        /// <summary>Nothing has flown, which is these tests' subject-free case.</summary>
        public string? AlreadyFlown(string provider, string id) => null;

        public string FlyTicket(string provider, string id) =>
            Fly($"{provider}#{id}");
        public string AddCredential() => "registered";

        public string ForgetCredential() => "registered";
        public string Invite() => "placed";
    }

    private sealed class HeldSession : ISessionStore
    {
        public StoredSession? Read() => new()
        {
            SessionToken = "a-session",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            TenantId = "019ff8aa-1111-7000-8000-0000000000ff",
            PrincipalDisplay = "edge",
        };

        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    private sealed class NoStore : ICredentialStore
    {
        public string Root => "(none)";
        public string Protection => "nothing is stored";
        public string PathFor(string locator) => "(none)";
        public void Write(string locator, string secret) { }
        public string? Read(string locator) => null;
        public bool Remove(string locator) => false;
    }

    /// <summary>Answers every prompt, so the failure under test is the network's.</summary>
    private sealed class Answers(string secret) : ISecretPrompt
    {
        public string ReadSecret(string prompt) => secret;
        public string ReadLine(string prompt) => "acme/widgets";
    }
}
