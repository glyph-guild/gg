namespace Gg.Client.Tests;

/// <summary>
/// The bound on a local git read is one the code can actually reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: reading the airspace freezes.</b> That read calls
/// <c>AirspaceTree.Dirty</c>, which calls <c>Git.Status</c>, which starts git
/// and drains its output.
/// </para>
/// <para>
/// <b>AND THE TIMEOUT IS AFTER THE UNBOUNDED READ.</b>
/// <c>StandardOutput.ReadToEnd()</c> blocks until the pipe CLOSES, which for a
/// git that never returns is never — so <c>WaitForExit(Patience)</c> on the
/// line below is never reached. The ten seconds guard only the gap between git
/// closing its streams and exiting, which is not the failure anybody has.
/// </para>
/// <para>
/// <b>The comment already claims the property:</b> <i>"Bounded because a git
/// that never returns would hang the verb with no output — and the failure a
/// person actually hits is a lock file left by an editor"</i>. That is the
/// defect family this repository keeps finding: prose asserting what the code
/// lacks, and the prose is why nobody looked again.
/// </para>
/// <para>
/// <b>DRAINING ONE PIPE AT A TIME IS THE SECOND HALF.</b> Reading stdout to
/// completion while the child fills the stderr pipe blocks the child, which
/// then never closes stdout — a deadlock neither stream can break. Both reads
/// have to be in flight before anything waits.
/// </para>
/// <para>
/// <b>Asserted against the source, because the executable is <c>git</c> by
/// name.</b> Nothing here can hand this a child that hangs, so what a test can
/// read is the order of the calls — <c>EveryTabSaysWhereFocusLandsTests</c>'
/// technique, and the reason it exists.
/// </para>
/// </remarks>
public class TheGitTimeoutIsReachableTests
{
    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, "Gg.Client", "Git.cs"));
    }

    [Test]
    public async Task Nothing_drains_a_pipe_to_the_end_before_the_wait()
    {
        var git = Source();

        await Assert.That(git).DoesNotContain("ReadToEnd()", StringComparison.Ordinal)
            .Because("a synchronous drain blocks until the pipe closes, and a git that "
                   + "never returns never closes it - so every bound below such a call is "
                   + "unreachable, including the one this file declares.");
    }

    [Test]
    public async Task Both_pipes_are_in_flight_before_anything_waits()
    {
        var git = Source();

        var out_ = git.IndexOf("StandardOutput.ReadToEndAsync", StringComparison.Ordinal);
        var err = git.IndexOf("StandardError.ReadToEndAsync", StringComparison.Ordinal);
        var wait = git.IndexOf("WaitForExit", StringComparison.Ordinal);

        await Assert.That(out_).IsGreaterThan(-1);
        await Assert.That(err).IsGreaterThan(-1);
        await Assert.That(wait).IsGreaterThan(-1);

        await Assert.That(out_).IsLessThan(wait)
            .Because("the read has to be started before the wait, or the wait is what the "
                   + "read is waiting behind.");

        await Assert.That(err).IsLessThan(wait)
            .Because("and both of them, because draining one while the child fills the "
                   + "other is a deadlock neither can break.");
    }

    [Test]
    public async Task The_wait_still_kills_what_it_gave_up_on()
    {
        // THE BOUND IS ONLY A BOUND IF SOMETHING STOPS. A wait that expired and
        // left the child running would leak a git per read on a machine where
        // this is already going wrong.
        var git = Source();

        var wait = git.IndexOf("WaitForExit", StringComparison.Ordinal);
        var after = git[wait..];

        await Assert.That(after).Contains("Kill(", StringComparison.Ordinal);
        await Assert.That(after).Contains("did not answer in time", StringComparison.Ordinal)
            .Because("and it says so, because a silent empty answer reads as a clean tree.");
    }

    [Test]
    public async Task A_clean_read_still_answers_with_what_git_said()
    {
        // THE ORDINARY PATH, DRIVEN RATHER THAN READ. git is on this machine and
        // this worktree is a repository, so a status here is a real invocation
        // through the real code - which is what stops the source assertions
        // above from passing over something that no longer runs.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        var status = Git.Status(here!.FullName);

        await Assert.That(status).IsNotNull()
            .Because("a repository answers, and an answer of no lines is a clean tree "
                   + "rather than a failure.");
    }
}
