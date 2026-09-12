using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Turning the help pages and opening a fold, through the keymap.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reducer cycled two pages by naming them.</b> A third page would have
/// been added to the enum, drawn on the bar, and never reached by the key —
/// the shape <c>Tabs.Offered</c> exists to prevent one layer up. It walks
/// <c>HelpPages.All</c> now, which is the same list the bar is built from.
/// </para>
/// <para>
/// <b>A fold is a person's choice, so it is a command.</b> Opening one by
/// letting a widget handle the key would put the answer where the model cannot
/// see it, and the console rebuilds its views from the model every time it
/// hands the terminal to an editor.
/// </para>
/// </remarks>
public class AFoldIsOpenedByAKeyTests
{
    private static AppState Helping() => new() { Mode = UiMode.Help };

    [Test]
    public async Task Tab_walks_every_page_the_bar_offers()
    {
        var state = Helping();

        foreach (var expected in HelpPages.All.Skip(1).Concat([HelpPages.All[0]]))
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);

            await Assert.That(state.HelpPage).IsEqualTo(expected)
                .Because("the key walks the same list the bar is drawn from, so a page "
                       + "added to one is reachable by the other.");
        }
    }

    [Test]
    public async Task A_fold_opens_and_closes_on_the_key()
    {
        var state = Helping() with { HelpFold = UiMode.Help };

        await Assert.That(HelpTree.IsOpen(state, UiMode.Help)).IsFalse();

        var opened = Reducer.Reduce(state, Command.ToggleFold);

        await Assert.That(HelpTree.IsOpen(opened, UiMode.Help)).IsTrue()
            .Because("a fold a widget opened would be invisible to the model, and the "
                   + "console rebuilds its views from the model.");

        await Assert.That(HelpTree.IsOpen(Reducer.Reduce(opened, Command.ToggleFold), UiMode.Help))
            .IsFalse();
    }

    [Test]
    public async Task The_key_does_nothing_where_there_is_no_fold_under_the_cursor()
    {
        var state = Helping();

        await Assert.That(Reducer.Reduce(state, Command.ToggleFold)).IsEqualTo(state)
            .Because("a key that silently changed something else would be worse than one "
                   + "that does nothing, and the cursor is on a KEY rather than a group "
                   + "most of the time.");
    }

    [Test]
    public async Task Folding_is_only_offered_where_there_is_something_to_fold()
    {
        var keys = Keymap.Hints(new KeymapContext(UiMode.Help, OverAFold: true));

        await Assert.That(keys).Contains("fold", StringComparison.OrdinalIgnoreCase)
            .Because("a key nobody can see is a key nobody presses - the argument the "
                   + "hint line exists for, and the reason the page bar names `tab'.");
    }
}
