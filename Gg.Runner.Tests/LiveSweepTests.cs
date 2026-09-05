using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// Every buffer is bounded, and the sweep cannot take evidence with it.
/// </summary>
/// <remarks>
/// <b>The bounds are as much about redraw cost as about memory.</b> A pane
/// appending on every tick with a full rebuild is the thing that makes a
/// console feel broken, and a file nothing caps is a disk a long flight fills.
/// </remarks>
public class LiveSweepTests
{
    private static string AScratchDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "gg-bounds-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    // ---- S31.5-03: the file is capped ----

    [Test]
    public async Task The_file_stops_growing_and_keeps_the_newest()
    {
        var dir = AScratchDirectory();
        var path = LocalPaths.LiveView("GG-1", root: dir);
        var stream = new LiveStream(path);

        // Enough 2,000-character lines to pass half a megabyte several times.
        for (var i = 0; i < 600; i++)
        {
            stream.Append(LiveLineKinds.Text, $"line {i} " + new string('x', 1900));
        }

        var size = new FileInfo(path).Length;
        var lines = await File.ReadAllLinesAsync(path);

        await Assert.That(size).IsLessThan(700 * 1024)
            .Because("a view nothing caps is a disk a long flight fills, and the cap is half a "
                   + "megabyte with a roll that keeps the newest half.");
        await Assert.That(lines[^1]).Contains("line 599")
            .Because("the NEWEST is what a person is reading: keeping the oldest would freeze "
                   + "a long run's pane at whatever it said an hour ago.");
        await Assert.That(lines.All(l => l.StartsWith('{') && l.EndsWith('}'))).IsTrue()
            .Because("the roll cuts at a line boundary, or the reader's refusal of partial "
                   + "lines would silently drop the first line after every roll.");
    }

    // ---- S31.5-05: the sweep, and what it must not touch ----

    [Test]
    public async Task A_sweep_removes_views_a_previous_life_left()
    {
        var dir = AScratchDirectory();
        await File.WriteAllTextAsync(Path.Combine(dir, "one.ndjson"), "{}\n");
        await File.WriteAllTextAsync(Path.Combine(dir, "two.ndjson"), "{}\n");

        var swept = new LiveViewSweep(dir).SweepOrphans();

        await Assert.That(swept).IsEqualTo(2)
            .Because("a runner that is starting holds no lease, so every view here belongs to "
                   + "a process that is gone - which makes the rule 'all of them' rather than "
                   + "a set difference against state we would have had to persist.");
        await Assert.That(Directory.EnumerateFiles(dir, "*.ndjson")).IsEmpty();
    }

    [Test]
    public async Task The_sweep_cannot_reach_a_transcript()
    {
        // THE VERSION OF THIS WORTH BEING UNABLE TO WRITE is the one that takes
        // the evidence with it. The directories are siblings for exactly this
        // reason: a transcript is the only copy of what an agent did.
        var state = AScratchDirectory();
        var live = Path.Combine(state, "live");
        var transcripts = Path.Combine(state, "transcripts");
        Directory.CreateDirectory(live);
        Directory.CreateDirectory(transcripts);

        await File.WriteAllTextAsync(Path.Combine(live, "a-flight.ndjson"), "{}\n");
        var evidence = Path.Combine(transcripts, "a-flight.ndjson");
        await File.WriteAllTextAsync(evidence, "what the agent actually did\n");

        new LiveViewSweep(live).SweepOrphans();

        await Assert.That(File.Exists(evidence)).IsTrue()
            .Because("live views are deletable and transcripts are not. Somebody clearing "
                   + "views must not have to be careful about which files they are.");
        await Assert.That(await File.ReadAllTextAsync(evidence))
            .IsEqualTo("what the agent actually did\n");
    }

    [Test]
    public async Task The_sweep_leaves_anything_that_is_not_a_view()
    {
        var dir = AScratchDirectory();
        var stranger = Path.Combine(dir, "notes.txt");
        await File.WriteAllTextAsync(stranger, "put here by hand");

        new LiveViewSweep(dir).SweepOrphans();

        await Assert.That(File.Exists(stranger)).IsTrue()
            .Because("being in the way is not a reason to delete something; only .ndjson "
                   + "views are this sweep's to remove.");
    }

    [Test]
    public async Task A_sweep_over_a_directory_that_is_not_there_is_not_an_error()
    {
        var swept = new LiveViewSweep(Path.Combine(AScratchDirectory(), "never-made"))
            .SweepOrphans();

        await Assert.That(swept).IsEqualTo(0)
            .Because("a first start on a fresh machine has no live directory, and a runner "
                   + "that refused to boot over that would be a view failing a flight.");
    }
}
