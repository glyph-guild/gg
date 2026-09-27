using Gg.Contracts;

namespace Gg.Client;

/// <summary>What a person typed where a runner id goes, and which machine it is.</summary>
/// <param name="RunnerId">The machine, or null when this could not be settled.</param>
/// <param name="Said">
/// Why it could not be, and what to do instead. Empty when it could.
/// </param>
public sealed record RunnerResolved(string? RunnerId, string Said);

/// <summary>
/// Turning the name of a machine into the machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because nobody knows a uuid.</b> Every verb that acts on a machine took
/// one, and the only way to get one was to list the fleet and copy it - so the
/// ordinary way to run <c>gg agent login</c> was two commands, the first of
/// which existed to feed the second.
/// </para>
/// <para>
/// <b>Pure, and the read is the caller's.</b> This decides nothing about the
/// network: it is handed the fleet and answers about it, which is what lets
/// every refusal here be a test rather than a walk.
/// </para>
/// </remarks>
public static class RunnerNamed
{
    /// <summary>
    /// Whether this is already an id, and so needs nothing looked up.
    /// </summary>
    /// <remarks>
    /// <b>The shape, not a lookup.</b> A script that holds an id should not
    /// start paying for a fleet listing on every verb - and worse, should not
    /// start failing when that listing does, for a value that never needed it.
    /// </remarks>
    public static bool LooksLikeAnId(string given) =>
        Guid.TryParse(given, out _);

    /// <summary>
    /// Which machine this names, or why that cannot be settled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exact, and case-insensitive.</b> A machine name is typed from memory
    /// and a refusal over a capital is a refusal about nothing. No prefixes,
    /// though: <c>retire</c>, <c>release</c> and <c>unclaim</c> take this, so
    /// <c>vm</c> must never reach for the only machine starting with it.
    /// </para>
    /// <para>
    /// <b>Two of a name is a refusal that names both.</b> Nothing makes a label
    /// unique - it is what a machine calls itself - and choosing for somebody
    /// here retires the wrong one half the time. The ids are in the sentence
    /// because they are the only thing that tells the two apart.
    /// </para>
    /// </remarks>
    public static RunnerResolved Resolve(string given, IReadOnlyList<RunnerSummary> fleet)
    {
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(fleet);

        if (LooksLikeAnId(given))
        {
            return new RunnerResolved(given, "");
        }

        var named = fleet
            .Where(runner => string.Equals(runner.Label, given, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (named.Count == 1)
        {
            return new RunnerResolved(named[0].RunnerId, "");
        }

        if (named.Count > 1)
        {
            return new RunnerResolved(
                null,
                $"'{given}' is the name of {named.Count} machines, so it does not say which: "
              + string.Join(", ", named.Select(runner => runner.RunnerId))
              + ". Name one of them by its id.");
        }

        // NO FLEET IS A DIFFERENT THING TO GO AND FIX than a name nothing
        // answers to, and a list of nothing reads as the second.
        if (fleet.Count == 0)
        {
            return new RunnerResolved(
                null,
                $"'{given}' does not name a machine, because this tenant has no machines. "
              + "Enroll one with `gg fleet token` and run `gg runner up` on it.");
        }

        return new RunnerResolved(
            null,
            $"'{given}' does not name a machine in this tenant. It has: "
          + string.Join(", ", fleet.Select(runner => runner.Label).Order(StringComparer.Ordinal))
          + ".");
    }
}
