using Gg.Local;

namespace Gg.Runner.Execution;

/// <summary>
/// Removes live views a previous life left behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same rule <c>WorkingTreeRoot.SweepOrphans</c> follows, and for the
/// same reason:</b> a runner that is starting holds no lease, so every live view
/// under the root belongs to a process that is gone. That makes the rule "all of
/// them" rather than a set difference against something we would have had to
/// persist — and a rule with no state behind it cannot be wrong about which
/// views are live.
/// </para>
/// <para>
/// <b>It cannot touch a transcript, and that is why the directories are
/// siblings.</b> Live views are deletable and transcripts are not: a transcript
/// is the only copy of what an agent did. This sweeps
/// <see cref="LocalPaths.LiveViews"/> and nothing else, and
/// <c>LiveSweepTests</c> asserts a transcript beside it survives — because the
/// version of this that took the evidence with it is the one worth being unable
/// to write.
/// </para>
/// </remarks>
public sealed class LiveViewSweep(string? root = null)
{
    private readonly string _root = root ?? LocalPaths.LiveViews();

    /// <summary>Removes every live view under the root, and says how many.</summary>
    /// <remarks>
    /// Only <c>.ndjson</c>, so a directory somebody put here by hand is left
    /// alone rather than deleted on the strength of being in the way.
    /// </remarks>
    public int SweepOrphans()
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        var swept = 0;
        foreach (var view in Directory.EnumerateFiles(_root, "*.ndjson"))
        {
            File.Delete(view);
            swept++;
        }

        return swept;
    }
}
