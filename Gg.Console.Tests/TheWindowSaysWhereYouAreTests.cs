using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The terminal's own title says which console this is and which tab is
/// showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one line of this console visible when the console is not.</b>
/// Terminal.Gui pushes the screen's title out as <c>OSC 0</c>, which is what
/// names the window, the tab strip and whatever a person alt-tabs through - so
/// it is read most often by somebody who is looking at something else.
/// </para>
/// <para>
/// <b>`gg', because that is what a person typed.</b> It said "Good Grief",
/// which is the product; the thing running in that window is the binary, and
/// the binary is `gg' in the prompt, in the install path and in every command
/// on the help page.
/// </para>
/// <para>
/// <b>And which tab, because a window title that never changes says nothing.</b>
/// Two terminals side by side, one on the queue and one reading a log, are told
/// apart by this and by nothing else.
/// </para>
/// </remarks>
public class TheWindowSaysWhereYouAreTests
{
    [Test]
    public async Task It_is_the_binarys_name_and_the_tab()
    {
        await Assert.That(PaneText.WindowTitle(new AppState { ActiveTab = TabId.Flights }))
            .IsEqualTo("gg - flights");

        await Assert.That(PaneText.WindowTitle(new AppState { ActiveTab = TabId.Queue }))
            .IsEqualTo("gg - queue");
    }

    [Test]
    public async Task Every_tab_gives_it_something_to_say()
    {
        // NO TAB IS NAMELESS, and a title falling back to the enum name would
        // put `Envelope' in the window of a tab the bar calls airspace.
        foreach (var tab in Tabs.All)
        {
            var said = PaneText.WindowTitle(new AppState { ActiveTab = tab });

            await Assert.That(said).StartsWith("gg - ");
            await Assert.That(said).EndsWith(Tabs.Name(tab))
                .Because($"{tab} is called '{Tabs.Name(tab)}' on the bar, and two words for "
                       + "one screen is the thing the airspace rename already settled.");
        }
    }

    [Test]
    public async Task It_says_the_product_nowhere()
    {
        // ASK WHY IT PASSES: "Good Grief" was the whole title, so this is the
        // assertion that would have failed before and the one that fails if it
        // creeps back.
        await Assert.That(PaneText.WindowTitle(new AppState()))
            .DoesNotContain("Good Grief", StringComparison.OrdinalIgnoreCase);
    }
}
