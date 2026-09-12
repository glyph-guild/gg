namespace Gg.Console;

/// <summary>
/// The pages the help modal offers, and the order they are offered in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in the widget that draws them.</b> A
/// <c>Terminal.Gui.Views.Tabs</c> renders these and takes its labels from
/// <see cref="Title"/>; which one is showing is <see cref="AppState.HelpPage"/>
/// and the widget follows it, the way the main tab bar already follows
/// <c>ActiveTab</c>. That is what keeps "which page is showing" assertable
/// after the console tears its views down and rebuilds them.
/// </para>
/// <para>
/// <b>One list, so the bar and the key cannot disagree.</b> <see cref="Next"/>
/// walks this and the widget is built from this, which is the same argument
/// <c>Tabs.Offered</c> makes one layer up - a bar drawn from one list and
/// cycled by another is how a tab becomes unreachable.
/// </para>
/// </remarks>
public static class HelpPages
{
    /// <summary>Every page, in the order the bar shows them.</summary>
    /// <remarks>
    /// <b>Keys first, and it is not alphabetical.</b> Help is opened to find a
    /// key far more often than to read a setting, so the first page is the one
    /// a person did not have to ask for.
    /// </remarks>
    public static IReadOnlyList<HelpPage> All { get; } =
        [HelpPage.Keys, HelpPage.Environment];

    /// <summary>What the tab is called.</summary>
    public static string Title(HelpPage page) => page switch
    {
        HelpPage.Keys => "Keys",
        HelpPage.Environment => "Environment",
        _ => page.ToString(),
    };

    /// <summary>The next page round, wrapping at the end.</summary>
    /// <remarks>
    /// <b>It wraps.</b> Stopping at the last page strands somebody there with
    /// no way back but closing the modal, which is the shape the main tab bar
    /// avoids for the same reason.
    /// </remarks>
    public static HelpPage Next(HelpPage page)
    {
        var at = All.ToList().IndexOf(page);

        return at < 0 ? All[0] : All[(at + 1) % All.Count];
    }
}
