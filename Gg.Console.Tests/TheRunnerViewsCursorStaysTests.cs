using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The cursor in the runner modal's tables is the model's, like every other.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: selecting the second member snaps back to the
/// first.</b> The two tables were filled with a literal <c>0</c> and a comment
/// saying they carry no cursor — which was true of the model and never true of
/// the widget, so every render dragged the selection back to the top under the
/// person moving it.
/// </para>
/// <para>
/// <b>That is the defect <c>TheRunnersCursorStaysTests</c> exists for, and its
/// own remark describes it exactly</b> — the runners table once "passed a
/// literal 0 and a comment saying nothing here is selectable". Written again
/// two panes over, by the person who had just read it.
/// </para>
/// <para>
/// <b>They cannot share <c>OnRowPointedAt</c>.</b> That handler routes through
/// <c>Reducer.Pointed</c>, which moves the cursor of the ACTIVE TAB — and the
/// active tab behind this modal is Runners, so pointing at a row here would
/// change which runner the modal is about. The flight log has its own handler
/// for the same reason.
/// </para>
/// </remarks>
public class TheRunnerViewsCursorStaysTests
{
    private static AppState Open(RunnerView view) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        RunnerView = view,
        RunnerSelected = 0,
        Chart = new EnvironmentChart
        {
            Environments =
            [
                new EnvironmentCharted
                {
                    Name = "aspire-payments",
                    Meaning = "ran here",
                    Disposition = LabelDispositions.Measured,
                    ChartedBy = "Kevin",
                    ChartedAt = DateTimeOffset.UnixEpoch,
                },
            ],
        },
        Runners = new RunnerList
        {
            Runners =
            [
                Member("gg-pool-payments-1"),
                Member("gg-pool-payments-2"),
                Member("gg-pool-payments-3"),
            ],
        },
    };

    private static RunnerSummary Member(string label) => new()
    {
        RunnerId = "runner-" + label,
        Label = label,
        State = RunnerStates.Idle,
        LastHeartbeatAt = DateTimeOffset.UnixEpoch,
        Labels = [new AdvertisedLabel
        {
            Name = "environment=aspire-payments",
            Disposition = LabelDispositions.Measured,
        }],
    };

    [Test]
    public async Task Pointing_at_a_member_moves_the_member_cursor()
    {
        var pointed = Reducer.Pointed(Open(RunnerView.Members), 1);

        await Assert.That(pointed.RunnerMemberSelected).IsEqualTo(1)
            .Because("a cursor the model does not hold is one the next render puts back "
                   + "where it started.");
    }

    [Test]
    public async Task And_leaves_the_fleet_cursor_alone()
    {
        // THE WHOLE REASON THIS NEEDS ITS OWN ARM. Reducer.Pointed routes by
        // ACTIVE TAB, and the tab behind this modal is Runners - so the shared
        // handler would have moved the fleet's cursor and changed which runner
        // the modal is about, under somebody reading it.
        var pointed = Reducer.Pointed(Open(RunnerView.Members), 2);

        await Assert.That(pointed.RunnerSelected).IsEqualTo(0)
            .Because("pointing inside the modal must not change which runner it is open on.");
    }

    [Test]
    public async Task The_environments_view_has_its_own_cursor_too()
    {
        // TWO CURSORS, NOT ONE SHARED. The views have different row counts, so
        // a single index carried across a turn of the bar would land somewhere
        // nobody chose - and be clamped to a row that means something else.
        var pointed = Reducer.Pointed(Open(RunnerView.Environments), 0);

        await Assert.That(pointed.RunnerEnvironmentSelected).IsEqualTo(0);
        await Assert.That(pointed.RunnerMemberSelected).IsEqualTo(0);
    }

    [Test]
    public async Task A_row_past_the_end_is_clamped_rather_than_kept()
    {
        var pointed = Reducer.Pointed(Open(RunnerView.Members), 99);

        await Assert.That(pointed.RunnerMemberSelected).IsEqualTo(2)
            .Because("three members, so the last row is two - and a cursor past the end "
                   + "selects a row that is not there.");
    }

    [Test]
    public async Task The_tables_are_told_where_the_cursor_is()
    {
        // ASSERTED AGAINST THE SOURCE, which is how the runners table's own
        // version of this defect is held: ConsoleScreen cannot be constructed
        // without a terminal, so what a test can read is the call.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var table in (string[])["_runnerEnvironments", "_runnerMembers"])
        {
            var at = screen.IndexOf($"Fill({table}", StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1);

            var call = screen[at..screen.IndexOf(");", at, StringComparison.Ordinal)];

            await Assert.That(call).Contains("State.Runner", StringComparison.Ordinal)
                .Because($"{table} is filled with a literal cursor, so the widget's is "
                       + "thrown away on every render. Call:\n" + call);
        }
    }

    [Test]
    public async Task They_are_wired_to_their_own_handler()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen)
            .Contains("_runnerMembers.ValueChanged += OnRunnerRowPointedAt", StringComparison.Ordinal);
        await Assert.That(screen)
            .Contains("_runnerEnvironments.ValueChanged += OnRunnerRowPointedAt", StringComparison.Ordinal);

        await Assert.That(screen)
            .Contains("_runnerMembers.ValueChanged -= OnRunnerRowPointedAt", StringComparison.Ordinal);
        await Assert.That(screen)
            .Contains("_runnerEnvironments.ValueChanged -= OnRunnerRowPointedAt", StringComparison.Ordinal);
    }
}
