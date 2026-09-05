using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The list is an input device, and a list with no selection has not chosen zero.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT, MEASURED.</b> <c>ListView.SetSource</c> resets
/// <c>SelectedItem</c> to <b>null</b> and raises <c>ValueChanged</c> while
/// doing it — verified against Terminal.Gui 2.4.17. <c>Render</c> calls
/// <c>SetSource</c> every time, and the handler read that null as
/// <c>?? 0</c>: "the person moved to row zero". So every render reduced a
/// <c>SelectPrevious</c>, re-rendered, and did it again until the cursor was
/// back at the top.
/// </para>
/// <para>
/// <b>What that costs a person is the whole pane.</b> Pressing <c>j</c> moves
/// the selection down one and the re-render drags it back, so the queue cursor
/// cannot move at all — and the queue is what every other pane hangs off, so
/// nothing below it can be reached either.
/// </para>
/// <para>
/// <b>The rule lives here rather than in the view</b> because the view is
/// Terminal.Gui and is not tested. What the view owes is to call this and obey
/// it; what this owes is to be the only place that decides.
/// </para>
/// </remarks>
public class TheQueueCursorCanMoveTests
{
    [Test]
    public async Task A_list_with_no_selection_has_not_chosen_anything()
    {
        // THE BUG. null is "this list has no selection", which is what
        // repopulating it produces - not a person picking the first row.
        await Assert.That(QueueSelection.Wanted(fromView: null, inModel: 3)).IsNull();
        await Assert.That(QueueSelection.Wanted(fromView: null, inModel: 0)).IsNull();
    }

    [Test]
    public async Task Choosing_the_row_already_chosen_moves_nothing()
    {
        // Render assigns SelectedItem after repopulating, which raises the
        // event again. Acting on it would be the same recursion by a shorter
        // route.
        await Assert.That(QueueSelection.Wanted(fromView: 2, inModel: 2)).IsNull();
    }

    [Test]
    public async Task Choosing_a_row_below_moves_down_and_above_moves_up()
    {
        await Assert.That(QueueSelection.Wanted(fromView: 3, inModel: 2))
            .IsEqualTo(Command.SelectNext);
        await Assert.That(QueueSelection.Wanted(fromView: 1, inModel: 2))
            .IsEqualTo(Command.SelectPrevious);
    }

    [Test]
    public async Task A_render_after_the_person_moved_leaves_the_cursor_where_they_put_it()
    {
        // THE WHOLE SEQUENCE, as the view performs it: a person presses j, the
        // model moves to 1, Render repopulates - which nulls the selection -
        // and then assigns it back. Neither event may move the model.
        var model = 0;

        model = Step(model, Command.SelectNext);                       // j
        await Assert.That(QueueSelection.Wanted(null, model)).IsNull(); // SetSource
        await Assert.That(QueueSelection.Wanted(model, model)).IsNull();// SelectedItem =

        await Assert.That(model).IsEqualTo(1)
            .Because("the cursor a person moved must survive the redraw that shows it.");
    }

    private static int Step(int at, Command command) =>
        command == Command.SelectNext ? at + 1 : Math.Max(0, at - 1);
}
