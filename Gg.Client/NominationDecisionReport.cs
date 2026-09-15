using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What a nomination came to once somebody answered it.
/// </summary>
/// <remarks>
/// <para>
/// <b>OBSERVED, NEVER COMPUTED, and here that is load-bearing rather than
/// ceremonial.</b> Answering <c>open</c> records a decision; the admission pass
/// that follows applies the menu, the selection, the chain and composition, and
/// it may refuse. A report that said "opened" because gg posted <c>opened</c>
/// would tell a person a flight exists on the day the row reads
/// <c>refused</c> and carries the rule's own sentence — which is the one
/// sentence they need.
/// </para>
/// <para>
/// <b>The row may be absent, and that is a state rather than a gap.</b> Null
/// means the board had not settled by the time gg stopped waiting: the decision
/// is recorded and the answer is not visible yet. Rendering it as an empty row
/// would make "not yet" and "nothing happened" the same value, which is the
/// distinction <see cref="SubmitAndObserve"/> exists to keep.
/// </para>
/// <para>
/// <b><see cref="DecisionReport"/>'s shape, one noun earlier</b>, and for its
/// reason: the observation says how long a person waited and against what
/// bound, so a reader can tell a slow board from a silent one.
/// </para>
/// </remarks>
public sealed record NominationDecisionReport
{
    /// <summary>The row as the board now has it, or null if it is not settled.</summary>
    public NominationSummary? Nomination { get; init; }

    /// <summary>What was observed on the read surface, and how long it took.</summary>
    public required Observation Observation { get; init; }
}
