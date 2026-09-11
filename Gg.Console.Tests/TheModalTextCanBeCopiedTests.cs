using System.Diagnostics;
using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What a reading modal shows can be put on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// <b>BECAUSE THE THING IN IT IS USUALLY A REFUSAL SOMEBODY HAS TO TAKE
/// SOMEWHERE.</b> The outcome modal exists to make a control plane's own
/// sentences readable — and the next thing a person does with one is paste it
/// into a chat, an issue, or a message to whoever owns the gate. Retyping a
/// wrapped RFC-9110 problem document out of a terminal is how a report stops
/// being useful.
/// </para>
/// <para>
/// <b>IT IS THE SHELL'S, AND THAT IS ALREADY DECIDED.</b>
/// <c>LiveStreamingTests</c> grants this console exactly one clipboard
/// exception and closes the door behind it: <i>"It is scoped to a clipboard
/// READ, in one field, by one key… and no second spawn: the console's other
/// clipboard use — copying a sign-in link — stays the shell's, and that is
/// deliberate."</i> A copy is a second spawn, so it ends the session and the
/// loop does it with the terminal free.
/// </para>
/// <para>
/// <b>And the modal comes back by itself.</b> The screen is rebuilt from
/// <c>AppState</c> and <c>Mode</c> is part of it, so a copy costs a redraw and
/// not a person's place — which is what makes paying the terminal-release
/// round trip acceptable here when it was not for a half-typed field.
/// </para>
/// <para>
/// <b>One text, from the one producer.</b> <c>PaneText.Modal</c> already
/// answers the body of whatever modal is open, unwrapped — that is why every
/// drawn mode has an arm in it — so copying reaches for the same words the
/// screen drew rather than a second rendering that could differ from them.
/// </para>
/// </remarks>
public class TheModalTextCanBeCopiedTests
{
    private static AppState Reading(UiMode mode) => new()
    {
        Mode = mode,
        ActiveTab = TabId.Envelope,
        ApplyOutcome =
        [
            "Nothing was applied.",
            "",
            "An apply carries a document, and this one carries neither an envelope nor a "
          + "narrowing.",
        ],
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Working = new EstateDiff { Changes = [], Retiring = [], Unreadable = [] },
            Tree = new WorkingCopy { Present = true, Documents = [], Unreadable = [] },
        },
    };

    [Test]
    public async Task Every_reading_view_offers_it_and_says_so_on_the_line()
    {
        foreach (var mode in (UiMode[])
                 [UiMode.ReadingOutcome, UiMode.ReadingChangeset, UiMode.ReadingEnvelope])
        {
            var state = Reading(mode);
            var context = KeymapContext.For(state);

            await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), context))
                .IsEqualTo(Command.CopyModal)
                .Because($"{mode} is read and then taken somewhere. Inside a modal the "
                       + "letters are free, so `c' costs nothing out in Normal mode where "
                       + "it is add-credential.");

            await Assert.That(Keymap.Hints(context)).Contains("c ", StringComparison.Ordinal)
                .Because("a key nobody is shown is a key nobody presses, and this one has "
                       + $"no other advertisement. Line: {Keymap.Hints(context)}");
        }
    }

    [Test]
    public async Task It_does_not_take_the_credential_key_away_from_normal_mode()
    {
        // THE TRAP THE RETIRE KEY AVOIDED, CHECKED AGAIN. `c` is AddCredential
        // in Normal mode; a tab-scoped one would have shadowed it.
        var normal = new AppState { ActiveTab = TabId.Envelope };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), KeymapContext.For(normal)))
            .IsEqualTo(Command.AddCredential);
    }

    [Test]
    public async Task It_is_the_shells_because_a_clipboard_is_a_child_process()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.CopyModal)
            .Because("LiveStreamingTests grants one clipboard exception, scoped to a READ "
                   + "in one field by one key, and says in as many words that a copy stays "
                   + "the shell's. A session may not spawn.");
    }

    [Test]
    public async Task It_copies_what_the_modal_is_showing_and_says_it_did()
    {
        var state = Reading(UiMode.ReadingOutcome);

        string? sent = null;

        var after = ConsoleClipboard.Copied(
            state,
            PaneText.Modal(state),
            (_, input) =>
            {
                sent = input;
                return 0;
            });

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!).Contains("neither an envelope nor a narrowing",
                StringComparison.Ordinal)
            .Because("the words the screen drew, from PaneText.Modal - the one producer - "
                   + "rather than a second rendering that could differ from them.");

        await Assert.That(after.LastEstate).IsNotNull();
        await Assert.That(after.LastEstate!).Contains("clipboard", StringComparison.OrdinalIgnoreCase)
            .Because("a copy that says nothing is indistinguishable from a key that does "
                   + $"nothing. Said: {after.LastEstate}");
    }

    [Test]
    public async Task A_clipboard_that_refuses_says_so_rather_than_going_quiet()
    {
        var state = Reading(UiMode.ReadingOutcome);

        var after = ConsoleClipboard.Copied(
            state, PaneText.Modal(state), (_, _) => 1);

        await Assert.That(after.LastEstate!).Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("silence is the one answer that reads as the feature being broken, "
                   + $"which is what ConsoleLink already says about its own copy. Said: "
                   + $"{after.LastEstate}");
    }

    [Test]
    public async Task Nothing_to_copy_is_not_reported_as_a_copy()
    {
        // A MODAL WITH AN EMPTY BODY. Reachable from a hand-built model, and
        // "copied nothing to the clipboard" is a sentence about an act that
        // did not happen.
        var after = ConsoleClipboard.Copied(
            new AppState { Mode = UiMode.ReadingOutcome }, "", (_, _) => 0);

        await Assert.That(after.LastEstate!).Contains("nothing", StringComparison.OrdinalIgnoreCase)
            .Because("there was nothing on screen to take.");
    }
}
