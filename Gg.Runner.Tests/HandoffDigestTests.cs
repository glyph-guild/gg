using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// Can somebody who was not there pick the work up from the digest alone?
/// </summary>
/// <remarks>
/// <para>
/// <b>The acceptance test is allowed to fail, and this one does.</b> ADR-0006
/// rejected reference-everything partly because <i>handoff dies</i>, and its
/// load-bearing claim is that a digest carries enough to act on. Run honestly -
/// a real flight, the transcript hidden - the digest supports <i>what was
/// looked at</i> and does not support <i>what was concluded</i>.
/// </para>
/// <para>
/// <b>What was missing, in the words it was missing in.</b> The run in
/// <c>agent-considered.ndjson</c> was told to match the project's style. It read
/// <c>src/util.py</c> and <c>README.md</c>, edited neither, and its own text
/// said: <i>"Neither existing file has a docstring, so there's no in-repo
/// convention to match."</i> That sentence is the finding of the exploration -
/// the reason the two files were ruled out - and the digest cannot carry it,
/// because carrying it would mean either trusting the agent's account of its own
/// reasoning or asking a model to produce one. Both are the thing this step
/// exists to refuse.
/// </para>
/// <para>
/// So the digest answers <b>"what did it look at and leave alone"</b> and leaves
/// <b>"and what did it decide about them"</b> to be re-derived. That is a real
/// limit on the three-way split rather than a gap in this extractor, and the
/// wrong response is a summariser.
/// </para>
/// </remarks>
public class HandoffDigestTests
{
    private static LoopDigest Digest(string fixture, string outcome = "completed") =>
        TranscriptDigest.Extract(
            File.ReadAllText(Path.Combine(Root(), "Gg.Runner.Tests", "Fixtures", fixture)),
            "implement", ["/work/tree"], outcome, refused: []);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    [Test]
    public async Task The_digest_says_what_was_looked_at_and_left_alone()
    {
        // The half that works, and the half ADR-0006 needed. A person taking
        // this over knows the style question was investigated in util.py and
        // README.md without opening either, and knows greet.py is the work.
        var digest = Digest("agent-considered.ndjson");

        await Assert.That(digest.FilesReadNotEdited).IsEquivalentTo(
            (string[])["src/util.py", "README.md"]);
        await Assert.That(digest.FilesEdited).IsEquivalentTo((string[])["src/greet.py"]);
        await Assert.That(digest.StopReason).IsEqualTo(LoopOutcomes.Completed);
    }

    [Test]
    public async Task The_digest_says_what_was_tried_and_how_it_went()
    {
        // The second run searched for slugify and ran pytest, which failed
        // because pytest is not installed. Somebody taking over does not repeat
        // the command as their first move, which is the concrete thing a digest
        // buys.
        var digest = Digest("agent-searched-and-failed.ndjson");

        await Assert.That(string.Join(" ", digest.Searches)).Contains("slugify");
        await Assert.That(digest.Errors.Single().Source).IsEqualTo("Bash");
        await Assert.That(digest.Errors.Single().Detail).Contains("No module named pytest");
    }

    [Test]
    public async Task What_it_concluded_about_what_it_ruled_out_is_not_in_the_digest()
    {
        // THE HONEST FAILURE, asserted rather than written up somewhere nobody
        // reads. The agent's own words were "Neither existing file has a
        // docstring, so there's no in-repo convention to match" - the reason the
        // two files were ruled out - and no field here carries it.
        //
        // Asserted as an ABSENCE on purpose. The day somebody adds a summariser
        // this test fails, and the failure is the argument: whatever produced
        // that sentence read the transcript, and the transcript can contain text
        // addressed to a model.
        var digest = Digest("agent-considered.ndjson");

        var everything = string.Join(
            " ",
            [.. digest.FilesReadNotEdited, .. digest.FilesEdited, .. digest.Searches,
             .. digest.Errors.Select(e => e.Source + " " + e.Detail), digest.StopReason]);

        await Assert.That(everything).DoesNotContain("convention")
            .Because("the conclusion is not carried, and carrying it would mean trusting the "
                   + "agent's account of its own reasoning - which is the thing step 3 refused for "
                   + "the manifest, one artifact earlier.");

        await Assert.That(digest.FilesReadNotEdited).IsNotEmpty()
            .Because("what it looked at IS carried. The limit is the conclusion, not the signal, "
                   + "and a test that proved nothing was carried would be reporting the wrong "
                   + "finding.");
    }

    [Test]
    public async Task A_search_says_what_was_looked_for_and_not_what_was_found()
    {
        // The second thing missing, and the one with a live design tension.
        // "Searched for slugify" without "found it in src/util.py" means the
        // next person runs the search again.
        //
        // Carrying the RESULT is not a small change: a grep result is file
        // CONTENT, and S2.0-02 says no source file content crosses - only paths,
        // counts and hashes. So the honest options are paths-and-counts or
        // nothing, and that is a decision about the criterion rather than a
        // detail of this extractor.
        var digest = Digest("agent-searched-and-failed.ndjson");

        await Assert.That(digest.Searches.Single()).Contains("slugify");
        await Assert.That(digest.Searches.Single()).DoesNotContain("src/util.py")
            .Because("the match was in util.py and the digest does not say so. Recorded as a "
                   + "finding: the fix is paths and counts, never the matched line.");
    }
}
