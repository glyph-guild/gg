using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The digest's two accounts of a blocked run, both of which were wrong, in
/// opposite directions, on the same flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fixture is the run that found it.</b> A real agent under a real lease
/// declaring <c>moves: [read, edit]</c>, asked for work needing a file it could
/// not create. Its stream is committed here; what the tree looked like afterwards
/// is committed in the comments, because the tree is the thing the digest is
/// supposed to be an account of.
/// </para>
/// <para>
/// What the stream says happened:
/// </para>
/// <code>
///   tools called:                    Bash, Read, Write
///   calls that errored:              Write
///   tools called and never errored:  Bash, Read
///   git status --porcelain:          (empty - the tree is untouched)
/// </code>
/// <para>
/// What the digest said about it:
/// </para>
/// <code>
///   refusedMoves: ["Bash", "Write"]                       Bash was USED
///   filesEdited:  ["migrations/0002_add_order_discount.sql"]   it does not exist
///   stopReason:   "completed"
/// </code>
/// <para>
/// <b>So a person reading the fact would be told the loop completed and edited a
/// migration.</b> It created nothing, and the one tool it was told off for is one
/// that worked. Both halves are corrected here; rendering any of it to a decider
/// is the step after, and rendering it before this was the thing not to do.
/// </para>
/// </remarks>
public class DigestTruthTests
{
    /// <summary>The tree the fixture's run happened in, so paths come out relative.</summary>
    private const string TreeRoot =
        "/private/var/folders/fl/bwf17f0s1wxfcpkn189580mr0000gn/T/gg-tree-tests/"
      + "b422cee1ec30425ca3acdc925d3fc5eb/trees/flight-1/11ac24ec5e95ee44";

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(
            (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName,
            "Gg.Runner.Tests", "Fixtures", name));
    }

    /// <summary>The digest for the blocked run, as the runner would produce it.</summary>
    private static LoopDigest Blocked() =>
        TranscriptDigest.Extract(
            Fixture("agent-refused-a-write.ndjson"), "implement", [TreeRoot],
            LoopOutcomes.Completed, declared: [LoopMoves.Read, LoopMoves.Edit]);

    // ---- what was refused ----

    [Test]
    public async Task A_tool_that_worked_is_not_reported_as_refused()
    {
        // Bash is not in `moves: [read, edit]`, the agent reached for it, and it
        // RAN - the allow-list does not bind Bash. The old derivation was a set
        // difference against the declared moves and never looked at whether the
        // call came back, so every undeclared tool was reported as refused
        // whether or not it worked.
        await Assert.That(Blocked().RefusedMoves).DoesNotContain("Bash")
            .Because("it was called and it never errored, which is the opposite of refused.");
    }

    [Test]
    public async Task A_tool_that_never_once_succeeded_is()
    {
        // The liveness twin. Without it the assertion above is satisfied by a
        // derivation that reports nothing as refused, which is the failure one
        // step further in the same direction.
        await Assert.That(Blocked().RefusedMoves).Contains("Write")
            .Because("every Write in this run came back an error, and the work needed one.");
    }

    // ---- what was edited ----

    [Test]
    public async Task A_file_whose_every_write_was_refused_is_not_reported_as_edited()
    {
        // The tree after this run is EMPTY - git status --porcelain says nothing
        // changed. The digest named a migration because a tool_use carried the
        // path, and the refusal arrives on a later event that nothing joined
        // back to it.
        await Assert.That(Blocked().FilesEdited)
            .DoesNotContain("migrations/0002_add_order_discount.sql");
        await Assert.That(Blocked().FilesEdited).IsEmpty()
            .Because("nothing was edited, and the honest account of a loop that changed "
                   + "nothing is an empty list rather than a claim.");
    }

    [Test]
    public async Task A_file_that_really_was_edited_still_is()
    {
        // The liveness twin, from the fixture where the edits succeeded. Without
        // it, "drop the refused ones" is indistinguishable from "drop them all".
        var digest = TranscriptDigest.Extract(
            Fixture("agent-considered.ndjson"), "implement", ["/work/tree"],
            LoopOutcomes.Completed, declared: [LoopMoves.Read, LoopMoves.Edit]);

        await Assert.That(digest.FilesEdited).IsNotEmpty();
    }

    [Test]
    public async Task What_it_read_is_unchanged_because_reading_was_never_refused()
    {
        // The half that was right, asserted so the correction is visibly a
        // correction of one thing rather than a rewrite of the extractor.
        await Assert.That(Blocked().FilesReadNotEdited).Contains("src/orders.py");
        await Assert.That(Blocked().FilesReadNotEdited).Contains("migrations/0001_init.sql");
    }

    [Test]
    public async Task The_refusal_is_still_in_the_errors_so_the_attempt_is_not_erased()
    {
        // Dropping the path from filesEdited must not delete the fact that it was
        // TRIED. The attempt is what tells somebody the envelope was the problem,
        // and it survives where a failure belongs.
        var errors = Blocked().Errors;

        await Assert.That(errors.Select(e => e.Source)).Contains("Write");
        await Assert.That(errors.Any(e =>
            e.Detail.Contains("0002_add_order_discount.sql", StringComparison.Ordinal))).IsTrue()
            .Because("the path is still recoverable, on the event that says it did not happen.");
    }

    // ---- and what is now the control plane's job rather than a lie here ----

    [Test]
    public async Task Undeclared_but_working_tools_are_still_recoverable_from_what_crosses()
    {
        // Bash leaves refusedMoves and does not vanish: loop.outcome carries the
        // tools the loop reached for, and the control plane holds the envelope
        // that says which were declared. The comparison belongs where both halves
        // are, which is not here - the runner is not an authority on the envelope.
        await Assert.That(Blocked().RefusedMoves).DoesNotContain("Bash");

        var reachedFor = (string[])["Bash", "Read", "Write"];
        var declared = LoopMoves.All.Where(m => m is LoopMoves.Read or LoopMoves.Edit)
            .Select(ClaudeCodeExecutor.ToolFor);

        await Assert.That(reachedFor.Except(declared)).Contains("Bash")
            .Because("derivable from loop.outcome's moves and the pinned envelope, which is "
                   + "where a claim about the envelope should be made.");
    }
}
