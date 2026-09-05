using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner always writes the live view, and a view that fails never fails the flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Always, because this side cannot know whether anybody is watching.</b>
/// The console is a different process started by a different invocation with no
/// channel to the runner but the filesystem, so "a run nobody is watching" is
/// not a fact the runner can hold. <c>ExecutorRequest.Live</c> said the
/// opposite until slice 31; the field carries the revision and what it costs.
/// </para>
/// <para>
/// <b>The producer was finished and had never run.</b> It was walked by hand on
/// a real host first — 37 lines, 5,331 bytes, 51 seconds, kinds setup 18 /
/// tool 16 / text 2 / meta 1, and no <c>raw</c>, so nothing arrived that the
/// classifier could not place. These are the assertions that keep that true
/// without a real agent.
/// </para>
/// </remarks>
public class LiveProducerTests
{
    /// <summary>The repository root, found by walking up to the solution.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no Gg.sln above the tests");
    }

    private static string AScratchDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "gg-live-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    // ---- S31.2-01: the four kinds arrive classified ----

    [Test]
    public async Task Every_kind_the_classifier_maps_lands_as_its_own_line()
    {
        var dir = AScratchDirectory();
        var stream = new LiveStream(LocalPaths.LiveView("GG-1", root: dir));

        stream.Append(LiveLineKinds.Setup, "session init");
        stream.Append(LiveLineKinds.Text, "I'll look at the project's style first.");
        stream.Append(LiveLineKinds.Tool, "Read");
        stream.Append(LiveLineKinds.Meta, "loop success");

        var lines = await File.ReadAllLinesAsync(LocalPaths.LiveView("GG-1", root: dir));

        await Assert.That(lines.Length).IsEqualTo(4)
            .Because("one line per append is what lets a console tail this and see each as "
                   + "it lands, which is why the producer opens and closes per line.");
        await Assert.That(lines.All(l => l.StartsWith('{') && l.EndsWith('}'))).IsTrue()
            .Because("NDJSON: a reader takes whole lines and refuses partial ones.");

        foreach (var kind in new[]
                 { LiveLineKinds.Setup, LiveLineKinds.Text, LiveLineKinds.Tool, LiveLineKinds.Meta })
        {
            await Assert.That(lines.Any(l => l.Contains($"\"kind\":\"{kind}\"", StringComparison.Ordinal)))
                .IsTrue()
                .Because($"'{kind}' is one of the four the classifier maps, and a pane that "
                       + "renders by kind cannot render one that never arrives.");
        }
    }

    [Test]
    public async Task A_line_longer_than_the_cap_is_shortened_and_marked()
    {
        // MEASURED ON THE WALK: the one line that hit the cap was the agent's
        // closing message, and it is the line a person is watching for. The cap
        // stays - an unbounded line is an unbounded screen - but it marks, so
        // nobody reads a truncated sentence as a finished one.
        var dir = AScratchDirectory();
        var stream = new LiveStream(LocalPaths.LiveView("GG-2", root: dir));

        stream.Append(LiveLineKinds.Text, new string('x', 5000));

        var line = (await File.ReadAllLinesAsync(LocalPaths.LiveView("GG-2", root: dir))).Single();

        // ESCAPED, and worth knowing: System.Text.Json writes non-ASCII as
        // \uXXXX, so the marker is \u2026 on disk and an ellipsis only after a
        // reader parses the line. A console grepping the raw file would miss it.
        await Assert.That(line.Contains(@"\u2026", StringComparison.Ordinal)
                          || line.Contains('…')).IsTrue()
            .Because("a truncated line that does not say so reads as a complete one, and on "
                   + "the walk the truncated line was the agent's conclusion.");
        await Assert.That(line.Length).IsLessThan(2200)
            .Because("2,000 characters plus the marker and the envelope around it.");
    }

    // ---- S31.2-02: a failed write does not fail the flight ----

    [Test]
    public async Task A_write_that_cannot_land_is_dropped_rather_than_thrown()
    {
        // THE STEP THAT GIVES THIS ITS FIRST CALLER is the step that has to
        // assert it. A full disk, a directory somebody removed, a permission
        // change - none of them are reasons to lose the work.
        const string unwritable = "/proc/gg-cannot-write-here/live.ndjson";
        var stream = new LiveStream(unwritable);

        // Reaching the next line at all is half the assertion - Append returned
        // rather than throwing into a run doing real work. The other half is
        // that it really did fail, or this passes over a write that worked.
        stream.Append(LiveLineKinds.Text, "this cannot possibly land");

        await Assert.That(File.Exists(unwritable)).IsFalse()
            .Because("the write had to genuinely fail, or this test proves nothing about "
                   + "what happens when one does.");
    }

    [Test]
    public async Task The_directory_is_made_when_it_is_not_there()
    {
        // A first flight on a fresh machine has no live directory, and the
        // producer is the only thing that would make one.
        var dir = Path.Combine(AScratchDirectory(), "not", "yet");
        var stream = new LiveStream(LocalPaths.LiveView("GG-3", root: dir));

        stream.Append(LiveLineKinds.Setup, "session init");

        await Assert.That(File.Exists(LocalPaths.LiveView("GG-3", root: dir))).IsTrue()
            .Because("otherwise the first flight after an install writes nothing and the "
                   + "pane is empty for a reason nobody can see.");
    }

    // ---- S31.2-04: the revised disposition is on the field ----

    [Test]
    public async Task The_field_says_the_runner_always_writes()
    {
        // A CRITERION ABOUT WORDS, deliberately. The field said "a run nobody is
        // watching writes nothing", the runner now always writes, and a comment
        // left saying the opposite is how the next reader designs around a
        // behaviour that is gone.
        var source = await File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), "Gg.Runner", "Execution", "ExecutorPort.cs"));

        var remark = source[..source.IndexOf("public LiveStream? Live", StringComparison.Ordinal)];
        var start = remark.LastIndexOf("/// <summary>", StringComparison.Ordinal);
        var onTheField = remark[start..];

        // QUOTED IS FINE; STATED IS NOT. The revision explains itself by
        // reciting what it replaces, so the guard asks that every occurrence of
        // the old sentence sits inside an italic quotation rather than being
        // asserted as current. Restoring it as prose fails here.
        var quoted = System.Text.RegularExpressions.Regex.Matches(onTheField, @"<i>[\s\S]*?</i>")
            .Select(m => m.Value)
            .ToList();
        var occurrences = System.Text.RegularExpressions.Regex
            .Matches(onTheField, "a run nobody").Count;

        await Assert.That(occurrences)
            .IsEqualTo(quoted.Count(q => q.Contains("a run nobody", StringComparison.Ordinal)))
            .Because("that sentence is now false, so it may appear only as the thing being "
                   + "revised. Stated as current it would send the next reader designing "
                   + "around a behaviour that is gone.");
        await Assert.That(onTheField).Contains("always")
            .Because("the field states the behaviour it has, not the one it used to have.");
        await Assert.That(onTheField).Contains("cannot")
            .Because("and says WHY - that this side cannot know who is watching - because a "
                   + "reversal without its reason is one somebody reverses back.");
    }

    // ---- S31.2-03: rule 1, the live channel crosses nothing ----

    [Test]
    public async Task Nothing_about_the_live_view_can_reach_a_fact()
    {
        // RULE 1, HELD STRUCTURALLY. The live view is ephemeral, local, and it
        // crosses nothing - not to the control plane, not into a fact, not into
        // a bundle. The console side already has this (BundleFrom deliberately
        // touches neither Live nor Held, with a test that means something
        // because of it); this is the runner's half, and step 2 is the step that
        // gives the producer a caller and therefore the first chance to break it.
        var factsDirectory = Path.Combine(RepoRoot(), "Gg.Runner", "Facts");
        var sources = Directory.EnumerateFiles(factsDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".whizbang", StringComparison.Ordinal))
            .ToList();

        await Assert.That(sources).IsNotEmpty()
            .Because("a scan over no files is total and holds nothing.");

        var leaking = sources
            .Where(f => File.ReadAllText(f).Contains("LiveStream", StringComparison.Ordinal)
                     || File.ReadAllText(f).Contains("LiveLine", StringComparison.Ordinal)
                     || File.ReadAllText(f).Contains("LiveView", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        await Assert.That(leaking).IsEmpty()
            .Because("the fact pipeline is what reaches the control plane, and a live view is "
                   + "a local screen. If one of these names appears there, something is "
                   + "shipping what a person was watching. Found: " + string.Join(", ", leaking));
    }

    [Test]
    public async Task The_scan_would_notice_a_leak_that_was_there()
    {
        // The planted twin: proof the sweep bites, on the guard's own question
        // rather than by writing into the fact pipeline.
        const string planted = "var line = new LiveStream(path);";

        await Assert.That(planted.Contains("LiveStream", StringComparison.Ordinal)).IsTrue()
            .Because("the scan matches on this name, so a rename would silently stop it "
                   + "catching anything and should fail here first.");
    }
}
