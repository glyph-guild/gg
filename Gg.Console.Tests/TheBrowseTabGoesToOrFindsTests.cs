using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// One field on the browse tab: a number goes to that item, words find items.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for as two things, built as one.</b> "Go to a ticket" and "search
/// for one" are the same act from a person's side - you know what you are
/// after and you type it - and what you typed says which it was. A tracker id
/// is digits; a title is not.
/// </para>
/// <para>
/// <b><c>ctrl+/</c>, beside the <c>/</c> that narrows.</b> Every plain letter
/// in this console is bound, and the two keys read as a pair: one picks from
/// what the tracker offers, the other says what you are looking for. The
/// translator gives ctrl+/ one meaning on every terminal -
/// <c>CtrlSlashArrivesTwoWaysTests</c> - because it arrives two ways.
/// </para>
/// <para>
/// <b>A field rather than a dialog</b>, for <c>AirspacePath</c>'s reason: a
/// title contains letters, so the keymap must not answer them, and a box with
/// a title around one input is ceremony around a search.
/// </para>
/// </remarks>
public class TheBrowseTabGoesToOrFindsTests
{
    private static AppState Browsing() => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
        ReaderKeys = ["a-tracker"],
    };

    [Test]
    public async Task Ctrl_slash_opens_the_field_on_the_browse_tab()
    {
        var context = KeymapContext.For(Browsing());

        await Assert.That(Keymap.Resolve(KeyStroke.Control('/'), context))
            .IsEqualTo(Command.FindInBrowse);

        await Assert.That(Keymap.Hints(context)).Contains("ctrl+/ go to or find");
    }

    [Test]
    public async Task And_the_slash_beside_it_still_narrows()
    {
        var context = KeymapContext.For(Browsing());

        await Assert.That(Keymap.Resolve(KeyStroke.Char('/'), context))
            .IsEqualTo(Command.FilterBrowse)
            .Because("the pair is the point: one picks from what the tracker offers, the "
                   + "other says what you are after.");
    }

    [Test]
    public async Task Neither_is_offered_on_another_tab()
    {
        var elsewhere = KeymapContext.For(Browsing() with { ActiveTab = TabId.Queue });

        await Assert.That(Keymap.Resolve(KeyStroke.Control('/'), elsewhere)).IsNull();
    }

    [Test]
    public async Task Inside_the_field_every_letter_is_the_fields_own()
    {
        // THE WHOLE REASON THIS IS A MODE. A title has letters in it, and a
        // keymap that answered `f' would make it untypeable - AirspacePath's
        // rule, one tab over.
        var typing = KeymapContext.For(Browsing() with { Mode = UiMode.BrowseFind });

        foreach (var letter in "abcdefghijklmnopqrstuvwxyz0123456789")
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char(letter), typing)).IsNull();
        }

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, typing))
            .IsEqualTo(Command.GoToOrFind);
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, typing)).IsEqualTo(Command.CloseModal);
    }

    [Test]
    public async Task A_number_is_an_item_to_go_to()
    {
        await Assert.That(BrowseFind.Wanted(" 18490 ")).IsEqualTo(new BrowseFind.Wish.AnItem("18490"));
    }

    [Test]
    public async Task Anything_else_is_words_to_find()
    {
        await Assert.That(BrowseFind.Wanted("login form"))
            .IsEqualTo(new BrowseFind.Wish.SomeWords("login form"));

        // AN ID WITH A LETTER IN IT IS NOT A NUMBER, and this console does not
        // know which trackers number their items and which key them - so the
        // rule is the narrow one: digits, and nothing else, are an id.
        await Assert.That(BrowseFind.Wanted("GG-153"))
            .IsEqualTo(new BrowseFind.Wish.SomeWords("GG-153"));
    }

    [Test]
    public async Task And_nothing_typed_is_nothing_asked()
    {
        await Assert.That(BrowseFind.Wanted("")).IsNull();
        await Assert.That(BrowseFind.Wanted("   ")).IsNull()
            .Because("a search for spaces is the listing somebody already has, and a page "
                   + "fetched for it is a page nobody asked for.");
    }

    [Test]
    public async Task What_was_typed_is_kept_so_the_read_can_use_it()
    {
        var typed = Reducer.BrowseFindTyped(Browsing(), "login form");

        await Assert.That(typed.BrowseFindTyped).IsEqualTo("login form");
    }

    [Test]
    public async Task Words_reach_the_reader_as_a_text_filter()
    {
        // THE JOIN. The field is where a person types; the filter is what the
        // tracker is asked - and a search that stopped at the model would be a
        // box that swallows what somebody typed.
        var asked = ConsoleBrowsing.Searching(Browsing() with { BrowseFindTyped = "login form" });

        await Assert.That(asked?.Text).IsEqualTo("login form");
    }

    [Test]
    public async Task And_a_search_replaces_the_facets_rather_than_joining_them()
    {
        // A PERSON WHO NARROWED TO A SPRINT AND THEN SEARCHED is looking in the
        // whole tracker, not in that sprint: the second act is the one they
        // just performed, and anding them answers an empty list for a reason
        // nothing on the screen explains.
        var narrowed = Browsing() with
        {
            ChosenAreaPath = "Payments",
            ChosenIteration = "Sprint 9",
            BrowseFindTyped = "login form",
        };

        var asked = ConsoleBrowsing.Searching(narrowed);

        await Assert.That(asked?.Text).IsEqualTo("login form");
        await Assert.That(asked?.AreaPath).IsNull();
        await Assert.That(asked?.Iteration).IsNull();
    }
}
