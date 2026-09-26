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
    /// <param name="notices">
    /// How many notice lines sit above the rows. <c>PaneText.QueueRows</c> puts
    /// them there, so a list index is a model index plus this - and reading one
    /// as the other is why the last row could not be selected: the view kept
    /// reporting a number one higher than the model held, this read every
    /// redraw as a move, and Render assigned the cursor back short of the end.
    /// </param>
    public static Command? Wanted(int? fromView, int inModel, int notices = 0)
    {
        // NO SELECTION IS NOT A CHOICE. Repopulating clears it, and a redraw
        // must never look like a keystroke.
        if (fromView is not { } wanted)
        {
            return null;
        }

        // A NOTICE IS NOT A ROW SOMEBODY CAN BE ON. Below the offset there is no
        // model row to mean, and the nearest one is the top - so acting would
        // drag the cursor to the first flight because somebody clicked a
        // sentence above it.
        var row = wanted - notices;

        if (row < 0)
        {
            return null;
        }

        // AND THE ROW ALREADY HELD IS NOT A CHOICE EITHER. Render assigns
        // SelectedItem back after repopulating, which raises the event a second
        // time; acting on that would be the same recursion by a shorter route.
        if (row == inModel)
        {
            return null;
        }

        return row > inModel ? Command.SelectNext : Command.SelectPrevious;
    }
}
