using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The verification link can be opened, and copied, without being typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The modal shows a URL a person cannot use.</b> gg owns the terminal it
/// is drawn in, so the link cannot be clicked and cannot be selected without
/// fighting the alternate screen - and it is a long one with a code beneath it.
/// Reading it across to a browser by hand is the same dead end the sign-in
/// modal exists to remove, one step further in.
/// </para>
/// <para>
/// <b>Both are the shell's.</b> Opening spawns a browser and copying reaches
/// the OS clipboard, and a UI session may do neither - the session ends, the
/// loop acts with the terminal provably free, and the next session says what
/// happened.
/// </para>
/// <para>
/// <b>And only once there is something to open.</b> The first step of this
/// modal has no URL: keys offered there would be keys that cannot work, which
/// is Article XI - a key that appears to work is worse than one not offered.
/// </para>
/// </remarks>
public class TheSignInUriIsTwoKeysAwayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);

    private static AppState Showing() => new()
    {
        Mode = UiMode.SignIn,
        SignIn = new PendingSignIn
        {
            VerificationUri = "https://example.test/device",
            UserCode = "WDJB-MJHT",
            ExpiresAt = T0.AddMinutes(15),
        },
    };

    private static KeymapContext Started() =>
        new(UiMode.SignIn) { SignInStarted = true };

    [Test]
    public async Task A_code_showing_offers_all_three()
    {
        // THE KEYS FOLLOW THE LABELS ON THE SCREEN. The modal writes two lines,
        // `Open:' and `Code:', so `o' and `c' are the letters a person reading
        // it would reach for - and `l' is the link, which is the one thing on
        // that screen with two useful things to do to it.
        //
        // `c' and `l' are add-credential and toggle-live in Normal mode, and
        // free here: a modal owns the keyboard, which is what lets one letter
        // mean the obvious thing in the place it is obvious.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), Started()))
            .IsEqualTo(Command.OpenSignInUri);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('l'), Started()))
            .IsEqualTo(Command.CopySignInUri);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), Started()))
            .IsEqualTo(Command.CopySignInCode)
            .Because("the code is what a person types once the browser is open, so it is the "
                   + "copy they reach for most.");
    }

    [Test]
    public async Task The_line_says_where_each_one_goes()
    {
        var hints = Keymap.Hints(Started());

        await Assert.That(hints).Contains("o open in browser")
            .Because("`open it' left a person to work out what `it' was, on a screen holding "
                   + $"a link and a code. Line: {hints}");
        await Assert.That(hints).Contains("c copy the code");
        await Assert.That(hints).Contains("l copy the link");
    }

    [Test]
    public async Task And_the_step_before_it_offers_neither()
    {
        // ARTICLE XI. There is no URL yet, so a key that opened one would be a
        // key that cannot work.
        var beforeTheCode = new KeymapContext(UiMode.SignIn);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), beforeTheCode)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), beforeTheCode)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('l'), beforeTheCode)).IsNull();
    }

    [Test]
    public async Task Both_are_the_shells_because_a_session_may_do_neither()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.OpenSignInUri);
        await Assert.That(ShellCommands.Handled).Contains(Command.CopySignInUri);
        await Assert.That(ShellCommands.Handled).Contains(Command.CopySignInCode);

        // AND THE MODAL STAYS OPEN. Opening a browser is a step on the way
        // through this modal, not a way out of it: the code is still what a
        // person needs on the screen while they approve, and the console closes
        // this itself once they have.
        await Assert.That(Reducer.Reduce(Showing(), Command.OpenSignInUri).Mode)
            .IsEqualTo(UiMode.SignIn);
        await Assert.That(Reducer.Reduce(Showing(), Command.CopySignInUri).Mode)
            .IsEqualTo(UiMode.SignIn);
        await Assert.That(Reducer.Reduce(Showing(), Command.CopySignInCode).Mode)
            .IsEqualTo(UiMode.SignIn);
    }

    [Test]
    public async Task The_loop_hands_over_what_is_on_the_screen()
    {
        var opened = new List<string>();
        var copied = new List<string>();

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.OpenSignInUri, state),
            state => new UiOutcome(Command.CopySignInUri, state),
            state => new UiOutcome(Command.CopySignInCode, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(
            ui,
            new NoEditor(),
            openUri: (state, uri) =>
            {
                opened.Add(uri);
                return state with { LastSignIn = $"Opened {uri}." };
            },
            copyUri: (state, uri) =>
            {
                copied.Add(uri);
                return state with { LastSignIn = "The link is on the clipboard." };
            })
            .Run(Showing());

        await Assert.That(opened).IsEquivalentTo(new[] { "https://example.test/device" });

        // THE LINK AND THEN THE CODE, each from the modal rather than composed
        // a second time - and each through the same port, because putting text
        // on a clipboard is one act whatever the text is.
        await Assert.That(copied)
            .IsEquivalentTo(new[] { "https://example.test/device", "WDJB-MJHT" });

        await Assert.That(final.LastSignIn).IsNotNull()
            .Because("and what happened is said, because a browser that opened behind this "
                   + "window and a copy that silently failed look identical from here.");
    }

    [Test]
    public async Task A_console_that_can_do_neither_says_so()
    {
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.OpenSignInUri, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(ui, new NoEditor()).Run(Showing());

        await Assert.That(final.LastSignIn).IsNotNull()
            .Because("the port not being passed is what `y' looked like for two slices.");
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => "";
    }
}
