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
