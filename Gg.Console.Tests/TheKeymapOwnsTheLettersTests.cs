using System.Collections.ObjectModel;
using System.Data;
using Gg.Console.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A letter the keymap binds means what the keymap says, whichever list has
/// the cursor.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>q</c> quit sometimes.</b> <c>ListView</c> and <c>TableView</c> search
/// their own rows as a person types - <c>KeystrokeNavigator</c> and
/// <c>CollectionNavigator</c> - and a keystroke that matches a row is consumed
/// there and never reaches the screen. So whether quit worked depended on
/// whether anything on screen began with a q, which is why it looked
/// intermittent rather than broken.
/// </para>
/// <para>
/// <b>Nothing in this console asked for type-to-search.</b> Both widgets have
/// it on by default and both document the way off; the keys a person is told
/// about come from <c>Keymap</c> and are meant to be the only ones that do
/// anything. A widget minting bindings from the data on screen is the same
/// defect as a hint line that advertises a key nothing resolves, arriving from
/// the other direction.
/// </para>
/// <para>
/// <b>The anchors are the half that makes this a measurement.</b> Each pair
/// seeds the collection with a row that starts with the letter under test, so
/// the unsilenced view demonstrably eats it. Without them a green could mean
/// the widget never wanted the key.
/// </para>
/// </remarks>
public class TheKeymapOwnsTheLettersTests
{
    /// <summary>Every plain letter or digit the keymap binds in any context.</summary>
    private static IReadOnlyList<char> Letters() =>
        [.. Keymap.Catalogue()
            .Select(entry => entry.Binding.Key)
            .Where(stroke => stroke is { Input: not null, Ctrl: false })
            .Select(stroke => stroke.Input!.Value)
            .Distinct()
            .Order()];

    private static ListView AList(char matching)
    {
        var list = new ListView();
        list.SetSource(new ObservableCollection<string> { $"{matching}uite something" });
        return list;
    }

    private static DataTable ATable(char matching)
    {
        var data = new DataTable();
        data.Columns.Add("what");
        data.Rows.Add($"{matching}uite something");
        return data;
    }

    [Test]
    public async Task A_list_left_to_itself_eats_the_letters()
    {
        // THE ANCHOR. If this ever goes green the widget stopped searching on
        // its own and the silencing below is dead code rather than a fix.
        var eaten = Letters()
            .Where(c => AList(c).NewKeyDownEvent(new Key(c)))
            .ToList();

        await Assert.That(eaten).IsNotEmpty()
            .Because("ListView searches its rows as a person types, and that is what took "
                   + "the quit key.");
    }

    [Test]
    public async Task A_table_left_to_itself_eats_the_letters()
    {
        var eaten = Letters()
            .Where(c => new TableView { Table = new DataTableSource(ATable(c)) }
                .NewKeyDownEvent(new Key(c)))
            .ToList();

        await Assert.That(eaten).IsNotEmpty()
            .Because("TableView does the same thing, and three tabs are tables.");
    }

    [Test]
    public async Task The_queues_list_leaves_every_one_of_them_alone()
    {
        var eaten = Letters()
            .Where(c =>
            {
                var list = CollectionViews.List();
                list.SetSource(new ObservableCollection<string> { $"{c}uite something" });
                return list.NewKeyDownEvent(new Key(c));
            })
            .ToList();

        await Assert.That(eaten).IsEmpty()
            .Because("a letter the keymap binds may not be answered by whatever happens to be "
                   + $"in the list. Eaten: {string.Join(", ", eaten)}.");
    }

    [Test]
    public async Task Every_table_leaves_every_one_of_them_alone()
    {
        var eaten = Letters()
            .Where(c =>
            {
                var table = CollectionViews.Table();
                CollectionViews.Fill(table, new DataTableSource(ATable(c)));
                return table.NewKeyDownEvent(new Key(c));
            })
            .ToList();

        await Assert.That(eaten).IsEmpty()
            .Because($"the same, for the three tabs that are tables. Eaten: "
                   + $"{string.Join(", ", eaten)}.");
    }

    [Test]
    public async Task The_screen_builds_no_collection_view_any_other_way()
    {
        // THE RATCHET, because silencing a factory only helps while the factory
        // is the only door. The next list somebody adds to the screen inherits
        // the property instead of needing to be remembered.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("new ListView")
            .Because("CollectionViews.List() is the door; a bare one is a list that searches "
                   + "its rows and takes the keymap's letters with it.");
        await Assert.That(screen).DoesNotContain("new TableView")
            .Because("CollectionViews.Table() likewise.");
        await Assert.That(screen).DoesNotContain(".Table = ")
            .Because("CollectionViews.Fill is the door for the rows too - assigning the "
                   + "source is what brings the widget's own search back.");
    }
}
