using Gg.Contracts;

namespace Gg.Console;

/// <summary>The gates the queue lists as needing this person.</summary>
/// <remarks>
/// <b>A role is anybody entitled's; a person is theirs alone</b> (owner, 2026-10-09). An approver
/// spelled <c>provider:subject</c> names one person and the control plane refuses anybody else's
/// answer, so listing it as needing somebody else is work they cannot do - GG-1044, about Phil's
/// machine, in Kevin's queue. The gates tab still lists every gate: seeing is not the question,
/// needing is.
/// </remarks>
public static class QueueGates
{
    public static GateList? Answerable(GateList? gates, string subject) =>
        gates is null
            ? null
            : gates with
            {
                Gates = [.. gates.Gates.Where(gate =>
                    !PersonSpelling.IsPerson(gate.Approver)
                    || (subject.Length > 0 && string.Equals(gate.Approver, subject, StringComparison.Ordinal)))],
            };
}
