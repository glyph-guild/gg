using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The cursor on the runners table stays where a person put it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It snapped back to the top on every redraw.</b> The tab was added with
/// the table's cursor hard-coded to row 0 and a comment saying nothing on it is
/// selectable - which was true of the model and never true of the widget. A
/// table a person can move the cursor in, whose cursor is reassigned on every
/// render, reads as a keyboard that is ignoring them.
/// </para>
/// <para>
/// <b>The model owns the selection, and this is the third table to learn
/// it.</b> Flights, browse and repositories each carry theirs;
/// <c>Reducer.Pointed</c> and <c>Moved</c> dispatch on the active tab and had
/// no arm for this one, so a click or an arrow reached the reducer and returned
/// the state unchanged.
/// </para>
/// </remarks>
public class TheRunnersCursorStaysTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private static AppState Fleet(int count) => new()
    {
        ActiveTab = TabId.Runners,
        Runners = new RunnerList
        {
            Runners =
            [
                .. Enumerable.Range(1, count).Select(n => new RunnerSummary
                {
                    RunnerId = $"runner-{n}",
                    Label = $"machine {n}",
                    State = RunnerStates.Idle,
                    LastHeartbeatAt = T0,
                }),
            ],
        },
    };

    [Test]
    public async Task An_arrow_moves_it_and_the_model_remembers()
    {
        var state = Fleet(5);

        state = Reducer.Reduce(state, Command.SelectNext);
        state = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(state.RunnerSelected).IsEqualTo(2);

        state = Reducer.Reduce(state, Command.SelectPrevious);

        await Assert.That(state.RunnerSelected).IsEqualTo(1);
    }

    [Test]
    public async Task A_click_lands_on_the_row_that_was_clicked()
    {
        // Through the reducer, with the row. The queue's list can only say up or
        // down, so a click five rows down moved the cursor one - a table hands
        // over the row it landed on.
        var pointed = Reducer.Pointed(Fleet(6), 4);

        await Assert.That(pointed.RunnerSelected).IsEqualTo(4);
    }

    [Test]
    public async Task It_cannot_be_moved_off_the_end_of_the_list()
    {
        var state = Fleet(2);

        for (var i = 0; i < 6; i++)
        {
            state = Reducer.Reduce(state, Command.SelectNext);
        }

        await Assert.That(state.RunnerSelected).IsEqualTo(1)
            .Because("a cursor past the last row is a cursor on nothing, and the widget would "
                   + "put it back without telling the model.");

        for (var i = 0; i < 6; i++)
        {
            state = Reducer.Reduce(state, Command.SelectPrevious);
        }

        await Assert.That(state.RunnerSelected).IsEqualTo(0);
    }

    [Test]
    public async Task Moving_on_this_tab_moves_nothing_on_another()
    {
        // THE LATENT BUG THAT BIT ONCE ALREADY, when Moved keyed on the pane
        // flags rather than the active tab and j moved the wrong list.
        var state = Reducer.Reduce(Fleet(5), Command.SelectNext);

        await Assert.That(state.RunnerSelected).IsEqualTo(1);
        await Assert.That(state.SelectedRow).IsEqualTo(0);
        await Assert.That(state.FlightSelected).IsEqualTo(0);
        await Assert.That(state.RepositorySelected).IsEqualTo(0);
        await Assert.That(state.BrowseSelected).IsEqualTo(0);
    }

    [Test]
    public async Task The_view_is_told_where_the_cursor_is_rather_than_where_it_started()
    {
        // THE RATCHET, because the defect was a literal 0 passed to the fill.
        // Every other table passes its own field, and a fourth one that passed
        // a constant would snap back exactly as this one did.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var fills = screen.Split("Fill(")
            .Skip(1)
            .Where(after => after.Contains("Rows.", StringComparison.Ordinal))
            .ToList();

        await Assert.That(fills).Count().IsEqualTo(4)
            .Because("four tables, four fills - a fifth needs a cursor of its own too.");

        foreach (var fill in fills)
        {
            await Assert.That(fill.Contains("Selected", StringComparison.Ordinal)).IsTrue()
                .Because("the cursor comes from the model. A constant here is a table that "
                       + $"resets under the person using it. Found:\n{fill[..120]}");
        }
    }

    [Test]
    public async Task Every_table_is_wired_to_the_thing_that_moves_the_model()
    {
        // THE HALF THE FILL RATCHET CANNOT SEE, and the half that was actually
        // broken. The runners table was built, added to its pane, filled from
        // the model - and never subscribed, so an arrow moved the widget's own
        // cursor, raised an event nothing was listening to, and the next render
        // put it back. Reducer.Pointed had the arm; nothing called it.
        //
        // Arrows never reach Keymap at all: the table binds them itself and
        // marks them handled, so this subscription IS the keyboard for these
        // four panes.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var built = screen.Split("CollectionViews.Table()").Length - 1;
        var wired = screen.Split("ValueChanged += OnRowPointedAt").Length - 1;
        var released = screen.Split("ValueChanged -= OnRowPointedAt").Length - 1;

        await Assert.That(built).IsEqualTo(4)
            .Because("four tables, and the count is here so a fifth has to come past this.");
        await Assert.That(wired).IsEqualTo(built)
            .Because($"a table nobody subscribed is a table whose cursor the model never "
                   + $"learns about. Built {built}, wired {wired}.");
        await Assert.That(released).IsEqualTo(built)
            .Because("and each one is let go when the session is torn down, because the "
                   + "screen is rebuilt from the model on every pass.");
    }
}
