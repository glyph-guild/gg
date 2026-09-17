using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What a runner's state is worth saying with colour.
/// </summary>
/// <remarks>
/// <see cref="FlightTint"/>'s shape one tab over, and for its reason: a tint is
/// answerable without a terminal, and which shade it becomes belongs to
/// <c>Views/LookStyles</c>.
/// </remarks>
public enum RunnerTint
{
    /// <summary>Alive and able to take work. The ordinary foreground.</summary>
    None,

    /// <summary>The heartbeat is stale, so it can take nothing.</summary>
    Offline,

    /// <summary>Somebody withheld it deliberately.</summary>
    Parked,

    /// <summary>
    /// It maintains a pool: alive, and taking no work. Not <see cref="Offline"/>,
    /// which says the heartbeat went stale.
    /// </summary>
    Maintaining,
}

/// <summary>
/// Which runners can take work, and what their not doing so is worth saying in
/// colour.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same rule as the flights tab: the ordinary case is plain.</b> There,
/// colour means "this is over"; here it means "this one will not take work".
/// <c>busy</c> and <c>idle</c> are both alive and both fine - one is holding a
/// lease and the other is waiting for one - so neither is tinted and neither
/// recedes. What a person scans a fleet for is the machine that is not going to
/// answer.
/// </para>
/// <para>
/// <b>Parked outranks offline, and that is a choice.</b> A runner can be both -
/// a machine somebody withheld and then shut - and the cell says
/// <c>offline · parked</c>. Offline is the ordinary condition of a laptop;
/// parked is a decision somebody made and may have forgotten, so it is the half
/// worth colouring.
/// </para>
/// <para>
/// <b>Offline is grey rather than red.</b> Red is for something that wanted to
/// work and did not - and most offline runners are laptops that are closed.
/// Colouring every shut machine as a fault is the cry of wolf that teaches
/// people to stop reading colour, which is the same argument that keeps
/// <c>grounded</c> off red one tab over.
/// </para>
/// </remarks>
public static class RunnerLook
{
    /// <summary>What the runners tab writes beside a state it withheld.</summary>
    /// <remarks>
    /// <b>The cell is not a bare state.</b> <c>Rows.Runners</c> writes both
    /// facts or neither - "a runner can be parked AND busy, draining, which is
    /// the reason to park anything" - so what arrives here is
    /// <c>idle · parked</c> as often as <c>idle</c>.
    /// </remarks>
    public const string Parked = "parked";

    /// <summary>What this cell is worth saying in colour.</summary>
    public static RunnerTint Tint(string? state)
    {
        if (state is not { Length: > 0 })
        {
            return RunnerTint.None;
        }

        if (state.Contains(Parked, StringComparison.Ordinal))
        {
            return RunnerTint.Parked;
        }

        // THE WORD IN FRONT OF THE MARK. Everything after it is what the row
        // added, and the state itself is what the control plane derived.
        var derived = state.Split('·', 2)[0].Trim();

        return derived switch
        {
            RunnerStates.Offline => RunnerTint.Offline,
            RunnerStates.Maintaining => RunnerTint.Maintaining,
            _ => RunnerTint.None,
        };
    }

    /// <summary>
    /// Whether this runner will take no work, and so should recede.
    /// </summary>
    /// <remarks>
    /// Read off the tint, for <c>FlightLook.IsOver</c>'s reason: two lists is
    /// one that drifts, and the drift shows up as a row that is coloured and
    /// not dimmed.
    /// </remarks>
    public static bool IsAside(string? state) => Tint(state) is not RunnerTint.None;
}
