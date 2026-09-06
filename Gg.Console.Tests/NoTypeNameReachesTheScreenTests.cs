using System.Text.RegularExpressions;
using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// What a person reads is written for them. No enum member, no column a
/// library named because we gave it nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two leaks, one shape.</b> The modal's title was
/// <c>State.Mode.ToString()</c>, so a person opening a refusal read
/// <c>HandFlight</c> across the top of it, and opening a flight read
/// <c>FlightDetail</c>. The tables' first column is deliberately nameless - it
/// holds a mark, and a heading over a column of marks is a word explaining a
/// symbol that already explains itself - and <c>DataTable</c> answers an empty
/// name by inventing <c>Column1</c>, which is then drawn as the heading.
/// </para>
/// <para>
/// <b>Both are the same defect.</b> A name that exists for the compiler reached
/// the screen because nothing was asked to write one for a person, and in both
/// cases the thing that filled the gap looked enough like a word to survive
/// review.
/// </para>
/// </remarks>
public class NoTypeNameReachesTheScreenTests
{
    /// <summary>Two words run together, which is how a type name reads.</summary>
    private static bool LooksLikeCode(string text) =>
        Regex.IsMatch(text, "[a-z][A-Z]") || text.StartsWith("Column", StringComparison.Ordinal);

    [Test]
    public async Task Every_modal_is_titled_in_words()
    {
        var coded = Enum.GetValues<UiMode>()
            .Where(mode => mode != UiMode.Normal)
            .Select(mode => (Mode: mode, Title: PaneText.ModalTitle(mode)))
            .Where(t => t.Title.Length == 0 || LooksLikeCode(t.Title))
            .ToList();

        await Assert.That(coded).IsEmpty()
            .Because("a person reading a refusal should not be told its enum member. Found: "
                   + string.Join(", ", coded.Select(t => $"{t.Mode}='{t.Title}'")));
    }

    [Test]
    public async Task And_the_titles_are_not_just_the_member_with_a_space_in_it()
    {
        // THE ANCHOR, because splitting the camel case would pass the row above
        // and still be a type name. `FlightDetail' is not what a person calls
        // the thing they just opened.
        await Assert.That(PaneText.ModalTitle(UiMode.FlightDetail))
            .IsNotEqualTo("Flight Detail");
        await Assert.That(PaneText.ModalTitle(UiMode.HandFlight))
            .IsNotEqualTo("Hand Flight");
    }

    [Test]
    public async Task A_column_with_no_name_is_blank_rather_than_invented()
    {
        // The mark columns: the repositories' arrow, and the runners'.
        var table = CollectionViews.Rows(
            Rows.RunnerColumns,
            [["→", "01a06572  this laptop", "idle", "", "2026-09-06 12:00:00Z"]]);

        var headings = Enumerable.Range(0, table.Columns.Count)
            .Select(i => table.Columns[i].ColumnName)
            .ToList();

        await Assert.That(headings[0].Trim()).IsEmpty()
            .Because("a heading over a column of marks is a word explaining a symbol that "
                   + $"already explains itself. Found: '{headings[0]}'.");
        await Assert.That(headings).Contains("working on")
            .Because("and the named ones keep their names.");
    }

    [Test]
    public async Task Two_nameless_columns_do_not_collide()
    {
        // DataTable refuses two columns with the same name, and "" twice is the
        // same name twice. Nothing needs two today, which is exactly when this
        // is cheap to hold.
        var table = CollectionViews.Rows(["", "what", ""], [["a", "b", "c"]]);

        await Assert.That(table.Columns.Count).IsEqualTo(3);
        await Assert.That(table.Rows.Count).IsEqualTo(1);
    }
}
