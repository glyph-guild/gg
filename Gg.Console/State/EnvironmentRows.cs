using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// One charted environment, and what is known about what furnishes it.
/// </summary>
/// <remarks>
/// <b>A record per row, for <c>Rows.cs</c>'s reason</b> — the values stay
/// checkable without a terminal, and the alignment is left to something that
/// can measure the screen.
/// </remarks>
/// <param name="Environment">The charted name, with its disposition when it has one worth saying.</param>
/// <param name="Strategy">The document that furnishes it, as name@version, or empty.</param>
/// <param name="Pool">The pool that strategy manages, or empty.</param>
/// <param name="Wants">How many the strategy keeps ready, of how many the pool may hold.</param>
/// <param name="Attested">What the pull point last observed about that pool, or empty.</param>
/// <param name="Measured">When it observed it, or empty.</param>
public sealed record EnvironmentRow(
    string Environment,
    string Strategy,
    string Pool,
    string Wants,
    string Attested,
    string Measured);

/// <summary>
/// What is running for one charted environment, as far as the fleet can see.
/// </summary>
/// <param name="Environment">The charted name this row is about.</param>
/// <param name="Member">The runner's own label, or empty when nothing advertises the name.</param>
/// <param name="State">idle, busy or offline - or why there is no runner at all.</param>
/// <param name="Work">The flight it is holding, when it holds one.</param>
/// <param name="Heard">When it last beat, or empty.</param>
public sealed record MemberRow(
    string Environment,
    string Member,
    string State,
    string Work,
    string Heard);

/// <summary>
/// The chart, joined with the strategies that furnish it and the pools'
/// latest word about themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CHART IS THE LIST, and nothing else is.</b> A strategy names the
/// environment it furnishes and a runner advertises one as a label, so both
/// mention environments — but neither enumerates them, and a charted name
/// nothing furnishes appears in neither. A pane built from the strategies
/// would show a tenant a shorter list than the one their envelopes are refused
/// against, which is the list that matters: an envelope naming an uncharted
/// environment is refused pointing at the chart.
/// </para>
/// <para>
/// <b>NOTHING HERE IS MEASURED, and no column may imply it was.</b>
/// <see cref="EnvironmentRow.Wants"/> is two numbers off a document somebody
/// wrote. <see cref="EnvironmentRow.Attested"/> is the pull point's own word
/// about a POOL — not about a container, because an attestation carries no
/// member name. How many containers are actually running is on neither, so
/// nothing here says.
/// </para>
/// <para>
/// <b>PURE, AND THE ORDER IS THE ORDER A CURSOR INDEXES</b> — <c>Rows.cs</c>'s
/// rule. Ordinal by name, so a chart read twice puts the same name under the
/// same cursor.
/// </para>
/// </remarks>
public static class EnvironmentRows
{
    public static IReadOnlyList<string> EnvironmentColumns { get; } =
        ["environment", "strategy", "pool", "wants", "attested", "measured"];

    /// <summary>
    /// The charted name the cursor is on, or null when there is no chart.
    /// </summary>
    /// <remarks>
    /// The NAME rather than the row, because what a pane says more about is
    /// keyed on the name in all three reads - and the row's own first cell has
    /// the disposition folded into it, so it is a label rather than a key.
    /// </remarks>
    public static string? Pointed(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var charted = Charted(state);

        return state.EnvironmentSelected >= 0 && state.EnvironmentSelected < charted.Count
            ? charted[state.EnvironmentSelected].Name
            : null;
    }

    /// <summary>Every charted name, in the order the rows are drawn.</summary>
    public static IReadOnlyList<EnvironmentCharted> Charted(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Chart is not { } chart
            ? []
            : [.. chart.Environments.OrderBy(e => e.Name, StringComparer.Ordinal)];
    }

    /// <summary>The strategy in force for a charted name, or null.</summary>
    /// <remarks>
    /// <b>Exactly one governs a name, by the document's own rule</b> — a
    /// strategy never composes — but two could name the same ENVIRONMENT, which
    /// is a different thing and not refused anywhere this console can see. The
    /// first by name is taken, deterministically, rather than one of them
    /// arbitrarily.
    /// </remarks>
    public static EnvironmentStrategyState? Furnishing(AppState state, string environment)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Strategies?.Strategies
            .Where(s => string.Equals(s.Strategy.Environment, environment, StringComparison.Ordinal))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// What a pool last reported when it looked at itself.
    /// </summary>
    /// <remarks>
    /// <b>Verify, and never whichever action came last.</b> Refresh and reset
    /// are acts; verify is the observation, and the ledger carries the latest
    /// of each per pool. A row taking the most recent of the three would read a
    /// failed reset as the pool's health, when what it means is that somebody
    /// rebuilt a container.
    /// </remarks>
    public static PoolStatus? Verified(AppState state, string pool)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Pools?.Pools.FirstOrDefault(
            p => string.Equals(p.Pool, pool, StringComparison.Ordinal)
              && string.Equals(p.Action, PoolActions.Verify, StringComparison.Ordinal));
    }

    /// <summary>One row per charted name, whether or not anything furnishes it.</summary>
    public static IReadOnlyList<EnvironmentRow> Environments(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. Charted(state).Select(charted => Row(state, charted))];
    }

    private static EnvironmentRow Row(AppState state, EnvironmentCharted charted)
    {
        var furnishing = Furnishing(state, charted.Name);
        var inventory = furnishing?.Strategy.Inventory;
        var attested = inventory is null ? null : Verified(state, inventory.Pool);

        return new EnvironmentRow(
            Environment: Says(charted),
            Strategy: furnishing is null ? "" : $"{furnishing.Name}@{furnishing.Version}",
            Pool: inventory?.Pool ?? "",

            // TWO DECLARED NUMBERS, SAID AS ONE. How many the strategy keeps
            // ready and how many the pool may hold are both authored, and the
            // pair is what a person checks a pool against - "wants 2 of 4" is
            // the sentence, and the column is named for the half that makes it
            // a declaration rather than a count.
            Wants: inventory is null ? "" : $"{inventory.Warm} of {inventory.Size}",
            Attested: attested?.Outcome ?? "",

            // ABSOLUTE, like the fleet's last-heard beside it. Nothing on the
            // model carries a clock, so an age would have to be computed in the
            // view - and the two panes would then disagree about what "now" is.
            Measured: attested is { } status ? status.MeasuredAt.ToString("u") : "");
    }

    public static IReadOnlyList<string> MemberColumns { get; } =
        ["environment", "member", "state", "working on", "last heard"];

    /// <summary>
    /// The label a runner advertises to be matched for an environment.
    /// </summary>
    /// <remarks>
    /// <b>THE CONTROL PLANE'S SPELLING, READ HERE RATHER THAN OWNED.</b>
    /// <c>AdvertisedLabel</c> documents the shape — <i>"the label as matched,
    /// e.g. environment=aspire-payments"</i> — and a checklist's
    /// <c>requiredLabels</c> carry it; gg composes neither, so there is no
    /// constant to import and this is the one place the convention is written
    /// down on this side.
    /// <para>
    /// <b>Exact, because both ways of being loose are wrong.</b> Matching the
    /// bare name would count <c>region=staging</c> as a runner for
    /// <c>staging</c>. Matching a prefix nobody uses would count nothing — and
    /// count it silently, which reads as "bring up a machine you already
    /// have" when one is already up.
    /// </para>
    /// </remarks>
    private const string AdvertisedAs = "environment=";

    /// <summary>
    /// One row per runner furnishing a charted name, and one for each name
    /// nothing furnishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>RUNNERS, NOT CONTAINERS, and the difference is not cosmetic.</b> gg
    /// cannot enumerate a pool: the only thing that talks to a container
    /// runtime is <c>IPoolAdapter</c>, which lives in <c>Gg.Runner</c> behind a
    /// proxy on the pool host's loopback and is not referenced by this project
    /// at all. A member that redeemed its nonce is a runner like any other; one
    /// created and never redeemed appears nowhere here, which is exactly the
    /// "counted as warm forever" failure the contract warns about — so nothing
    /// in these rows may be read as a count of what exists.
    /// </para>
    /// <para>
    /// <b>A CHARTED NAME NOTHING ADVERTISES IS STILL A ROW.</b> That is the
    /// state somebody acts on: it is why a flight selecting the environment
    /// waits, and dropping the row would leave them looking at a tab with
    /// nothing in it for the one name they came to check.
    /// </para>
    /// <para>
    /// <b>A RUNNER ADVERTISING SOMETHING UNCHARTED IS NOT HERE.</b> It is not
    /// an environment an envelope may select, so this pane is not about it -
    /// and including it would make the tab a second fleet listing that happens
    /// to sort differently. The Runners tab is where every runner is.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MemberRow> Members(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var fleet = state.Runners?.Runners ?? [];

        return
        [
            .. Charted(state).SelectMany(charted =>
            {
                var advertising = fleet
                    .Where(r => r.Labels.Any(
                        l => string.Equals(
                            l.Name, AdvertisedAs + charted.Name, StringComparison.Ordinal)))
                    .OrderBy(r => r.Label, StringComparer.Ordinal)
                    .ToList();

                return advertising.Count == 0
                    ? (IEnumerable<MemberRow>)[Nobody(charted.Name)]
                    : advertising.Select(r => Running(charted.Name, r));
            }),
        ];
    }

    /// <summary>
    /// A charted name with nothing advertising it.
    /// </summary>
    /// <remarks>
    /// <b>The control plane's own words</b> — <c>ReasonKinds.NoRunnerAdvertises</c>
    /// renders "waiting: no runner advertises environment=…" for a flight stuck
    /// on exactly this. Two surfaces describing one state differently is how a
    /// person ends up believing they are two states.
    /// </remarks>
    private static MemberRow Nobody(string environment) =>
        new(environment, Member: "", State: "no runner advertises it", Work: "", Heard: "");

    private static MemberRow Running(string environment, Gg.Contracts.RunnerSummary runner) =>
        new(
            environment,

            // THE RUNNER'S OWN LABEL, which for a pool member is whatever the
            // control plane named it at mint. gg does not choose it and cannot
            // check it, so it is shown rather than parsed.
            Member: runner.Label,

            // BOTH FACTS OR NEITHER, which is Rows.Runners' rule one pane over:
            // a runner can be parked AND busy, and a column printing only the
            // state shows a machine somebody deliberately withheld as idle.
            State: runner.ParkedAt is null ? runner.State : $"{runner.State} · parked",
            Work: runner.CurrentFlightNumber ?? "",
            Heard: runner.LastHeartbeatAt is { } at ? at.ToString("u") : "never");

    /// <summary>
    /// The name, and what its word is worth.
    /// </summary>
    /// <remarks>
    /// <c>measured</c> means the chart registered a meaning — a predicate the
    /// control plane evaluates from produced facts — and is the ordinary case,
    /// so it costs no words. <c>stated</c> means somebody named it and nothing
    /// checks. <c>Rows.Advertised</c>'s shape one pane over, and the contract's
    /// own rule: the disposition travels with the name everywhere the name
    /// does, because the hazard was never the claim, it is a claim wearing
    /// measurement's clothes.
    /// </remarks>
    private static string Says(EnvironmentCharted charted) =>
        string.Equals(charted.Disposition, LabelDispositions.Measured, StringComparison.Ordinal)
            ? charted.Name
            : $"{charted.Name} ({charted.Disposition})";
}
