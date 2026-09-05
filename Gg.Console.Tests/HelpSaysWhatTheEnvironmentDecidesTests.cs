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
    public async Task Tab_outside_help_still_changes_pane()
    {
        // THE ANCHOR. Tab is FocusNextPane everywhere else, and a help page
        // that stole it would break the key it borrowed.
        var moved = Reducer.Reduce(new AppState(), Command.FocusNextPane);

        await Assert.That(moved.FocusedPane).IsNotEqualTo(PaneId.Queue);
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
