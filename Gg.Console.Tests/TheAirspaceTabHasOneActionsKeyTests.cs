using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace tab's four actions live behind one key, and that key is the
/// one this console already uses for actions.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: too many keys on one tab.</b> The line carried ten,
/// six of them this tab's own - <c>p s m v w o</c> - and at 170 columns it was
/// already truncating mid-sentence. A line a person cannot read to the end is
/// a line that has stopped advertising anything.
/// </para>
/// <para>
/// <b>THE ACTIONS COLLAPSE; THE TWO THAT MOVE A CURSOR DO NOT.</b> Pull,
/// apply, draft and the outcome are things a person does to the airspace as a
/// whole, occasionally. <c>v</c> and <c>w</c> move around what is on screen,
/// constantly. Putting the second pair behind a modal would charge two
/// keystrokes for every glance.
/// </para>
/// <para>
/// <b><c>a</c>, because it is already the actions key and already answers on
/// this tab.</b> It is bound in Normal mode on EVERY tab and merely hidden
/// from the line when no flight is under a cursor - which on the airspace tab
/// is always, so <c>a</c> there opened a flight modal about no flight. One key
/// with two meanings and the tab deciding which is the shape this keymap
/// already argues for over <c>f</c>, and it turns a dead key into the door.
/// </para>
/// <para>
/// <b>It cannot be a tab-scoped binding.</b> <c>Resolve</c> answers the FIRST
/// match and the Normal-mode <c>a</c> is declared above the tab spread, so an
/// <c>a</c> added there would never fire - the trap a red test already caught
/// for <c>enter</c> and the reason a tab-scoped <c>d</c> was never written.
/// The existing arm has to do the deciding.
/// </para>
/// </remarks>
public class TheAirspaceTabHasOneActionsKeyTests
{
    private static KeymapContext OnTheAirspaceTab() => new()
    {
        Showing = TabId.Envelope,
        OverADocument = true,
    };

    [Test]
    public async Task The_four_actions_leave_the_tab()
    {
        // WHAT THIS ASSERTS CHANGED, AND IT DID NOT LOOSEN. It used to say the
        // four letters resolve to NOTHING from the tab, which was the strongest
        // true statement while all four were free. `s' is the Environments
        // tab's key now, so it resolves - and a test demanding silence would be
        // asking for a dead letter rather than for the property that matters.
        //
        // THE PROPERTY IS THAT NONE OF THEM STILL ACTS ON THE AIRSPACE. Two of
        // the four rewrite a working copy, and what made moving them worth a
        // second keystroke is that no bare-tab letter does that any more. A key
        // that switched TABS was never the hazard; a key that pulled was.
        var tab = OnTheAirspaceTab();

        var acts = (Command[])
        [
            Command.PullEstate,
            Command.AskToApplyEstate,
            Command.DraftEstate,
            Command.ReadOutcome,
        ];

        foreach (var key in "psmo")
        {
            await Assert.That(acts).DoesNotContain(
                    Keymap.Resolve(KeyStroke.Char(key), tab) ?? Command.Quit)
                .Because($"`{key}' acts on the airspace only from inside the actions modal, "
                       + "and a letter that still did it from out here would be a second way "
                       + "to do one thing - with two of the four rewriting a tree.");
        }

        // AND THREE OF THEM ARE STILL SILENT, which is worth keeping separate:
        // `s' was taken deliberately and by argument, and a future letter taken
        // by accident must not pass this by inheriting that.
        foreach (var key in "pmo")
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char(key), tab)).IsNull()
                .Because($"nothing has claimed `{key}' since the four moved, and a letter "
                       + "that quietly acquires a meaning here is how the line grew to ten.");
        }

        await Assert.That(Keymap.Resolve(KeyStroke.Char('s'), tab))
            .IsEqualTo(Command.ToggleEnvironments)
            .Because("`s' went to the Environments tab once applying stopped needing it - "
                   + "named here so the exception is a decision somebody reads rather than "
                   + "a hole in the loop above.");
    }

    [Test]
    public async Task The_two_that_move_the_cursor_stay()
    {
        var tab = OnTheAirspaceTab();

        await Assert.That(Keymap.Resolve(KeyStroke.Char('v'), tab))
            .IsEqualTo(Command.NextAirspaceView);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('w'), tab))
            .IsEqualTo(Command.NextAirspacePane);
    }

    [Test]
    public async Task The_actions_key_is_the_actions_key()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('a'), OnTheAirspaceTab()))
            .IsEqualTo(Command.ToggleAirspaceActions)
            .Because("`a' already answers on this tab and already says `actions'; it just "
                   + "opened a flight modal about no flight.");

        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('a'), new KeymapContext { Showing = TabId.Queue }))
            .IsEqualTo(Command.ToggleFlightActions)
            .Because("and everywhere else it means exactly what it meant.");
    }

    [Test]
    public async Task The_modal_resolves_the_four_and_a_way_out()
    {
        var inside = new KeymapContext
        {
            Mode = UiMode.AirspaceActions,
            Showing = TabId.Envelope,
        };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('p'), inside))
            .IsEqualTo(Command.PullEstate);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('s'), inside))
            .IsEqualTo(Command.AskToApplyEstate);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('m'), inside))
            .IsEqualTo(Command.DraftEstate);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), inside))
            .IsEqualTo(Command.ReadOutcome);

        // THE ONE ESCAPE HATCH, which every modal in this console has.
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, inside))
            .IsEqualTo(Command.CloseModal);
    }

    [Test]
    public async Task The_line_is_short_enough_to_read()
    {
        var said = Keymap.Hints(OnTheAirspaceTab());

        await Assert.That(said).Contains("a actions", StringComparison.Ordinal);

        await Assert.That(said)
            .DoesNotContain("pull the airspace", StringComparison.Ordinal)
            .Because("what the four do is said inside the modal, where there is room for "
                   + "a sentence each.");

        // THE NUMBER IS THE COMPLAINT, so the number is what is held. Six
        // fits a narrow terminal; ten did not fit a wide one.
        var keys = said.Split(" · ", StringSplitOptions.RemoveEmptyEntries).Length;

        await Assert.That(keys).IsLessThanOrEqualTo(7)
            .Because($"ten of them truncated mid-sentence at 170 columns. Said:\n{said}");
    }

    [Test]
    public async Task A_shell_command_from_inside_the_modal_closes_it()
    {
        // PULL AND DRAFT END THE SESSION. They were reached from Normal mode
        // before, where there was no modal to close; reached from one, the
        // surviving state would carry the open modal into the session built
        // over the result - a dialog over an answer nobody asked it about.
        await Assert.That(ShellCommands.Handled).Contains(Command.PullEstate);
        await Assert.That(ShellCommands.Handled).Contains(Command.DraftEstate);

        var loop = Sources.Read("Gg.Console", "ConsoleLoop.cs");

        var pull = loop[loop.IndexOf("case Command.PullEstate:", StringComparison.Ordinal)..];
        pull = pull[..pull.IndexOf("case Command.", 4, StringComparison.Ordinal)];

        await Assert.That(pull).Contains("Closed(", StringComparison.Ordinal)
            .Because("the question closed, however it was answered - which is what that "
                   + "helper is called and what it already does for the compose choice.");
    }
}
