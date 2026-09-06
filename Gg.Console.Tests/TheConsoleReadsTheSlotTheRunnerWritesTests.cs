using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The console looks for this machine's runner where this machine's runner
/// puts itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two slots and the console read the wrong one.</b>
/// <c>FileRunnerStore.DefaultPath()</c> is <c>runner.json</c> and belongs to
/// <c>gg runner maintain</c> - it keeps that name so an upgrade does not take a
/// pool host down. <c>gg runner up</c> writes
/// <c>PathFor(Environment.MachineName)</c>, a slot per name, because a pool host
/// runs both and a single file meant whichever registered last owned the only
/// credential.
/// </para>
/// <para>
/// <b>So the tab told a machine with a runner that it had none.</b>
/// <c>LocalRunnerId</c> came back null, this machine's row was never marked,
/// the notice said nothing is registered here and the start key stayed live -
/// which is how somebody ends up with two runner processes from two presses.
/// Observed: a console with <c>gg runner up</c> running twice, and
/// <c>~/.config/good-grief/runner-&lt;machine&gt;.json</c> on disk the whole
/// time.
/// </para>
/// </remarks>
public class TheConsoleReadsTheSlotTheRunnerWritesTests
{
    [Test]
    public async Task The_two_slots_are_genuinely_different_files()
    {
        // THE ANCHOR. If these ever become one path the guard below is checking
        // a distinction that no longer exists, and it should say so rather than
        // pass for ever.
        await Assert.That(FileRunnerStore.PathFor("some-machine"))
            .IsNotEqualTo(FileRunnerStore.DefaultPath())
            .Because("one slot per name, because a pool host runs `runner up` as itself and "
                   + "`runner maintain` as <machine>:maintain.");
    }

    [Test]
    public async Task The_console_reads_the_named_slot_and_not_the_unnamed_one()
    {
        var program = Sources.Read("Gg.Cli", "Program.cs");

        var reads = program.Split("LocalRunnerId =")
            .Skip(1)
            .Select(after => after[..Math.Min(200, after.Length)])
            .ToList();

        await Assert.That(reads).Count().IsEqualTo(1)
            .Because("one place decides which runner is this machine's.");

        await Assert.That(reads[0]).Contains("PathFor(")
            .Because("the unnamed slot is the maintain service's - reading it says a pool "
                   + $"host's maintain runner is the one you are sitting at. Found:\n{reads[0]}");
    }

    [Test]
    public async Task And_it_is_the_same_slot_the_runner_registers_into()
    {
        // NOT JUST `A NAMED SLOT'. There are three named slots in this file and
        // they are three different runners - `runner up' is this machine,
        // `fly --hand' is the attended one beside it, and a pool member is
        // keyed on its container. Naming any of the others would satisfy the
        // row above and still be the wrong runner.
        var program = Sources.Read("Gg.Cli", "Program.cs");

        await Assert.That(Slot(program, "LocalRunnerId =")).IsEqualTo(
            Slot(program, "static async Task<int> RunnerUpAsync"))
            .Because("the console and `gg runner up` have to name the same slot, or the tab "
                   + "tells a machine with a runner that it has none.");
    }

    /// <summary>The first slot named after <paramref name="from"/>.</summary>
    private static string Slot(string program, string from)
    {
        var after = program[program.IndexOf(from, StringComparison.Ordinal)..];
        var call = after[(after.IndexOf("PathFor(", StringComparison.Ordinal) + 8)..];

        return call[..call.IndexOf(')')];
    }
}
