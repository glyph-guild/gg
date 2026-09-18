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

    /// <summary>
    /// The control plane performs it itself, where it can reach the system of
    /// record. Legal for a watch and NOT for a strategy — a pool the control
    /// plane performs is a pool nothing warms, so each document validates the
    /// subset it can actually be performed by.
    /// </summary>
    public const string ControlPlane = "control-plane";

    /// <summary>
    /// The forge's own scheduler, as the always-on party. Legal for a watch and
    /// not for a strategy, for the reason above.
    /// </summary>
    public const string ForgeScheduler = "forge-scheduler";

    public static IReadOnlyList<string> All { get; } =
        [ResidentRunner, ControlPlane, ForgeScheduler];
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

    /// <summary>
    /// How many members should be warm and idle <b>before anything asks</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first number in a strategy that is not a ceiling. <see cref="Size"/>
    /// is how many the pool MAY hold and <see cref="StrategyBounds.PoolMax"/>
    /// is how many may be warm at once; this is how many it SHOULD hold, which
    /// is the only one of the three a decider can be short of.
    /// </para>
    /// <para>
    /// <b>Zero is a real answer, and it is the default.</b> It means warm only
    /// behind demand — what every strategy did before this member existed, so a
    /// document written then reads back meaning what it meant, and warming
    /// ahead of demand is opt-in by construction rather than by a migration.
    /// </para>
    /// </remarks>
    public int Warm { get; init; }
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
/// Where a strategy's image comes from: a directory in a registered
/// repository, at a ref.
/// </summary>
/// <remarks>
/// <para>
/// <b>By reference, never by content.</b> The shape a watch's skill already
/// has - a repository by its registry name, a path and a ref - and nothing a
/// Dockerfile's text could arrive in. The control plane resolves the name and
/// never reads the recipe; the runner that builds it fetches it with the
/// machine's own credential.
/// </para>
/// <para>
/// <b>Never read when a member is made.</b> <see cref="EnvironmentStrategy.Image"/>
/// is still the pin. This says where the NEXT pin comes from, so declaring or
/// changing it moves nothing until a build has run and its digest has come
/// back through the door.
/// </para>
/// </remarks>
[PinnedId("c783624d-3877-4a78-9309-79ee8b7f3ec8")]
public sealed record StrategyBuild
{
    /// <summary>The repository, by its name in the tenant's registry. Never a url.</summary>
    public required string Repository { get; init; }

    /// <summary>
    /// The recipe directory inside it, relative to the repository root. This
    /// is the whole build context: nothing outside it reaches the build.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>What to build from. The runner resolves it and reports the commit.</summary>
    public required string Ref { get; init; }

    /// <summary>
    /// The Dockerfile's name inside <see cref="Path"/>, or null for
    /// <c>Dockerfile</c>.
    /// </summary>
    public string? Dockerfile { get; init; }
}

/// <summary>
/// What the image in force was built from: the recipe, at the commit a build
/// resolved it to.
/// </summary>
/// <remarks>
/// <b>A record, written by a build.</b> It means something only beside the
/// <see cref="StrategyBuild"/> it names, which is why a strategy carrying one
/// without the other, or naming a different directory, is refused - and the
/// control plane believes it only when a build it decided attested that
/// digest and that commit, so a line written by hand cannot pass for one.
/// </remarks>
[PinnedId("42cc92d5-893d-47a9-95b7-12418303e1d9")]
public sealed record StrategyProvenance
{
    /// <summary>The recipe's repository, as <see cref="StrategyBuild.Repository"/> names it.</summary>
    public required string Repository { get; init; }

    /// <summary>The recipe's directory, as <see cref="StrategyBuild.Path"/> names it.</summary>
    public required string Path { get; init; }

    /// <summary>The commit the ref resolved to when the image was built. Never a ref.</summary>
    public required string Commit { get; init; }
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
    /// Where the next image comes from, or null for a strategy whose image is
    /// pinned by hand - which is every strategy written before slice forty-one.
    /// </summary>
    public StrategyBuild? Build { get; init; }

    /// <summary>
    /// What <see cref="Image"/> was built from, written by a build. Null when
    /// the image was pinned by hand or no build has run.
    /// </summary>
    public StrategyProvenance? BuiltFrom { get; init; }

    /// <summary>
    /// The schema's own rule, shared so gg and the control plane cannot
    /// disagree about what a valid strategy is. Null means valid; anything
    /// else is the refusal, Article XI-shaped.
    /// </summary>
    public static string? Validate(EnvironmentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        // THE VOCABULARY HOLDS THREE AND A STRATEGY TAKES ONE, which is the
        // shape `DestinationKinds` already has where a repository's bound
        // accepts only `flight`. A pull point widened for a watch must not
        // silently become legal for a pool: the control plane cannot warm a
        // container, and a forge's scheduler has never heard of one.
        if (!string.Equals(
                strategy.PullPoint, PullPoints.ResidentRunner, StringComparison.Ordinal))
        {
            return $"This strategy is performed by '{strategy.PullPoint}', and a pool is "
                 + $"warmed by a runner on the managed host - '{PullPoints.ResidentRunner}'. "
                 + "A powered-off pool cannot pull, and neither can one nothing is resident "
                 + "on.";
        }

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

        if (strategy.Inventory.Warm < 0)
        {
            return $"inventory.warm is {strategy.Inventory.Warm}; below zero is not a smaller "
                 + "kind of zero, it is a number nothing can act on. Zero means warm only "
                 + "behind demand, which is what a strategy naming no target says.";
        }

        // ONE REFUSAL, NOT TWO. A target above inventory.size looks like it
        // deserves its own arm and cannot be reached: pool-max > size is
        // already refused above, so anything above the inventory is above the
        // bound first. An unreachable refusal is a control that never runs.
        if (strategy.Inventory.Warm > strategy.Bounds.PoolMax)
        {
            return $"inventory.warm ({strategy.Inventory.Warm}) exceeds bounds.pool-max "
                 + $"({strategy.Bounds.PoolMax}) - a target the bound can never reach leaves "
                 + "the pool permanently short of a promise it was never allowed to keep.";
        }

        if (strategy.Bounds.ActiveHours is { } hours && ParseActiveHours(hours) is null)
        {
            return $"bounds.active-hours '{hours}' is not readable. Expected HH:MM-HH:MMZ, "
                 + "e.g. 08:00-20:00Z - a schedule bound nobody can parse binds nothing.";
        }

        if (strategy.Build is { } recipe && RecipeRefusal(recipe) is { } badRecipe)
        {
            return badRecipe;
        }

        if (strategy.BuiltFrom is { } provenance)
        {
            // A RECORD OF A BUILD NOTHING ON THIS DOCUMENT COULD HAVE ASKED FOR.
            // Provenance means something only beside the recipe it names; alone,
            // or naming another directory, it is a claim the direction rule would
            // read as "built from the reviewed recipe" when it was not.
            if (strategy.Build is not { } declared)
            {
                return "built-from says what the image was built from, and this strategy names "
                     + "no recipe for it to have been built from. Declare build:, or remove "
                     + "built-from.";
            }

            if (!string.Equals(provenance.Repository, declared.Repository, StringComparison.Ordinal)
                || !string.Equals(provenance.Path, declared.Path, StringComparison.Ordinal))
            {
                return $"built-from names {provenance.Repository}:{provenance.Path}, and this "
                     + $"strategy's recipe is {declared.Repository}:{declared.Path}. An image "
                     + "built from another directory is not this recipe's.";
            }

            if (!GitObjectIds.IsOne(provenance.Commit))
            {
                return $"built-from.commit '{provenance.Commit}' is not a commit. A commit is forty "
                     + "or sixty-four hex digits, and a ref here would be the one answer that "
                     + "looks like an answer and is not - refs move.";
            }
        }

        return null;
    }

    /// <summary>What is wrong with a recipe, or null when nothing is.</summary>
    /// <remarks>
    /// <b>The build context is the directory, and nothing else</b>, so a path
    /// that could climb out of the repository or name a place on whatever
    /// machine reads it is refused where the author can still act - the
    /// narrowings directory's rule, for a directory that is built rather than
    /// read.
    /// </remarks>
    private static string? RecipeRefusal(StrategyBuild recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.Repository))
        {
            return "build.repository is blank. A recipe names the repository it is in by its "
                 + "name in this tenant's registry.";
        }

        if (string.IsNullOrWhiteSpace(recipe.Path))
        {
            return "build.path is blank. A recipe is a directory, and the directory is the "
                 + "whole build context.";
        }

        if (PathRefusal(recipe.Path, "build.path") is { } badPath)
        {
            return badPath;
        }

        if (string.IsNullOrWhiteSpace(recipe.Ref))
        {
            return "build.ref is blank. A recipe is built at a ref, which the runner resolves "
                 + "and reports as the commit it built.";
        }

        if (recipe.Dockerfile is { } dockerfile
            && (string.IsNullOrWhiteSpace(dockerfile)
                || PathRefusal(dockerfile, "build.dockerfile") is not null))
        {
            return $"build.dockerfile '{dockerfile}' is not a file inside the recipe's "
                 + "directory. Name it relative to build.path, with no '..' and no leading '/'.";
        }

        return null;
    }

    /// <summary>A refusal for a path that could leave where it is declared, or null.</summary>
    private static string? PathRefusal(string path, string key)
    {
        if (path.Contains('\\', StringComparison.Ordinal))
        {
            return $"{key} '{path}' contains a backslash. A forge serves forward slashes, so a "
                 + "backslash would mean one thing where it is declared and another where it "
                 + "is fetched.";
        }

        if (path.StartsWith('/'))
        {
            return $"{key} '{path}' is absolute, which names a place on whatever machine reads "
                 + "it. Declare it relative to the repository root.";
        }

        if (path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            return $"{key} '{path}' contains '..'. The build context is this directory and "
                 + "nothing else, and a path that can climb out of it has no containment at "
                 + "all.";
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
