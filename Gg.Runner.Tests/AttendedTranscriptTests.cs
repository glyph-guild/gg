using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A hand-flight's conversation is not gg's to hold, and it can never reach the
/// tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>A TRANSCRIPT INSIDE A TREE IS A TRANSCRIPT IN SOMEBODY'S PULL REQUEST.</b>
/// It holds the whole conversation — every file that was read, every path named,
/// whatever the person typed while thinking out loud. The tree is what gets
/// pushed and proposed, so a transcript written into it is customer-visible by
/// the next <c>git add</c>.
/// </para>
/// <para>
/// <b>The attended path writes no transcript at all, which is stronger than not
/// copying one.</b> <c>ClaudeCodeExecutor</c> redirects the child's output and
/// captures it; <c>AttendedExecutor</c> redirects nothing, because a person is at
/// the keyboard and the child owns the screen. There is nothing to copy.
/// </para>
/// <para>
/// <b>Asserted where the guarantee actually lives.</b> The first version of this
/// walked the tree after a flight and read <c>TranscriptPath</c> off the recorded
/// requests. Neither works and both would have looked like they did: the harness
/// records a request RECONSTRUCTED from the process start info, whose transcript
/// path is empty, and <c>ScratchTreeRoot</c> deletes the trees before the helper
/// returns — so an empty directory listing would have proved that the directory
/// was gone. What is checked instead is the path <c>TranscriptStore</c> answers,
/// which is the value the runner actually hands over.
/// </para>
/// </remarks>
public class AttendedTranscriptTests
{
    // ---- S26.9-04 ----

    [Test]
    public async Task It_declares_that_it_could_not_identify_the_session_file()
    {
        // WHAT S26.0-03 MEASURED, SAID OUT LOUD RATHER THAN LEFT TO A READER.
        // The path is `~/.claude/projects/<mangled cwd>/<session_id>.jsonl`, and
        // an attended session cannot name it: the id arrives in the
        // `stream-json` init event, which an interactive session does not
        // produce, and one flight's tree directory held TWO files - so the
        // newest is a guess and a guess attributed to a flight is worse than an
        // absence.
        //
        // THE COMMENT BELOW ASSERTED THIS AND NOTHING DID. It says a reader
        // "learns that from the declaration on loop.attended", and there was no
        // such declaration - AttendedGaps held turns, moves and move-bound, and
        // AttendedExecutor mentions the transcript nowhere at all. So the
        // absence was learned exactly the way S26.9-05 says it must not be: from
        // an empty fetch.
        //
        // Declared as a GAP rather than as a member of its own, because that is
        // what the other three are: a value a reader would otherwise infer
        // wrongly. Null there reads as "not shipped yet" on a platform where
        // facts arrive in batches, and "never, and here is why" is a different
        // thing.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync();

        var attended = AttendedExecutorTests.Shipped(protocol, FactKinds.LoopAttended);

        await Assert.That(attended).Count().IsEqualTo(1);
        await Assert.That(attended[0].Attended!.Unmeasured).Contains(AttendedGaps.Transcript)
            .Because("a reader of a hand-flown flight has to be able to tell that no session "
                   + "file will ever be referenced from a batch that has not carried one yet, "
                   + "and only the flight's own record can say which.");
    }

    // ---- S26.9-05 ----

    [Test]
    public async Task What_gg_captures_lives_under_its_own_state_root()
    {
        // WHICH IS WHAT MAKES "OUTSIDE IT" MEAN SOMETHING. gg sweeps this
        // directory and can serve what is in it; a person's Claude Code session
        // file is somewhere else entirely, chosen by that tool, and gg neither
        // sweeps it nor can fetch it. A reader learns that from a declaration
        // rather than from an empty fetch.
        var offered = new TranscriptStore().For("flight-1", "implement");

        await Assert.That(offered.StartsWith(Gg.Local.LocalPaths.Transcripts(), StringComparison.Ordinal))
            .IsTrue();
    }

    // ---- S26.9-06 ----

    [Test]
    public async Task The_path_it_offers_is_never_inside_a_working_tree()
    {
        // STRUCTURAL, so it holds against a future convenience rather than
        // against present intent. Even something that DID write the offered path
        // would put the file in gg's own directory - the state root and the tree
        // root are different places, and a transcript cannot land in a pull
        // request by that route.
        var offered = new TranscriptStore().For("flight-1", "implement");

        using var trees = new ScratchTreeRoot();

        await Assert.That(offered.StartsWith(trees.Root.Path, StringComparison.Ordinal)).IsFalse()
            .Because("the tree is the one place it must never be, and that is a property of "
                   + "where the two roots are rather than of what any writer remembers.");
    }

    [Test]
    public async Task A_hand_flown_session_writes_no_transcript_at_all()
    {
        // NOTHING TO COPY, WHICH IS WHY THE ONE ABOVE IS ENOUGH. The runner
        // hands every executor a path; the attended one redirects nothing and
        // writes nowhere, so the file gg would have swept does not exist. A
        // reader looking for it learns that from the declaration on
        // loop.attended, not from an empty fetch.
        var offered = new TranscriptStore().For("flight-1", "implement");

        if (File.Exists(offered))
        {
            File.Delete(offered);
        }

        await AttendedExecutorTests.FlownAsync(edits: "PERSON.md");

        await Assert.That(File.Exists(offered)).IsFalse()
            .Because("the child owned the screen and this process saw none of it, so there "
                   + "was never anything to write.");
    }
}
