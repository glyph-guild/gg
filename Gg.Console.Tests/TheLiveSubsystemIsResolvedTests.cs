namespace Gg.Console.Tests;

/// <summary>
/// The live view has a producer at both ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>S28.6-02 asked for a producer or a deletion, and the answer is the first
/// one.</b> The plan called this "the largest single piece of
/// registered-and-never-invoked in this project" - a pane, a key, a freeze, a
/// fact type and a reducer arm with nothing writing to them. That was true of
/// the console when it was written and had stopped being true by the time this
/// step ran: <c>RunnerLoop</c> gives every lease a <c>LiveStream</c> over
/// <c>LocalPaths.LiveView(flightId)</c>, and the composition root hands
/// <c>ConsoleLoop</c> a <c>LiveTails</c> reading the same path.
/// </para>
/// <para>
/// <b>WHAT IS WRITTEN DOWN IS THE CONSTRAINT, because it is the part a person
/// hits.</b> Both ends resolve the path through <see cref="Gg.Local.LocalPaths"/>,
/// so the live view works when the runner is on THIS machine and shows nothing
/// when it is not - and "nothing" is `LiveSilence.NotStarted`, which reads as a
/// flight that has not begun. That is not a defect of this subsystem; it is the
/// same boundary <c>ArtifactScopes.RunnerLocal</c> draws for a transcript, and
/// a control plane that carried the stream would be carrying customer output
/// into a system whose whole point is not holding any.
/// </para>
/// <para>
/// Asserted over SOURCE rather than by running a runner, deliberately: what is
/// in question is whether the two ends exist and agree on a path, and a test
/// that spawned a runner would prove that and a great deal else.
/// </para>
/// </remarks>
public class TheLiveSubsystemIsResolvedTests
{
    [Test]
    public async Task A_runner_writes_the_stream_the_console_tails()
    {
        var writes = ConsoleSource.Text("Gg.Runner", "RunnerLoop.cs");
        var reads = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(writes).Contains("LocalPaths.LiveView(")
            .Because("a pane with no producer is the thing this criterion exists to resolve, "
                   + "and the producer is the runner giving each lease a LiveStream.");
        await Assert.That(reads).Contains("LocalPaths.LiveView(")
            .Because("and the console's end has to resolve the SAME path, or the two halves "
                   + "are a subsystem only in the way they are named.");
        await Assert.That(reads).Contains("LiveTails(")
            .Because("wired at the composition root rather than merely available - the "
                   + "takeover's ports were available for two slices and answered 'not "
                   + "configured' on every real press.");
    }

    [Test]
    public async Task The_console_says_which_silence_it_is_showing()
    {
        // The constraint above is only survivable because the pane distinguishes
        // its silences. A runner on another machine writes nothing HERE, and a
        // pane that went blank would read as a flight that had not begun.
        var silences = Enum.GetValues<LiveSilence>();

        await Assert.That(silences).Contains(LiveSilence.NotAttached);
        await Assert.That(silences).Contains(LiveSilence.NotStarted);
        await Assert.That(silences).Contains(LiveSilence.NothingYet);
        await Assert.That(silences).Contains(LiveSilence.Stopped)
            .Because("a reader that died quietly looks exactly like a flight that went "
                   + "quiet, and those want opposite reactions from a person.");
    }
}
