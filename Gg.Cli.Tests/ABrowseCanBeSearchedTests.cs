using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A listing can be narrowed by words, and a reader that cannot says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for from the console: a free text search beside the facets.</b>
/// Narrowing by area, iteration and state finds work somebody can already
/// describe by where it is filed; finding the item whose title you half
/// remember is the other half of browsing, and there was no way to do it.
/// </para>
/// <para>
/// <b>Optional, unlike the three narrowings beside it, and that is the whole
/// design.</b> <see cref="BrowseTool.CanFilter"/> demands a reader declare
/// EVERY filter it knows, so adding a fourth to that list would make every
/// reader in the field unfilterable overnight - the narrow modal would stop
/// working against readers that were fine a moment before. Searching gets its
/// own question instead, and a reader that answers no keeps everything it had.
/// </para>
/// <para>
/// <b>The title, because that is what a tracker's own quick search does.</b>
/// A body search reads customer content into a list this console shows on one
/// line, and the browse contract already refuses bodies for that reason.
/// </para>
/// </remarks>
public class ABrowseCanBeSearchedTests
{
    [Test]
    public async Task The_text_argument_is_named_once()
    {
        await Assert.That(BrowseTool.Filters.Text).IsEqualTo("text");

        await Assert.That(BrowseTool.Filters.All).DoesNotContain(BrowseTool.Filters.Text)
            .Because("CanFilter demands every name in All, so a reader that declares the "
                   + "three it always did must not become unfilterable the day this ships.");
    }

    [Test]
    public async Task A_reader_that_declares_it_can_be_searched()
    {
        await Assert.That(BrowseTool.CanSearch(
                [BrowseTool.Filters.AreaPath, BrowseTool.Filters.Iteration,
                 BrowseTool.Filters.States, BrowseTool.Filters.Text]))
            .IsTrue();
    }

    [Test]
    public async Task And_one_that_does_not_is_still_filterable()
    {
        string[] asBefore =
            [BrowseTool.Filters.AreaPath, BrowseTool.Filters.Iteration, BrowseTool.Filters.States];

        await Assert.That(BrowseTool.CanSearch(asBefore)).IsFalse();

        await Assert.That(BrowseTool.CanFilter(asBefore)).IsTrue()
            .Because("every reader in the field declares exactly these three, and the day "
                   + "this ships none of them may lose the narrowing they had.");
    }

    [Test]
    public async Task A_reader_that_declares_nothing_can_do_neither()
    {
        await Assert.That(BrowseTool.CanSearch(null)).IsFalse();
        await Assert.That(BrowseTool.CanSearch([])).IsFalse();
    }

    [Test]
    public async Task What_to_say_when_it_cannot_names_the_argument()
    {
        // NotFilterable's shape, and its reason: the person reading this is
        // usually the operator who installed the reader, and the argument is
        // what they would add.
        var said = BrowseTool.NotSearchable("a-tracker");

        await Assert.That(said).Contains("a-tracker");
        await Assert.That(said).Contains(BrowseTool.Filters.Text);
        await Assert.That(said).Contains("narrow")
            .Because("what still works is the half this does not take away.");
    }

    [Test]
    public async Task A_filter_with_words_in_it_narrows()
    {
        await Assert.That(new WorkItemFilter(Text: "login form").Narrows).IsTrue();
        await Assert.That(new WorkItemFilter().Narrows).IsFalse();

        await Assert.That(new WorkItemFilter(Text: "   ").Narrows).IsFalse()
            .Because("blank is not a search: it would read as every item, which is the "
                   + "listing somebody already has.");
    }
}
