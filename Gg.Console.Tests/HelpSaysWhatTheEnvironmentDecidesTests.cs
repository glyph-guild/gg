using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The help modal says which environment variables decide what.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS: a key that did nothing, and no way to see why.</b>
/// Pressing <c>n</c> hands the terminal to <c>$EDITOR</c>. Set to a GUI editor
/// that forks and returns, <c>WaitForExit</c> comes back at once, the temp file
/// is still empty, and the console reports "no intent was written" — which
/// reads as a broken key. The console knew the answer and had nowhere to say
/// it.
/// </para>
/// <para>
/// <b>A DECLARED LIST, NEVER A SWEEP.</b> Every variable here is one this
/// program reads, named by the code that reads it. Dumping the process
/// environment would put whatever else a person exports — cloud keys, tokens —
/// on a screen they may be sharing, and into the state dump.
/// </para>
/// <para>
/// <b>Unset is shown, not omitted.</b> The variable you need to look at is
/// usually the one that is not set, and a list of only what is set cannot tell
/// you that.
/// </para>
/// </remarks>
public class HelpSaysWhatTheEnvironmentDecidesTests
{
    private static AppState Helping() => new()
    {
        Mode = UiMode.Help,
        Settings =
        [
            new EnvironmentSetting
            {
                Name = "EDITOR",
                Value = "code",
                Why = "the editor `n` hands the terminal to",
            },
            new EnvironmentSetting
            {
                Name = IntentConfiguration.ServedVariable,
                Value = null,
                Why = "which trackers this machine reads",
            },
        ],
    };

    [Test]
    public async Task Help_opens_on_the_keys_because_that_is_what_it_was_always_for()
    {
        var text = PaneText.Modal(Helping());

        await Assert.That(text).Contains("quit from anywhere");
        await Assert.That(text).DoesNotContain("EDITOR")
            .Because("the keys are the answer to 'what can I press', which is why help exists.");
    }

    [Test]
    public async Task Tab_turns_the_page_and_turns_it_back()
    {
        var environment = Reducer.Reduce(Helping(), Command.FocusNextPane);

        await Assert.That(PaneText.Modal(environment)).Contains("EDITOR");
        await Assert.That(PaneText.Modal(environment)).Contains("code");

        var keys = Reducer.Reduce(environment, Command.FocusNextPane);

        await Assert.That(PaneText.Modal(keys)).Contains("quit from anywhere");
    }

    [Test]
    public async Task The_page_says_which_page_it_is_and_how_to_turn_it()
    {
        // A modal with a hidden second page is a second page nobody finds.
        await Assert.That(PaneText.Modal(Helping())).Contains("tab");
        await Assert.That(PaneText.Modal(Reducer.Reduce(Helping(), Command.FocusNextPane)))
            .Contains("tab");
    }

    [Test]
    public async Task Every_variable_says_what_it_decides()
    {
        // A name and a value with no consequence attached is a line a person
        // has to go and look up, which is what they were already doing.
        var text = PaneText.Modal(Reducer.Reduce(Helping(), Command.FocusNextPane));

        await Assert.That(text).Contains("the editor `n` hands the terminal to");
        await Assert.That(text).Contains("which trackers this machine reads");
    }

    [Test]
    public async Task An_unset_variable_is_shown_as_unset_rather_than_left_out()
    {
        var text = PaneText.Modal(Reducer.Reduce(Helping(), Command.FocusNextPane));

        await Assert.That(text).Contains(IntentConfiguration.ServedVariable);
        await Assert.That(text).Contains("not set")
            .Because("the variable worth looking at is usually the one that is not set.");
    }

    [Test]
    public async Task Closing_help_and_reopening_it_starts_on_the_keys()
    {
        // A modal that remembers a page from ten minutes ago answers a question
        // nobody just asked.
        var environment = Reducer.Reduce(Helping(), Command.FocusNextPane);
        var closed = Reducer.Reduce(environment, Command.CloseModal);
        var again = Reducer.Reduce(closed, Command.ToggleHelp);

        await Assert.That(PaneText.Modal(again)).Contains("quit from anywhere");
    }

    [Test]
    public async Task Tab_outside_help_still_moves_between_tabs()
    {
        // THE ANCHOR. Tab is FocusNextPane everywhere else, and a help page
        // that stole it would break the key it borrowed.
        //
        // AMENDED WHEN A VIEW TOOK THE WHOLE SCREEN. It asserted that tab moves
        // off the queue from a bare state, which was true when the flight pane
        // counted as somewhere to move to and every pane was 'visible'. Tab
        // walks the OPEN TABS now and there is nowhere to go from a console
        // with nothing open - Tabs_never_lands_on_a_view_nobody_opened says so
        // deliberately - so the anchor opens one and asserts the move.
        var opened = Reducer.Reduce(new AppState(), Command.ToggleEvidence);

        var moved = Reducer.Reduce(opened, Command.FocusNextPane);

        await Assert.That(moved.ActiveTab).IsNotEqualTo(opened.ActiveTab)
            .Because("tab outside help moves between the open tabs.");
    }

    [Test]
    public async Task Esc_still_closes_help_from_either_page()
    {
        foreach (var state in (AppState[])[Helping(), Reducer.Reduce(Helping(), Command.FocusNextPane)])
        {
            await Assert.That(Reducer.Reduce(state, Command.CloseModal).Mode)
                .IsEqualTo(UiMode.Normal);
        }
    }

    [Test]
    public async Task A_console_told_nothing_about_its_environment_says_that()
    {
        // The composition root builds this list; a test host does not. An empty
        // page must read as "nobody told me" rather than as "nothing is set".
        var text = PaneText.Modal(new AppState { Mode = UiMode.Help });
        var page = PaneText.Modal(
            Reducer.Reduce(new AppState { Mode = UiMode.Help }, Command.FocusNextPane));

        await Assert.That(text).Contains("quit from anywhere");
        await Assert.That(page).Contains("was not told");
    }
}

/// <summary>
/// The key that turns the help page is bound to something.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS FILE SHIPPED WITH.</b> Every test in
/// <see cref="HelpSaysWhatTheEnvironmentDecidesTests"/> calls
/// <c>Reducer.Reduce(state, Command.FocusNextPane)</c> directly. The reducer arm
/// worked, the pane rendered, and the keys page advertised <i>"tab: what this
/// machine's environment decides"</i> — while <c>UiMode.Help</c> bound only
/// <c>Esc</c>, so <c>tab</c> resolved to null and the arm was unreachable from a
/// keyboard.
/// </para>
/// <para>
/// <b>Nine passing tests, one layer below the break.</b> That is the shape worth
/// remembering: a feature can be correct at the level you tested and absent at
/// the level a person uses. The fix is one binding; the lesson is that a
/// reducer test is not a key test.
/// </para>
/// <para>
/// <b>And it is the dead-key shape by name</b> — a key advertised in a hint line
/// that does nothing, which <c>ShellHandledTests</c> exists for one layer up.
/// This one escaped because the advertisement was a literal in the pane text
/// rather than a <c>KeyBinding</c>, so nothing derived it from the bindings.
/// </para>
/// </remarks>
public class TheHelpTabIsActuallyBoundTests
{
    [Test]
    public async Task Tab_resolves_to_something_while_help_is_open()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.TabKey, new KeymapContext(UiMode.Help)))
            .IsEqualTo(Command.FocusNextPane)
            .Because("the keys page tells a person to press it.");
    }

    [Test]
    public async Task Pressing_it_turns_the_page_through_the_whole_path()
    {
        // KEY TO TEXT, not reducer to text. The path the earlier tests skipped.
        var help = Reducer.Reduce(new AppState(), Command.ToggleHelp);
        var command = Keymap.Resolve(KeyStroke.TabKey, new KeymapContext(help.Mode));

        await Assert.That(command).IsNotNull();

        var turned = Reducer.Reduce(help, command!.Value);

        await Assert.That(turned.HelpPage).IsEqualTo(HelpPage.Environment);
    }

    [Test]
    public async Task Every_key_the_help_page_names_in_prose_actually_resolves()
    {
        // THE RATCHET. The advertisement was a literal in the pane text, so no
        // binding derived it and nothing noticed it pointed at nothing. Any
        // future "press x to ..." in this pane has to be a key that works.
        var keys = PaneText.Modal(Reducer.Reduce(new AppState(), Command.ToggleHelp));

        foreach (var named in System.Text.RegularExpressions.Regex
            .Matches(keys, @"^\s{2}(tab|esc|[a-z]):", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value))
        {
            var stroke = named switch
            {
                "tab" => KeyStroke.TabKey,
                "esc" => KeyStroke.Esc,
                _ => KeyStroke.Char(named[0]),
            };

            await Assert.That(Keymap.Resolve(stroke, new KeymapContext(UiMode.Help)))
                .IsNotNull()
                .Because($"the help page tells a person to press '{named}' and it must do "
                       + "something. A key advertised and bound to nothing is the shape "
                       + "ShellHandledTests exists for.");
        }
    }

    [Test]
    public async Task Esc_still_closes_help_now_that_tab_is_bound()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, new KeymapContext(UiMode.Help)))
            .IsEqualTo(Command.CloseModal);
    }
}

/// <summary>
/// The help modal shows its tabs.
/// </summary>
/// <remarks>
/// <para>
/// <b>A SECOND PAGE NOBODY CAN SEE IS A SECOND PAGE NOBODY OPENS.</b> The first
/// version of this feature had a keystroke and a line of prose telling a person
/// to press it, which is a worse thing than a tab: it asks them to read an
/// instruction and remember it, where a tab bar shows both pages at once and
/// says which one they are on.
/// </para>
/// <para>
/// <b>Rendered as text, like every other pane here.</b> The modal body is a
/// Label fed by <see cref="PaneText"/>; a Terminal.Gui TabView would put the
/// state of which page is showing in a widget, where nothing can assert it and
/// the state dump cannot reproduce it.
/// </para>
/// </remarks>
public class TheHelpModalShowsItsTabsTests
{
    private static AppState Helping() => Reducer.Reduce(new AppState(), Command.ToggleHelp);

    [Test]
    public async Task Both_tabs_are_visible_from_either_page()
    {
        foreach (var state in (AppState[])[Helping(), Reducer.Reduce(Helping(), Command.FocusNextPane)])
        {
            var text = PaneText.Modal(state);

            await Assert.That(text).Contains("Keys");
            await Assert.That(text).Contains("Environment")
                .Because("a person has to see that the other page exists without pressing anything.");
        }
    }

    [Test]
    public async Task The_tab_you_are_on_is_marked_and_the_other_is_not()
    {
        var keys = PaneText.Modal(Helping());
        var environment = PaneText.Modal(Reducer.Reduce(Helping(), Command.FocusNextPane));

        await Assert.That(keys).Contains("[ Keys ]");
        await Assert.That(keys).DoesNotContain("[ Environment ]");

        await Assert.That(environment).Contains("[ Environment ]");
        await Assert.That(environment).DoesNotContain("[ Keys ]")
            .Because("two tabs marked as current is a tab bar that says nothing.");
    }

    [Test]
    public async Task The_tab_bar_is_the_first_thing_in_the_modal()
    {
        // Below the content it is a footer, and a person reads the page before
        // learning there was another one.
        var first = PaneText.Modal(Helping()).Split('\n')[0];

        await Assert.That(first).Contains("Keys");
        await Assert.That(first).Contains("Environment");
    }

    [Test]
    public async Task The_bar_says_how_to_move_between_them()
    {
        // Tabs a person cannot work out how to change are decoration.
        await Assert.That(PaneText.Modal(Helping())).Contains("tab");
    }

    [Test]
    public async Task The_keys_page_no_longer_explains_the_other_page_in_prose()
    {
        // THE THING THE TAB BAR REPLACES. A sentence saying "tab: what this
        // machine's environment decides" is an instruction to remember; the bar
        // is the same fact, visible, and it cannot point at a page that is not
        // there.
        await Assert.That(PaneText.Modal(Helping()))
            .DoesNotContain("what this machine's environment decides");
    }
}
