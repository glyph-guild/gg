using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// Tab moves to the tab drawn next to this one.
/// </summary>
/// <remarks>
/// <para>
/// <b>It jumped, because two lists disagreed about one order.</b>
/// <c>Tabs.Next</c> cycles <c>Tabs.All</c>, which is the enum's declaration
/// order; the bar draws whatever order <c>_tabbed</c> lists. <c>Runners</c> was
/// declared third and appended seventh, so tab from Flights went to a tab six
/// places along and a person following the highlight watched it skip.
/// </para>
/// <para>
/// <b>Neither list was wrong on its own, which is why nothing caught it.</b>
/// Every tab is in both, exactly once, and every existing guard asks about
/// membership - is every tab on the bar, does every tab have a key, is every
/// tab reachable. Order is the one property no guard held, and it is the only
/// one a person navigating with the keyboard can see.
/// </para>
/// </remarks>
public class TabGoesLeftToRightTests
{
    /// <summary>The tabs the bar is built from, in the order it builds them.</summary>
    private static IReadOnlyList<TabId> AsDrawn()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");
        var opened = screen.IndexOf("_tabbed =", StringComparison.Ordinal);
        var closed = screen.IndexOf("];", opened, StringComparison.Ordinal);

        return
        [
            .. Regex.Matches(screen[opened..closed], @"\(TabId\.(\w+),")
                .Select(m => Enum.Parse<TabId>(m.Groups[1].Value)),
        ];
    }

    [Test]
    public async Task The_bar_draws_them_in_the_order_tab_walks_them()
    {
        await Assert.That(AsDrawn()).IsNotEmpty()
            .Because("the list this is about has to be found before anything is claimed of "
                   + "it - a slice that found nothing would pass by matching nothing.");

        // SEQUENCE, NOT SET. IsEquivalentTo ignores order by default, which is
        // precisely the property in question - the two lists already hold the
        // same tabs and always did.
        await Assert.That(AsDrawn().SequenceEqual(Tabs.All)).IsTrue()
            .Because("`tab' means the next one along, and the only thing that makes that "
                   + $"true is these two agreeing. Drawn: {string.Join(", ", AsDrawn())}. "
                   + $"Walked: {string.Join(", ", Tabs.All)}.");
    }

    [Test]
    public async Task Tab_from_each_one_reaches_the_next_and_wraps_at_the_end()
    {
        var drawn = AsDrawn();

        for (var i = 0; i < drawn.Count; i++)
        {
            var from = new AppState { ActiveTab = drawn[i] };
            var expected = drawn[(i + 1) % drawn.Count];

            await Assert.That(Reducer.Reduce(from, Command.FocusNextPane).ActiveTab)
                .IsEqualTo(expected)
                .Because($"from {drawn[i]}, the next one drawn is {expected}.");
        }
    }
}
