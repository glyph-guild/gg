namespace Gg.Contracts;

/// <summary>
/// Which infrastructure a strategy manages. Closed at one, and the closure is
/// the design: ADR-0015's table is eight infrastructures, and only one has to
/// exist for a pool to be managed at all.
/// </summary>
/// <remarks>
/// A second member here (vm-fleet, kubernetes, devcontainer, microvm) is a
/// design event that arrives as a deliberate contract change carrying its own
/// scope story — § 12's credential-scoping callout binds hardest per row, and
/// each row must say what enforces its scope before it exists as a value.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class StrategyKinds
{
    /// <summary>
    /// Containers on one host the resident runner lives on: local actions, no
    /// cloud account, scope enforced by a socket proxy outside gg.
    /// </summary>
    public const string DockerHost = "docker-host";

    public static IReadOnlyList<string> All { get; } = [DockerHost];
}

/// <summary>
/// How decided work reaches the pool. Closed at one; a strategy naming none
/// is refused at authoring, because a powered-off pool cannot pull.
/// </summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class PullPoints
{
    /// <summary>
    /// A runner resident on the managed host polls for decided actions and
    /// attests their outcomes.
    /// </summary>
    public const string ResidentRunner = "resident-runner";

    public static IReadOnlyList<string> All { get; } = [ResidentRunner];
}

/// <summary>The pool a strategy manages: a name and how many.</summary>
/// <remarks>
/// Containers are cattle: <c>&lt;pool&gt;-1..N</c>. The inventory is the
/// scope § 12 binds the runner's credential to — extending it is a widening
/// and rides the gate; nothing here enforces it, because the enforcement is
/// the provider's (the socket proxy), never ours.
/// </remarks>
[PinnedId("3b7cfb45-67b9-4180-83ac-fd81100f3e50")]
public sealed record StrategyInventory
{
    /// <summary>The pool's name; container names derive from it.</summary>
    public required string Pool { get; init; }

    /// <summary>How many environments the pool may hold, total.</summary>
    public required int Size { get; init; }
}

/// <summary>
/// The bounds a tenant declared, inside which Good Grief manages. A bound
/// binds by waiting — a flight at one waits naming the bound and its
/// clearing, never a capability gap.
/// </summary>
[PinnedId("d18b0ae9-c83d-43d5-90a6-bcc306394abd")]
public sealed record StrategyBounds
{
    /// <summary>
    /// How many environments may be warm at once. At the bound a flight
    /// waits with the <c>capacity</c> clearing: a peer's release clears it.
    /// </summary>
    public required int PoolMax { get; init; }

    /// <summary>
    /// When the pool may be warmed at all, as <c>HH:MM-HH:MMZ</c> — or null,
    /// which means always. Outside the hours a flight waits with the
    /// <c>schedule</c> clearing and the opening time as its ETA.
    /// </summary>
    /// <remarks>
    /// No spend ceiling, deliberately: docker-host meters no spend, and a
    /// bound nothing measures is a promise nobody has to keep. It arrives
    /// with the first metered strategy kind, and the <c>authority</c>
    /// clearing arrives with it.
    /// </remarks>
    public string? ActiveHours { get; init; }
}

/// <summary>
/// A strategy: the document under which Good Grief manages a pool of
/// environments on a tenant's host.
/// </summary>
/// <remarks>
/// <para>
/// <b>A named Airspace document with a shape of its own.</b> It applies
/// through the per-name stream to a name whose topology role is
/// <see cref="Roles.Strategy"/>, versions through the same counter as every
/// other document, and never composes — exactly one strategy governs one
/// name, so it owes the merge-operator table nothing and both composition
/// and the flight door refuse it by role.
/// </para>
/// <para>
/// <b>No host, no socket, no credential — by shape.</b> The resident
/// runner's endpoint lives in its own environment
/// (<c>GG_POOL_ENDPOINT</c>) and nowhere on the wire, because which host a
/// customer's credential goes to must never be a policy edit here. The
/// repository registration's rule, one document over; held by
/// <c>StrategyContainmentTests</c> over the member types.
/// </para>
/// </remarks>
[PinnedId("84f42bc4-9fdb-46a7-aa1b-7ba9f574e19a")]
public sealed record EnvironmentStrategy
{
    /// <summary>One of <see cref="StrategyKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The charted environment name this pool furnishes. A warm environment
    /// is a container whose runner advertises this label — the only warmth
    /// the matcher can see.
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>The pool: name and size.</summary>
    public required StrategyInventory Inventory { get; init; }

    /// <summary>One of <see cref="PullPoints"/>.</summary>
    public required string PullPoint { get; init; }

    /// <summary>
    /// What reset resets to: an image reference pinned by digest, because a
    /// reset that converges on whatever a tag means today converges on
    /// nothing.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>The declared bounds. Managing happens inside them.</summary>
    public required StrategyBounds Bounds { get; init; }

    /// <summary>
    /// The schema's own rule, shared so gg and the control plane cannot
    /// disagree about what a valid strategy is. Null means valid; anything
    /// else is the refusal, Article XI-shaped.
    /// </summary>
    public static string? Validate(EnvironmentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        if (!StrategyKinds.All.Contains(strategy.Kind, StringComparer.Ordinal))
        {
            return $"'{strategy.Kind}' is not a strategy kind this version knows. Expected "
                 + $"one of: {string.Join(", ", StrategyKinds.All)}.";
        }

        if (string.IsNullOrWhiteSpace(strategy.Environment))
        {
            return "A strategy must name the charted environment its pool furnishes - "
                 + "environment is blank.";
        }

        if (string.IsNullOrWhiteSpace(strategy.Inventory.Pool))
        {
            return "A strategy must name its pool - inventory.pool is blank.";
        }

        if (strategy.Inventory.Size < 1)
        {
            return $"inventory.size is {strategy.Inventory.Size}; a pool holds at least one "
                 + "environment or there is nothing to manage.";
        }

        if (string.IsNullOrWhiteSpace(strategy.PullPoint))
        {
            return "This strategy names no pull point, and a powered-off pool cannot pull. "
                 + "Declare pull-point: " + PullPoints.ResidentRunner + ".";
        }

        if (!PullPoints.All.Contains(strategy.PullPoint, StringComparer.Ordinal))
        {
            return $"'{strategy.PullPoint}' is not a pull point this version knows. Expected "
                 + $"one of: {string.Join(", ", PullPoints.All)}.";
        }

        if (string.IsNullOrWhiteSpace(strategy.Image) || !strategy.Image.Contains("@sha256:", StringComparison.Ordinal))
        {
            return "image must be pinned by digest (name@sha256:...). What reset resets TO "
                 + "must be a fixed point, or the reset converges on whatever the tag means "
                 + "today.";
        }

        if (strategy.Bounds.PoolMax < 1)
        {
            return $"bounds.pool-max is {strategy.Bounds.PoolMax}; a bound below one declines "
                 + "everything, which is a strategy for a pool that should not exist.";
        }

        if (strategy.Bounds.PoolMax > strategy.Inventory.Size)
        {
            return $"bounds.pool-max ({strategy.Bounds.PoolMax}) exceeds inventory.size "
                 + $"({strategy.Inventory.Size}) - a bound above the inventory is a promise "
                 + "the inventory cannot keep.";
        }

        if (strategy.Bounds.ActiveHours is { } hours && ParseActiveHours(hours) is null)
        {
            return $"bounds.active-hours '{hours}' is not readable. Expected HH:MM-HH:MMZ, "
                 + "e.g. 08:00-20:00Z - a schedule bound nobody can parse binds nothing.";
        }

        return null;
    }

    /// <summary>
    /// Reads <c>HH:MM-HH:MMZ</c> into UTC times, or null if it is not that.
    /// One parser, used by the validation above and by whatever evaluates the
    /// bound, so "valid" and "evaluable" cannot drift apart.
    /// </summary>
    public static (TimeOnly OpensUtc, TimeOnly ClosesUtc)? ParseActiveHours(string hours)
    {
        ArgumentNullException.ThrowIfNull(hours);

        if (!hours.EndsWith('Z'))
        {
            return null;
        }

        var span = hours[..^1].Split('-');
        if (span.Length != 2
            || !TimeOnly.TryParseExact(span[0], "HH:mm", out var opens)
            || !TimeOnly.TryParseExact(span[1], "HH:mm", out var closes))
        {
            return null;
        }

        return (opens, closes);
    }
}

/// <summary>One applied strategy, as the read side serves it.</summary>
[PinnedId("9e035070-1a69-41ac-8141-5dff0d3e7b6e")]
public sealed record EnvironmentStrategyState
{
    /// <summary>The topology name the strategy was applied to.</summary>
    public required string Name { get; init; }

    /// <summary>The per-name version in force, e.g. v2.</summary>
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public required EnvironmentStrategy Strategy { get; init; }
}

/// <summary>Every strategy in force for the tenant.</summary>
/// <remarks>
/// An envelope rather than a bare array, for the same reason
/// <see cref="EnvironmentChart"/> is: a bare array has nowhere to put the
/// paging this will grow.
/// </remarks>
[PinnedId("841c873b-c35d-4afa-a230-8394a9d71096")]
public sealed record StrategyList
{
    public required IReadOnlyList<EnvironmentStrategyState> Strategies { get; init; }
}
