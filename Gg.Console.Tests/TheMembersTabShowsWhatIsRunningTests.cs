using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// What is actually running for each charted environment, as far as the fleet
/// can see it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HONEST LIMIT, AND IT IS THE WHOLE DESIGN OF THIS TAB.</b> gg cannot
/// enumerate a pool's containers. <c>IPoolAdapter</c> is the only thing that
/// talks to a container runtime, it lives in <c>Gg.Runner</c> behind a proxy on
/// the pool host's loopback, and <c>Gg.Console</c> cannot even reference that
/// project. An attestation would be the other route and it carries no member
/// name, so the ledger collapses a pool of four to one row.
/// </para>
/// <para>
/// <b>So these rows are RUNNERS, not containers</b>, and the tab says so. A
/// member that came up and redeemed its nonce is a runner like any other; one
/// that was created and never redeemed is invisible here, which is precisely
/// the "counted as warm forever" failure the contract warns about — and naming
/// the rows honestly is what keeps this tab from hiding it behind a number.
/// </para>
/// <para>
/// <b>A charted name with nothing advertising it is still a row.</b> That is
/// the state a person acts on: it is why a flight selecting that environment
/// waits, and the control plane's own word for it is
/// <c>no-runner-advertises</c>.
/// </para>
/// </remarks>
public class TheMembersTabShowsWhatIsRunningTests
{
    private static EnvironmentCharted Charted(string name) => new()
    {
        Name = name,
        Meaning = "ran here",
        Disposition = LabelDispositions.Measured,
        ChartedBy = "Kevin",
        ChartedAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
    };

    private static RunnerSummary Runner(
        string label, string state, string? flight = null, params string[] labels) => new()
        {
            RunnerId = "runner-" + label,
            Label = label,
            State = state,
            CurrentFlightNumber = flight,
            LastHeartbeatAt = DateTimeOffset.Parse("2026-09-12T14:22:01Z"),
            Labels = [.. labels.Select(l => new AdvertisedLabel
        {
            Name = l, Disposition = LabelDispositions.Measured,
        })],
        };

    private static AppState Loaded() => new()
    {
        Chart = new EnvironmentChart
        {
            Environments = [Charted("aspire-payments"), Charted("staging")],
        },
        Runners = new RunnerList
        {
            Runners =
            [
                Runner("gg-pool-payments-1", RunnerStates.Idle,
                       labels: "environment=aspire-payments"),
                Runner("gg-pool-payments-2", RunnerStates.Busy, flight: "GG-1042",
                       labels: "environment=aspire-payments"),
                Runner("kev-laptop", RunnerStates.Idle, labels: "environment=dev"),
            ],
        },
    };

    [Test]
    public async Task A_runner_advertising_a_charted_name_is_a_row_under_it()
    {
        var rows = EnvironmentRows.Members(Loaded());

        var payments = rows.Where(r => r.Environment == "aspire-payments").ToList();

        await Assert.That(payments.Select(r => r.Member))
            .IsEquivalentTo((string[])["gg-pool-payments-1", "gg-pool-payments-2"]);

        await Assert.That(payments.Single(r => r.Member.EndsWith('2')).Work).IsEqualTo("GG-1042");
    }

    [Test]
    public async Task A_charted_name_nothing_advertises_says_so_rather_than_vanishing()
    {
        // THE STATE A PERSON ACTS ON. It is why a flight selecting this
        // environment waits, and saying nothing would leave them looking at a
        // tab with no row for the one name they care about.
        var row = EnvironmentRows.Members(Loaded()).Single(r => r.Environment == "staging");

        await Assert.That(row.Member).IsEmpty();
        await Assert.That(row.State).IsEqualTo("no runner advertises it")
            .Because("which is the control plane's own word for it - the same sentence a "
                   + "waiting flight is given, so the two cannot describe one state "
                   + "differently.");
    }

    [Test]
    public async Task A_runner_advertising_nothing_charted_is_not_here_at_all()
    {
        // THE FLEET HAS ITS OWN TAB. `kev-laptop' advertises environment=dev,
        // which nobody charted, so it is not an environment this tenant can
        // select and this pane is not about it. Showing it would make the tab a
        // second runners list that happens to sort differently.
        var rows = EnvironmentRows.Members(Loaded());

        await Assert.That(rows.Select(r => r.Member)).DoesNotContain("kev-laptop");
    }

    [Test]
    public async Task The_match_is_the_label_the_matcher_would_use()
    {
        // THE SPELLING IS THE CONTROL PLANE'S, READ HERE RATHER THAN OWNED.
        // `environment=<name>' is what AdvertisedLabel documents and what a
        // checklist's requiredLabels carry; gg composes neither. A pane that
        // matched on the bare name would count `region=dev' as a runner for
        // `dev', and one that invented its own prefix would count nothing at
        // all - and count it silently, which reads as "bring up a machine you
        // already have".
        var state = Loaded() with
        {
            Runners = new RunnerList
            {
                Runners =
                [
                    Runner("near-miss", RunnerStates.Idle, labels: "region=staging"),
                    Runner("bare", RunnerStates.Idle, labels: "staging"),
                ],
            },
        };

        var row = EnvironmentRows.Members(state).Single(r => r.Environment == "staging");

        await Assert.That(row.Member).IsEmpty()
            .Because("neither label is the one the matcher would use.");
    }

    [Test]
    public async Task Every_charted_name_appears_whether_or_not_anything_runs()
    {
        var rows = EnvironmentRows.Members(Loaded());

        await Assert.That(rows.Select(r => r.Environment).Distinct())
            .IsEquivalentTo((string[])["aspire-payments", "staging"]);
    }

    [Test]
    public async Task An_unread_chart_is_no_rows()
    {
        await Assert.That(EnvironmentRows.Members(new AppState())).IsEmpty();
    }

    [Test]
    public async Task The_columns_are_the_row_in_the_same_order()
    {
        await Assert.That(EnvironmentRows.MemberColumns)
            .IsEquivalentTo((string[])
                ["environment", "member", "state", "working on", "last heard"]);
    }

    [Test]
    public async Task The_tab_is_on_the_bar_and_m_reaches_it()
    {
        await Assert.That(Tabs.Offered(new AppState())).Contains(TabId.Members);

        // `z' RATHER THAN `m', WHICH IS WHERE THIS TEST STARTED. `m' is the
        // word's own letter and it is spoken for: ComposeChoice binds it, and
        // ComposeChoiceTests holds a stated rule that its keys may not be live
        // in the mode it opens from - `n' then `m' are two sets a person is
        // holding at one moment. Caught by that guard rather than by reading,
        // which is what it is for.
        await Assert.That(Tabs.KeyFor(TabId.Members)).IsEqualTo(KeyStroke.Char('z'));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('z'), new KeymapContext()))
            .IsEqualTo(Tabs.CommandFor(TabId.Members));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('m'), new KeymapContext())).IsNull()
            .Because("and `m' stays free in Normal, because the compose choice needs it to "
                   + "be one keypress later.");
    }

    [Test]
    public async Task It_asks_for_the_chart_the_way_its_neighbour_does()
    {
        // THE ROWS ARE KEYED ON THE CHART, so this tab needs the same read the
        // Environments tab does. The fleet is already in the model from the
        // boot; the chart is not, and a tab that assumed its neighbour had been
        // opened first would be empty for anybody who pressed `m' before `s' -
        // silently, and in a way that reads as "nothing is running".
        var command = Tabs.CommandFor(TabId.Members);

        await Assert.That(command).IsNotNull();
        await Assert.That(ShellCommands.Reads).Contains(command!.Value);
        await Assert.That(ShellCommands.Handled).DoesNotContain(command!.Value);
    }
}
