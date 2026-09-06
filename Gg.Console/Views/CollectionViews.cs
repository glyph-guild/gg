using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Views;

/// <summary>
/// The lists this console draws, and the one thing they must not do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both widgets search their own rows as a person types, and that took the
/// keymap's letters.</b> <c>ListView.KeystrokeNavigator</c> and
/// <c>TableView.CollectionNavigator</c> are on by default; a keystroke that
/// matches a row is consumed by the widget and never reaches the screen. So
/// <c>q</c> quit or did not depending on whether anything on screen began with
/// a q - twenty-one of the keys the keymap binds are reachable this way, which
/// is nearly all of them.
/// </para>
/// <para>
/// <b>A factory rather than a line in the screen, so a test can ask.</b>
/// <see cref="ConsoleScreen"/> cannot be constructed without a terminal, so a
/// property of the views it builds is a property nothing can check - the same
/// reason <see cref="ConsoleTheme"/> exists. What is decided here is one
/// question per widget; where they sit on the screen stays in the screen.
/// </para>
/// <para>
/// Nothing in this console asked for type-to-search and nothing advertises it.
/// The keys a person is told about come from <c>Keymap</c>, and it is meant to
/// be the only place a printable key means anything.
/// </para>
/// </remarks>
public static class CollectionViews
{
    /// <summary>The queue's list.</summary>
    public static ListView List()
    {
        var list = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };

        // OFF, AND null IS THE DOCUMENTED WAY OFF. Unbound printable keys then
        // bubble through normal key handling instead of being eaten here.
        list.KeystrokeNavigator = null;

        return list;
    }

    /// <summary>
    /// One of the three tabs that are tables.
    /// </summary>
    /// <remarks>
    /// Whole rows select, because every one of these lists is read a row at a
    /// time and a cell cursor would say otherwise.
    /// </remarks>
    /// <summary>
    /// A table that does not answer a printable key.
    /// </summary>
    /// <remarks>
    /// <b>Nulling <c>CollectionNavigator</c> is documented as the way off and it
    /// does not work in Terminal.Gui 2.4.17.</b> Measured: the property reads
    /// back null, before and after the rows are given, and
    /// <c>NewKeyDownEvent('q')</c> still returns handled. Whatever consumes it
    /// does so in <c>OnKeyDownNotHandled</c>, which is the last thing a view is
    /// asked before a key bubbles - so declining there is the only place left to
    /// decline.
    /// <para>
    /// <c>ListView</c> honours its own switch, which is why only the tables need
    /// this. Revisit on the next Terminal.Gui bump: if the switch starts working
    /// the anchor tests beside this will say so by going green on their own.
    /// </para>
    /// </remarks>
    private sealed class QuietTable : TableView
    {
        protected override bool OnKeyDownNotHandled(Key key) => false;
    }

    public static TableView Table()
    {
        var table = new QuietTable
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            FullRowSelect = true,
            MultiSelect = false,
            Style =
            {
                ShowHeaders = true,
                ShowHorizontalHeaderUnderline = true,
                ShowHorizontalHeaderOverline = false,
                ShowVerticalCellLines = false,
                ExpandLastColumn = true,
            },
        };

        table.CollectionNavigator = null;

        return table;
    }

    /// <summary>
    /// Give a table its rows, and take the search back off it.
    /// </summary>
    /// <remarks>
    /// <b>Assigning the source brings the navigator back.</b> <c>TableView</c>
    /// builds a <c>TableCollectionNavigator</c> for whatever table it is handed,
    /// so silencing it once at construction lasts exactly until the first
    /// refresh - and the console refills these on every render. Measured: with
    /// the factory silencing it and nothing else, all twenty-one keys were eaten
    /// again the moment the rows arrived.
    /// </remarks>
    public static void Fill(TableView table, ITableSource? source)
    {
        ArgumentNullException.ThrowIfNull(table);

        table.Table = source;
        table.CollectionNavigator = null;
    }
}
