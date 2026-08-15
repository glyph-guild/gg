using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// What to say when a loop reached for something its envelope did not declare.
/// </summary>
/// <remarks>
/// <para>
/// <b>Naming what was needed and where to add it is the whole obligation.</b> A
/// refusal that only says no teaches people to want the middle option - a
/// rejection reason that can widen an envelope - and that was refused on
/// governance grounds: an envelope by accretion, made of rejection comments, is
/// unreviewable configuration arriving one sentence at a time. The way out is a
/// refusal somebody can act on through the envelope, which means it has to say
/// which move and which loop.
/// </para>
/// <para>
/// <b>It reads the corrected list.</b> <c>refusedMoves</c> used to be every
/// undeclared tool the loop reached for, whether or not the reach worked, so a
/// diagnosis built on it would have told people to declare <c>run-tests</c>
/// because <c>Bash</c> ran fine. It now means reached for and refused every time,
/// which is the only version this sentence can be built on.
/// </para>
/// </remarks>
public static class MoveRefusal
{
    /// <summary>
    /// The sentence, or null when nothing was refused.
    /// </summary>
    /// <param name="refused">Tools refused every time they were tried.</param>
    /// <param name="loopId">Which loop, so the envelope path is exact.</param>
    public static string? Diagnose(IReadOnlyList<string> refused, string loopId)
    {
        ArgumentNullException.ThrowIfNull(refused);

        // Only the tools a declared move could actually have granted. A tool no
        // move maps to cannot be fixed by editing an envelope, and telling
        // somebody to add a move that does not exist is worse than saying nothing.
        var fixable = refused
            .Select(tool => (Tool: tool, Move: MoveFor(tool)))
            .Where(x => x.Move is not null)
            .OrderBy(x => x.Move, StringComparer.Ordinal)
            .ToList();

        if (fixable.Count == 0)
        {
            return null;
        }

        var moves = string.Join(", ", fixable.Select(x => $"'{x.Move}'"));
        var tools = string.Join(", ", fixable.Select(x => x.Tool));

        return $"This loop reached for {tools} and was refused every time, because the envelope "
             + $"does not declare {moves}. Add {moves} to loops.{loopId}.moves if this flight is "
             + "meant to be able to. Nothing here can grant it: what a loop may do is the "
             + "envelope's to say, and a runner that widened it would make the envelope advisory.";
    }

    /// <summary>The declared move that would have granted a tool, when one would.</summary>
    private static string? MoveFor(string tool) =>
        LoopMoves.All.FirstOrDefault(move =>
            string.Equals(ClaudeCodeExecutor.ToolFor(move), tool, StringComparison.Ordinal));
}
