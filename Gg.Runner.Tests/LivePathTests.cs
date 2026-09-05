using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// One filename, computed the same way by the half that writes and the half that reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two computations of one filename is a console that tails nothing and
/// reports no error.</b> That is the failure this exists to prevent, and it is
/// the quiet kind: a reader pointed at a path nobody writes returns an empty
/// file for ever and looks exactly like a flight that has said nothing yet.
/// </para>
/// <para>
/// The convention could not live in <c>Gg.Runner</c>, where it started -
/// <c>Gg.Console</c> does not reference it. It could not live in
/// <c>Gg.Contracts</c> either: that is the wire contract, the artifact a
/// customer audits and the package good-grief consumes from a release, and a
/// local filesystem path is not part of a contract between two machines.
/// </para>
/// </remarks>
public class LivePathTests
{
    /// <summary>
    /// This test's own state root, passed rather than exported.
    /// </summary>
    /// <remarks>
    /// <b>XDG_STATE_HOME is process-global and this suite runs in parallel.</b>
    /// The first version of these tests set and restored it around each call,
    /// and two of them failed at once because a sibling had it set to something
    /// else while they read it - a test whose precondition is global state, in a
    /// suite where another test performs a global write. Passing the root is
    /// both the fix and the better shape.
    /// </remarks>
    private const string Root = "/tmp/gg-live-test";

    // ---- S31.1-01 ----

    [Test]
    public async Task The_writer_and_the_reader_compute_the_same_path()
    {
        // THE WRITER'S ENTRY POINT AND THE SHARED ONE, which is the whole claim:
        // LiveStream still answers, and what it answers is what the console can
        // compute without referencing Gg.Runner at all.
        const string flight = "01a07028-7164-77ee-8ebb-1a2a069646b9";

        var byTheWriter = LiveStream.DefaultPath(flight);
        var byTheReader = LocalPaths.LiveView(flight);

        await Assert.That(byTheWriter).IsEqualTo(byTheReader)
            .Because("the runner writes this file and the console reads it. If the two "
                   + "differ by so much as a separator the console tails nothing, and an "
                   + "empty file is indistinguishable from a flight that has not spoken.");
    }

    [Test]
    public async Task The_path_is_normalised_so_two_spellings_cannot_disagree()
    {
        // The first version composed this as transcripts/../live, which names
        // the right directory and spells it with a parent segment - so two
        // halves comparing strings could disagree about one place.
        var path = LocalPaths.LiveView("GG-1", stateHome: Root);

        await Assert.That(path).DoesNotContain("..")
            .Because("a path carrying a parent segment compares unequal to the same "
                   + "directory spelled directly, and both halves compare strings.");
        await Assert.That(Path.IsPathFullyQualified(path)).IsTrue();
    }

    [Test]
    public async Task A_flight_id_that_is_not_a_filename_cannot_leave_the_directory()
    {
        var path = LocalPaths.LiveView("../../etc/passwd", stateHome: Root);

        await Assert.That(Path.GetDirectoryName(path))
            .IsEqualTo(Path.GetFullPath(LocalPaths.LiveViews(Root)))
            .Because("an id is used as a filename, so an id with a separator in it would "
                   + "otherwise name a file somewhere else entirely.");
    }

    // ---- S31.1-03 ----

    [Test]
    public async Task The_live_directory_is_a_sibling_of_the_transcripts_not_a_child()
    {
        var live = LocalPaths.LiveViews(Root);
        var transcripts = LocalPaths.Transcripts(Root);

        await Assert.That(live).IsNotEqualTo(transcripts);
        await Assert.That(live.StartsWith(transcripts, StringComparison.Ordinal)).IsFalse()
            .Because("live views are deletable and transcripts are not. Somebody clearing "
                   + "views must not have to be careful about which files they are, and a "
                   + "directory under the transcripts makes one rm take the evidence too.");
        await Assert.That(Path.GetDirectoryName(live)).IsEqualTo(Path.GetDirectoryName(transcripts))
            .Because("siblings under one root, so a state directory somebody moves moves "
                   + "both halves together.");
    }

    [Test]
    public async Task Everything_lands_under_the_state_root_a_person_can_move()
    {
        var root = LocalPaths.StateRoot(Root);

        await Assert.That(root).StartsWith("/tmp/gg-live-test")
            .Because("XDG_STATE_HOME moves all of it at once, which is what lets a test "
                   + "have its own and an operator relocate a machine's state.");
        await Assert.That(root).EndsWith("good-grief")
            .Because("state is shared with everything else on the machine, so gg keeps to "
                   + "its own directory under it.");
    }
}
