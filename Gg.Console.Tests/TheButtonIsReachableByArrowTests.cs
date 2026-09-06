using System.Data;
using Gg.Console.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// The start button is reached with the arrow keys, and once it is, the key
/// beside it is one way too many.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was drawn and could not be reached.</b> Focus lands on the table when
/// the tab opens, the table keeps it, and tab is taken - this console binds it
/// to moving between tabs - so the only way to press the button was a mouse.
/// A button a keyboard cannot reach is a picture of a button.
/// </para>
/// <para>
/// <b>And then `s' is redundant.</b> Two ways to do one thing is two things to
/// keep agreeing; the button says what it does in words, sits above the table
/// it is about, and is now one arrow away. The key was there because nothing
/// else could be reached.
/// </para>
/// <para>
/// <b>What the view relies on is asserted here, because the view cannot be.</b>
/// Three facts make the step work - a table declines an arrow it cannot act on,
/// a button declines the one that would leave it, and a button takes enter -
/// and all three are Terminal.Gui's rather than ours. A version that changes
/// any of them should fail here rather than in somebody's hands.
/// </para>
/// </remarks>
public class TheButtonIsReachableByArrowTests
{
    private static TableView AFullTable()
    {
        var table = CollectionViews.Table();
        var data = new DataTable();
        data.Columns.Add("what");

        for (var i = 0; i < 4; i++)
        {
            data.Rows.Add($"row {i}");
        }

        CollectionViews.Fill(table, new DataTableSource(data));
        table.SetSelection(0, 0, extendExistingSelection: false, null);

        return table;
    }

    [Test]
    public async Task A_table_lets_an_arrow_out_when_the_cursor_cannot_move()
    {
        var table = AFullTable();

        await Assert.That(table.NewKeyDownEvent(Key.CursorUp)).IsFalse()
            .Because("at the top row there is nowhere to go, and the key has to reach whatever "
                   + "is above the table or nothing above it can be focused.");

        table.SetSelection(0, 1, extendExistingSelection: false, null);

        await Assert.That(table.NewKeyDownEvent(Key.CursorUp)).IsTrue()
            .Because("and anywhere else it is the cursor's, which is the half that must not "
                   + "change.");
    }

    [Test]
    public async Task A_button_lets_the_arrow_that_leaves_it_out_and_takes_enter()
    {
        var button = new Button { Text = "Start a runner here" };

        await Assert.That(button.NewKeyDownEvent(Key.CursorDown)).IsFalse()
            .Because("down off the button is how somebody gets back to the table.");
        await Assert.That(button.NewKeyDownEvent(Key.Enter)).IsTrue()
            .Because("and enter on it is how it is pressed, which is the whole point of "
                   + "reaching it.");
    }

    [Test]
    public async Task The_screen_steps_focus_on_those_arrows()
    {
        // THE WIRING, because the screen cannot be constructed without a
        // terminal. The three facts above are what it stands on; this is that
        // it stands on them.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_runnerStart.SetFocus()")
            .Because("up from the top of the table reaches the button.");
        await Assert.That(screen).Contains("Key.CursorUp")
            .Because("and the key it reads is the one the table just let go of.");
    }

    [Test]
    public async Task Nothing_starts_a_runner_from_the_keymap_any_more()
    {
        // EVERY SHAPE, and there is no longer a flag for this one - the
        // context carried a RunnerStartable that only that binding read, and a
        // flag nothing dispatches on is weight the catalogue and the help page
        // carry for nothing.
        var anywhere = from mode in Enum.GetValues<UiMode>()
                       from showing in Enum.GetValues<TabId>()
                       from frozen in (bool[])[false, true]
                       from takeable in (bool[])[false, true]
                       from handedBack in (bool[])[false, true]
                       select new KeymapContext(mode, showing, frozen, takeable, handedBack);

        var bound = anywhere
            .SelectMany(Keymap.Bindings)
            .Where(binding => binding.Command == Command.StartRunner)
            .Select(binding => binding.Key.Name)
            .Distinct()
            .ToList();

        await Assert.That(bound).IsEmpty()
            .Because("the button is the way, and two ways to do one thing is two things to "
                   + $"keep agreeing. Found: {string.Join(", ", bound)}");
    }

    [Test]
    public async Task And_the_notice_stops_naming_a_key_that_is_gone()
    {
        var notice = PaneText.RunnerNotice(new AppState { ActiveTab = TabId.Runners });

        await Assert.That(notice).IsNotEmpty()
            .Because("there is still nothing running here and that is still worth saying.");
        await Assert.That(notice).DoesNotContain("[ s ")
            .Because("a notice naming a key nothing resolves is the shape this console has a "
                   + $"guard for. Notice: '{notice}'");
    }

    [Test]
    public async Task The_command_is_still_the_shells_because_the_button_raises_it()
    {
        // NOT DELETED, ONLY UNBOUND. The button ends the session with this
        // command and the loop still has the arm.
        await Assert.That(ShellCommands.Handled).Contains(Command.StartRunner);
    }
}
