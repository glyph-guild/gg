using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// A decision the control plane answered no to.
/// </summary>
/// <remarks>
/// <para>
/// <b>An answer, not a crash.</b> Every refusal on this path used to leave
/// <c>gg</c> through an unhandled <see cref="InvalidOperationException"/> - a
/// stack trace on stderr and exit 134, which is SIGABRT and is indistinguishable
/// from the process dying. A script cannot tell "you were told no" from "gg
/// broke", which is the exact distinction the wait makes load-bearing.
/// </para>
/// <para>
/// <b>It derives from <see cref="InvalidOperationException"/> deliberately.</b>
/// That is what the 409 already threw and what the verb's own refusals throw, so
/// nothing that catches the old type stops catching. The type is narrower, not
/// different.
/// </para>
/// </remarks>
public class DecisionRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// What <c>gg decide</c> came away with: what was observed, and - for now - what
/// the synchronous write still answered.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wrapper exists so that step 2 changes nothing here.</b> Today the
/// control plane still writes the decision inline and still returns it, so
/// <see cref="Decision"/> is populated and the observation beside it is a
/// second, independent read of the same fact. When the write becomes a command,
/// <see cref="Decision"/> becomes null and <see cref="Observation"/> is the whole
/// answer - the shape does not move, only whether one field is filled.
/// </para>
/// <para>
/// <b>Emitting <c>DecisionRecorded</c> at the top level would not survive
/// that.</b> The break is taken now, while both halves exist and can be compared,
/// rather than in the step where the thing being compared against disappears.
/// </para>
/// </remarks>
public sealed record DecisionReport
{
    /// <summary>What was observed on the read surface, and how long it took.</summary>
    public required Observation Observation { get; init; }

    /// <summary>
    /// What the synchronous write answered, while there still is one.
    /// </summary>
    /// <remarks>
    /// Null is a real value here rather than a gap: it means the control plane no
    /// longer answers inline, which is the whole direction of ADR-0012.
    /// </remarks>
    public DecisionRecorded? Decision { get; init; }
}
