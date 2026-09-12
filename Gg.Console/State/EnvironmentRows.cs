using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// One charted environment the runner under the cursor can claim work for.
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
/// One runner that furnishes the same environment as the one under the cursor.
/// </summary>
/// <param name="Here">A mark on the runner whose modal this is, or empty.</param>
/// <param name="Environment">The charted name they share.</param>
/// <param name="Member">The runner's own label.</param>
/// <param name="State">idle, busy or offline, and whether it is parked.</param>
/// <param name="Work">The flight it is holding, when it holds one.</param>
/// <param name="Heard">When it last beat, or never.</param>
public sealed record MemberRow(
    string Here,
    string Environment,
    string Member,
    string State,
    string Work,
    string Heard);

/// <summary>
/// What the runner under the cursor runs, and what else runs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>EVERYTHING HERE IS SCOPED TO ONE RUNNER.</b> A tenant-wide list of
/// environments answers "what does this tenant have"; the question somebody has
/// with a runner open is "what does THIS machine run, and what else runs it".
/// These were two tabs on the bar first, which answered the other question.
/// </para>
/// <para>
/// <b>TWO WAYS A RUNNER IS ASSOCIATED, AND ONLY ONE IS ON THE WIRE.</b> The
/// labels a runner ADVERTISES cross on every heartbeat, so the charted names it
/// can claim work for are knowable exactly. A resident runner MAINTAINS a pool
/// and nothing says which: <see cref="RunnerSummary"/> has no pool, an
/// attestation names a pool without naming the runner that made it, and the
/// pool's name lives in a systemd unit's argument. See <see cref="Maintains"/>.
/// </para>
/// <para>
/// <b>NOTHING HERE IS MEASURED, and no column may imply it was.</b>
/// <see cref="EnvironmentRow.Wants"/> is two numbers off a document somebody
/// wrote. <see cref="EnvironmentRow.Attested"/> is the pull point's own word
/// about a POOL — not about a container, because an attestation carries no
/// member name.
/// </para>
/// </remarks>
public static class EnvironmentRows
{
    public static IReadOnlyList<string> EnvironmentColumns { get; } =
        ["environment", "strategy", "pool", "wants", "attested", "measured"];

    public static IReadOnlyList<string> MemberColumns { get; } =
        ["", "environment", "member", "state", "working on", "last heard"];

    /// <summary>The mark on the runner whose modal this is.</summary>
    private const string Mark = "→";

    /// <summary>
    /// The label a runner advertises to be matched for an environment.
    /// </summary>
    /// <remarks>
    /// <b>THE CONTROL PLANE'S SPELLING, READ HERE RATHER THAN OWNED.</b>
    /// <see cref="AdvertisedLabel"/> documents the shape — <i>"the label as
    /// matched, e.g. environment=aspire-payments"</i> — and a checklist's
    /// <c>requiredLabels</c> carry it; gg composes neither, so there is no
    /// constant to import and this is the one place the convention is written
    /// down on this side.
    /// <para>
    /// <b>Exact, because both ways of being loose are wrong.</b> Matching the
    /// bare name would count <c>region=staging</c> as a runner for
    /// <c>staging</c>. Matching a prefix nobody uses would count nothing — and
    /// count it silently, which reads as "bring up a machine you already have"
    /// when one is already up.
    /// </para>
    /// </remarks>
    private const string AdvertisedAs = "environment=";

    /// <summary>
    /// The suffix a resident runner's label carries.
    /// </summary>
    /// <remarks>
    /// <b>A fact about gg's own packaging rather than a guess about a host</b> —
    /// which is the argument <c>RunnerDetails.Suggestion</c> already makes about
    /// this same suffix: <c>&lt;machine&gt;:maintain</c> is run by the unit gg
    /// ships in <c>deploy/pool-host</c>, so gg chose the name.
    /// </remarks>
    private const string Maintainer = ":maintain";

    /// <summary>
    /// The fleet's word about the runner under the cursor, or null.
    /// </summary>
    /// <remarks>
    /// <b>The summary rather than the row</b>, because the row's labels are
    /// already joined into one display string and what these joins need is the
    /// list. A runner this console just started has a synthesised row and no
    /// summary at all, which is null here and right: nothing has heard from it.
    /// </remarks>
    public static RunnerSummary? Runner(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Rows.Selected(state) is not { } row
            ? null
            : state.Runners?.Runners.FirstOrDefault(
                r => string.Equals(r.RunnerId, row.Id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether this runner's label says it keeps a pool warm.
    /// </summary>
    /// <remarks>
    /// <b>It says THAT and never WHICH.</b> Nothing on the wire connects a
    /// runner to a pool, so naming one here would be a guess — and on a tenant
    /// with one pool it would look right, which is worse than looking wrong.
    /// </remarks>
    public static bool Maintains(AppState state) =>
        Runner(state)?.Label.EndsWith(Maintainer, StringComparison.Ordinal) is true;

    /// <summary>Every charted name, in the order rows are drawn.</summary>
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

    /// <summary>The charted names this runner advertises, in chart order.</summary>
    private static IReadOnlyList<EnvironmentCharted> Advertised(AppState state) =>
        Runner(state) is not { } runner
            ? []
            : [.. Charted(state).Where(
                charted => runner.Labels.Any(
                    l => string.Equals(
                        l.Name, AdvertisedAs + charted.Name, StringComparison.Ordinal)))];

    /// <summary>One row per charted environment this runner can claim work for.</summary>
    public static IReadOnlyList<EnvironmentRow> Environments(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. Advertised(state).Select(charted => Row(state, charted))];
    }

    /// <summary>
    /// Every runner that advertises an environment this one does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The runner itself is in the list, marked.</b> A list of peers that
    /// leaves you out is one you have to hold your place in; one that includes
    /// you without saying so is one you have to count.
    /// </para>
    /// <para>
    /// <b>RUNNERS, NOT CONTAINERS.</b> gg cannot enumerate a pool:
    /// <c>IPoolAdapter</c> is the only thing that talks to a container runtime,
    /// it lives in <c>Gg.Runner</c> behind a proxy on the pool host's loopback,
    /// and this project does not reference it. A member that redeemed its nonce
    /// is a runner like any other; one created and never redeemed appears
    /// nowhere here — which is exactly the "counted as warm forever" failure
    /// the contract warns about, so nothing in these rows may be read as a
    /// count of what exists.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MemberRow> Members(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var mine = Runner(state);
        var fleet = state.Runners?.Runners ?? [];

        return
        [
            .. Advertised(state).SelectMany(charted => fleet
                .Where(r => r.Labels.Any(
                    l => string.Equals(
                        l.Name, AdvertisedAs + charted.Name, StringComparison.Ordinal)))
                .OrderBy(r => r.Label, StringComparer.Ordinal)
                .Select(r => new MemberRow(
                    Here: mine is not null
                       && string.Equals(r.RunnerId, mine.RunnerId, StringComparison.Ordinal)
                        ? Mark
                        : "",
                    Environment: charted.Name,
                    Member: r.Label,

                    // BOTH FACTS OR NEITHER, which is Rows.Runners' rule one
                    // pane over: a runner can be parked AND busy, and a column
                    // printing only the state shows a machine somebody
                    // deliberately withheld as idle.
                    State: r.ParkedAt is null ? r.State : $"{r.State} · parked",
                    Work: r.CurrentFlightNumber ?? "",
                    Heard: r.LastHeartbeatAt is { } at ? at.ToString("u") : "never"))),
        ];
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
            // column is named for the half that makes it a declaration rather
            // than a count.
            Wants: inventory is null ? "" : $"{inventory.Warm} of {inventory.Size}",
            Attested: attested?.Outcome ?? "",

            // ABSOLUTE, like the fleet's last-heard beside it. Nothing on the
            // model carries a clock, so an age would have to be computed in the
            // view - and two panes would then disagree about what "now" is.
            Measured: attested is { } status ? status.MeasuredAt.ToString("u") : "");
    }

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
