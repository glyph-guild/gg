namespace Gg.Console;

/// <summary>
/// What a change of selection in the queue list means, if anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>NULL IS NOT ZERO, and reading it as zero cost the pane its cursor.</b>
/// <c>ListView.SetSource</c> resets <c>SelectedItem</c> to null and raises its
/// change event while doing so. The view repopulates on every render, so a
/// handler that treated null as "row zero was chosen" reduced a
/// <c>SelectPrevious</c> every redraw, re-rendered, and repeated until the
/// cursor was back at the top — which is a queue whose selection cannot move,
/// and therefore a console whose other panes cannot be reached.
/// </para>
/// <para>
/// <b>The list is an input device, not a second store.</b> The model decides
/// what is selected and the view reports what a person clicked. That contract
/// only holds if the view's own bookkeeping is distinguishable from a person's
/// choice, and the difference is exactly this: a person always chooses a row,
/// and only repopulating produces none.
/// </para>
/// <para>
/// <b>Here rather than in the view</b> because <c>ConsoleScreen</c> is
/// Terminal.Gui and is not unit-tested, while what an event MEANS is a decision
/// that needs no terminal to check.
/// </para>
/// </remarks>
public static class QueueSelection
{
    /// <summary>
    /// The move a person asked for, or null when they asked for nothing.
    /// </summary>
    /// <param name="fromView">What the list now reports, or null for no selection.</param>
    /// <param name="inModel">What the model currently holds.</param>
    public static Command? Wanted(int? fromView, int inModel)
    {
        // NO SELECTION IS NOT A CHOICE. Repopulating clears it, and a redraw
        // must never look like a keystroke.
        if (fromView is not { } wanted)
        {
            return null;
        }

        // AND THE ROW ALREADY HELD IS NOT A CHOICE EITHER. Render assigns
        // SelectedItem back after repopulating, which raises the event a second
        // time; acting on that would be the same recursion by a shorter route.
        if (wanted == inModel)
        {
            return null;
        }

        return wanted > inModel ? Command.SelectNext : Command.SelectPrevious;
    }
}
