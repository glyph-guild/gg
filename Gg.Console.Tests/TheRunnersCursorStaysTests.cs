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

    /// <summary>
    /// The four tables a tab is driven by, each told where its cursor is.
    /// </summary>
    /// <remarks>
    /// <b>Four rather than every table, since the flight modal's log arrived.</b>
    /// The count used to be the whole ratchet - "a fifth needs a cursor of its
    /// own too" - and the fifth came past it with the opposite answer, which is
    /// what the count was for. A log's cursor is a person's place in a history
    /// inside a modal; it is not kept, because a modal is a question with an
    /// answer and a way out, and <c>AppState</c> holds what is worth keeping.
    /// So the four are named here and the fifth is named below.
    /// </remarks>
    /// <summary>
    /// The tables a tab is driven by, each told where its cursor is.
    /// </summary>
    /// <remarks>
    /// <b>The airspace tree joined them and the list was not the count.</b> It
    /// passes <c>State.AirspaceSelected</c> and would have passed this ratchet
    /// on the day it arrived - but a list of four names cannot notice a fifth
    /// table, which is how it came past two ratchets and broke a third thing
    /// neither was watching. Named here so both tests below read the same list.
    /// </remarks>
    private static readonly string[] Driven =
        ["_flightsTable", "_browseTable", "_repositoriesTable", "_runnersTable",
         "_airspaceTable"];

    [Test]
    public async Task The_view_is_told_where_the_cursor_is_rather_than_where_it_started()
    {
        // THE RATCHET, because the defect was a literal 0 passed to the fill.
        // Every table a tab is driven by passes its own field, and one that
        // passed a constant would snap back exactly as the runners' did.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var table in Driven)
        {
            var call = Call(screen, $"Fill({table},");

            await Assert.That(call).IsNotNull()
                .Because($"{table} is a tab's table and is filled from the model.");
            await Assert.That(call!.Contains("Selected", StringComparison.Ordinal)).IsTrue()
                .Because("the cursor comes from the model. A constant here is a table that "
                       + $"resets under the person using it. Found:\n{call}");
        }
    }

    /// <summary>
    /// The whole call, from the opening name to the semicolon that ends it.
    /// </summary>
    /// <remarks>
    /// Naming the table rather than splitting on <c>Fill(</c>: the loose split
    /// counted <c>Dim.Fill()</c> among the fills and was only ever right by the
    /// accident of where the next one fell.
    /// </remarks>
    private static string? Call(string source, string opening)
    {
        var at = source.IndexOf(opening, StringComparison.Ordinal);

        if (at < 0)
        {
            return null;
        }

        var end = source.IndexOf(");", at, StringComparison.Ordinal);

        return end < 0 ? source[at..] : source[at..(end + 2)];
    }

    [Test]
    public async Task Every_table_a_tab_drives_is_wired_to_the_thing_that_moves_the_model()
    {
        // THE HALF THE FILL RATCHET CANNOT SEE, and the half that was actually
        // broken. The runners table was built, added to its pane, filled from
        // the model - and never subscribed, so an arrow moved the widget's own
        // cursor, raised an event nothing was listening to, and the next render
        // put it back. Reducer.Pointed had the arm; nothing called it.
        //
        // Arrows never reach Keymap at all: the table binds them itself and
        // marks them handled, so this subscription IS the keyboard for those
        // four panes.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var built = screen.Split("CollectionViews.Table()").Length - 1;
        var wired = screen.Split("ValueChanged += OnRowPointedAt").Length - 1;
        var released = screen.Split("ValueChanged -= OnRowPointedAt").Length - 1;

        await Assert.That(built).IsEqualTo(6)
            .Because("six tables, and the count is here so a seventh has to come past this. "
                   + "The sixth is the airspace tree, which replaced a Label that rendered "
                   + "a hand-counted role column - the last list-of-things pane to get a "
                   + "table.");
        await Assert.That(wired).IsEqualTo(5)
            .Because($"a tab's table that nobody subscribed is a table whose cursor the model "
                   + $"never learns about. Built {built}, wired {wired}.");
        await Assert.That(released).IsEqualTo(wired)
            .Because("and each one is let go when the session is torn down, because the "
                   + "screen is rebuilt from the model on every pass.");
    }

    /// <summary>
    /// Every fill happens while the sync flag is held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A repopulated table raises its own <c>ValueChanged</c>, which is
    /// also how a click arrives.</b> <see cref="ConsoleScreen"/>'s own
    /// <c>Fill</c> says so in as many words - <i>"the caller holds the sync
    /// flag while this runs"</i> - and it is the caller's to hold, because
    /// <c>Fill</c> is static and the flag is not.
    /// </para>
    /// <para>
    /// <b>So a fill outside the flag is a click nobody made, on every
    /// render.</b> It reaches <c>Reducer.Pointed</c> as a cursor move, assigns
    /// the model from inside a draw, and calls <c>Render</c> re-entrantly from
    /// within <c>Render</c> - once a second, for as long as the tab is open.
    /// </para>
    /// <para>
    /// <b>The airspace tree was filled after the <c>finally</c> that clears
    /// it.</b> Four fills sat inside the block and the fifth was appended
    /// below it, which is a placement defect: nothing about the call is wrong
    /// and there is no wrong argument to find. The table-count ratchet counted
    /// it and the cursor ratchet did not know its name, so neither could see
    /// where it sat - hence a ratchet about position rather than about
    /// arguments.
    /// </para>
    /// </remarks>
    [Test]
    public async Task Every_fill_happens_while_the_sync_flag_is_held()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var unguarded = new List<string>();

        foreach (var table in Driven)
        {
            var at = screen.IndexOf($"Fill({table},", StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1)
                .Because($"{table} is a tab's table and is filled from the model.");

            // THE NEAREST ASSIGNMENT BEFORE IT, which is the state of the flag
            // when the fill runs. Held means the last one set it.
            var before = screen[..at];
            var held = before.LastIndexOf("_syncing = true", StringComparison.Ordinal);
            var cleared = before.LastIndexOf("_syncing = false", StringComparison.Ordinal);

            if (held < cleared)
            {
                unguarded.Add(table);
            }
        }

        await Assert.That(unguarded).IsEmpty()
            .Because("a fill outside the flag dispatches a cursor move nobody made, once "
                   + "per render, and renders again from inside the render that did it. "
                   + "Unguarded: " + string.Join(", ", unguarded));
    }

    /// <summary>
    /// The fifth table is subscribed, and to something else.
    /// </summary>
    /// <remarks>
    /// <b>It went from wired to nothing to wired to its own handler, and the
    /// reason it may not use this one never changed.</b> <c>Reducer.Pointed</c>
    /// dispatches on the tab that has the screen, and the tab behind the flight
    /// modal is the flights list - so a log row handed to <c>OnRowPointedAt</c>
    /// would move the cursor BEHIND the modal, leaving it about one flight
    /// while the list under it pointed at another. That is the defect the
    /// flight pane was fixed for once already, arriving through a different
    /// door. A row of the log is not an entry either, which is the second
    /// reason: <c>OnLogRowPointedAt</c> maps it through <c>LogRow.Entry</c>
    /// first.
    /// </remarks>
    [Test]
    public async Task And_the_log_is_wired_to_its_own_handler_rather_than_that_one()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("_flightLog.ValueChanged += OnRowPointedAt")
            .Because("the tab dispatcher would move the cursor behind the modal.");
        await Assert.That(screen).Contains("_flightLog.ValueChanged += OnLogRowPointedAt");
        await Assert.That(screen).Contains("_flightLog.ValueChanged -= OnLogRowPointedAt")
            .Because("and it is let go with the other four when the session is torn down.");
    }
}
