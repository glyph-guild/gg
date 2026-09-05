using System.Text.Json;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The pane that shows work to pick from, and what it says when there is none.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.4-01, -02 and -05.</b> The listing is state like everything else the
/// console draws, so it is reduced into <see cref="AppState"/>, rendered by
/// <see cref="PaneText"/>, and round-trips through the dump. What it is NOT is
/// a second way to fetch: the reader answers, the reducer records, the pane
/// renders.
/// </para>
/// <para>
/// <b>THE PROVIDER KEY IS ON THE SCREEN, not implied.</b> A tenant may
/// configure more than one tracker, and a list of work items with no
/// attribution is a list a person cannot act on - two trackers can both hold an
/// item 26.
/// </para>
/// <para>
/// <b>Titles are customer content, and this is where that gets decided.</b>
/// They have to be in state, because choosing without them is choosing by
/// number. So the safety is where <c>ConsoleData.BundleFrom</c> already puts
/// it: the bundle takes the whole state and deliberately reads almost none of
/// it, and the test plants the needle in scope and proves it does not come out.
/// That is the same disposition <c>Live</c> and <c>Held</c> already have, and
/// choosing it deliberately is what the slice asked for.
/// </para>
/// </remarks>
public class BrowsePaneTests
{
    private static BrowseListing Listing(params string[] titles) => new()
    {
        ProviderKey = "a-tracker",
        Items = [.. titles.Select((title, at) => new BrowseRow
        {
            Id = (18000 + at).ToString(null as IFormatProvider),
            Title = title,
            State = "Active",
            Updated = "2026-09-05T01:06:13Z",
        })],
    };

    [Test]
    public async Task The_pane_lists_the_work_and_names_the_tracker_it_came_from()
    {
        var state = new AppState { Browse = Listing("Oz asks guided questions", "Story 3") };

        var text = PaneText.Browse(state);

        await Assert.That(text).Contains("18000");
        await Assert.That(text).Contains("Oz asks guided questions");
        await Assert.That(text).Contains("Story 3");
        await Assert.That(text).Contains("a-tracker")
            .Because("two trackers can both hold an item 26, so a list with no attribution "
                   + "is a list a person cannot act on.");
    }

    [Test]
    public async Task No_reader_configured_is_a_stated_absence_that_names_the_variable()
    {
        // THE GG_POOL_ENDPOINT SHAPE: refused loudly, naming the variable. A
        // person whose browse pane is empty needs to know it is configuration
        // and which line to write.
        var state = new AppState { Browse = null };

        var text = PaneText.Browse(state);

        await Assert.That(text).Contains(Gg.Local.IntentConfiguration.ServedVariable);
        await Assert.That(text).Contains(Gg.Local.IntentConfiguration.ReadersVariable)
            .Because("both are ways to declare a reader and an operator may want either.");
    }

    [Test]
    public async Task A_tracker_with_no_work_says_so_rather_than_showing_an_empty_box()
    {
        // The third silence, and the only one that means "there is nothing for
        // you". An empty box cannot say which of the five it is.
        var state = new AppState
        {
            Browse = new BrowseListing { ProviderKey = "a-tracker", Items = [] },
        };

        var text = PaneText.Browse(state);

        await Assert.That(text).Contains("a-tracker");
        await Assert.That(text).Contains("no work")
            .Because("the tracker answered, and the answer was nothing.");
    }

    [Test]
    public async Task A_reader_that_could_not_answer_shows_its_own_words()
    {
        // Carried through, never reworded: the reader already said why, and a
        // second wording is a second answer to one question.
        var state = new AppState
        {
            Browse = new BrowseListing
            {
                ProviderKey = "a-tracker",
                Absence = "The reader for 'a-tracker' did not answer within 5000ms.",
            },
        };

        var text = PaneText.Browse(state);

        await Assert.That(text).Contains("did not answer within 5000ms");
    }

    [Test]
    public async Task A_page_that_continues_says_so()
    {
        // A person who cannot tell a full page from the whole backlog stops
        // looking at the first screenful.
        var state = new AppState
        {
            Browse = Listing("One") with { NextCursor = "50" },
        };

        await Assert.That(PaneText.Browse(state)).Contains("more");
        await Assert.That(PaneText.Browse(new AppState { Browse = Listing("One") }))
            .DoesNotContain("more")
            .Because("saying there is more when there is not is worse than saying nothing.");
    }

    [Test]
    public async Task A_listing_round_trips_through_the_dump()
    {
        // GG_STATE_DUMP has to reproduce what the screen showed, or a report
        // about a pane cannot be read against the state that drew it.
        var state = new AppState { Browse = Listing("Oz asks guided questions") };

        var json = JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var back = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState);

        await Assert.That(back!.Browse).IsNotNull();
        await Assert.That(back.Browse!.Items).Count().IsEqualTo(1);
        await Assert.That(back.Browse.Items[0].Title).IsEqualTo("Oz asks guided questions");
    }
}
