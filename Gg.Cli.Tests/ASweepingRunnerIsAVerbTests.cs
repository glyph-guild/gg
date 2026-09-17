namespace Gg.Cli.Tests;

/// <summary>
/// The verb a resident runner sweeps a watch under.
/// </summary>
/// <remarks>
/// <para>
/// <b>One watch, named, and no default.</b> A watch is the pull point's
/// instruction to this machine: it names the tracker, the credential and the
/// query somebody reviewed. A verb that swept every watch it could find would
/// have this runner claim work nobody pointed it at, and the fleet's own rule
/// is that a runner never derives what it may do.
/// </para>
/// <para>
/// <b>Machine-facing, so it is absent from the usage on purpose</b> — the
/// disposition <c>runner read</c> and <c>runner tools</c> have.
/// <c>EveryVerbIsDiscoverableTests</c> walks the FIRST word of each arm, and
/// that word is <c>runner</c>, which the usage already names.
/// </para>
/// </remarks>
public class ASweepingRunnerIsAVerbTests
{
    [Test]
    public async Task The_verb_a_watch_is_swept_under_parses()
    {
        var parsed = CliArgs.Parse(["runner", "sweep", "nightly-triage"]);

        await Assert.That(parsed).IsTypeOf<CliAction.RunnerSweep>();
        await Assert.That(((CliAction.RunnerSweep)parsed).Watch).IsEqualTo("nightly-triage");
    }

    [Test]
    public async Task A_watch_whose_name_has_a_space_is_one_argument()
    {
        var parsed = CliArgs.Parse(["runner", "sweep", "nightly triage"]);

        await Assert.That(((CliAction.RunnerSweep)parsed).Watch).IsEqualTo("nightly triage")
            .Because("a watch's name is a person's words, and the protocol escapes it into the "
                   + "path rather than this file rejecting it.");
    }

    [Test]
    public async Task A_sweep_with_no_watch_is_refused_rather_than_defaulted()
    {
        var parsed = CliArgs.Parse(["runner", "sweep"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>()
            .Because("sweeping whatever this runner could find would claim work nobody pointed "
                   + "it at.");
    }
}
