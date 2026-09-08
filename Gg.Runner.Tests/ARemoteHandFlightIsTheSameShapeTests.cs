using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// One way to be attended, and one way to say a person flew it.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013 says a remote hand-flight is local hand-flying at a distance, and
/// the criterion's reason is the part that bites:</b> two code paths here would
/// be the defect <c>FileRunnerStore.PathFor</c> exists to have fixed, arriving
/// again. So this asserts there is one — no remote-versus-local fork anywhere on
/// the attended path.
/// </para>
/// <para>
/// <b>But there are TWO attended concepts, and collapsing them would be a
/// defect.</b> The LEASE says a person is watching, which is what opens a
/// channel. The EXECUTOR measuring nothing says a person did the work, which is
/// what emits <c>loop.attended</c>. A single flag driving both would make a fleet
/// flight somebody merely watched claim that a person flew it — an agent's work
/// recorded as a human's, on a platform whose premise is that work is recorded.
/// </para>
/// <para>
/// <b>Which is why a remote attended flight today is WATCHED rather than
/// driven.</b> Driving needs a third entry in <c>RunnerAskKinds</c> — a contract
/// change with a fingerprint bump, deliberately — and a terminal on the far end.
/// <c>AttendedExecutor</c> inherits the terminal, so it is local by construction:
/// a supervised runner has no keyboard for a child to own. S34.0-04 is the
/// question that has to be answered by somebody actually watching before
/// anything is widened, and inventing that answer is what a walk exists to
/// prevent.
/// </para>
/// </remarks>
public class ARemoteHandFlightIsTheSameShapeTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    private static IEnumerable<(string File, int Line, string Text)> Runner()
    {
        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(Root(), "Gg.Runner"), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i++)
            {
                yield return (Path.GetFileName(path), i + 1, lines[i]);
            }
        }
    }

    [Test]
    public async Task One_place_decides_that_somebody_is_watching()
    {
        // THE LEASE SAYS SO, ONCE. A second reader would be a second answer to
        // "is a person on the other end of this", and the two would disagree
        // the first time either moved.
        var readers = Runner()
            .Where(l => Regex.IsMatch(l.Text, @"\blease\.Attended\b"))
            .Select(l => $"{l.File}:{l.Line}")
            .ToList();

        await Assert.That(readers).HasSingleItem()
            .Because("the channel's lifetime is the flight's, and one place deciding is what "
                   + "makes that a property rather than a convention. Found: "
                   + string.Join(", ", readers));

        await Assert.That(readers[0]).StartsWith("RunnerLoop.cs")
            .Because("it belongs in the hold, which is what owns the lease.");
    }

    [Test]
    public async Task One_place_says_a_person_flew_it()
    {
        // AND IT IS THE EXECUTOR THAT SAYS SO, not the lease. `loop.attended` is
        // emitted when the executor measured NOTHING - which is what handing a
        // terminal to a person looks like from here.
        var built = Runner()
            .Where(l => l.Text.Contains("new LoopAttended", StringComparison.Ordinal))
            .Select(l => $"{l.File}:{l.Line}")
            .ToList();

        await Assert.That(built).HasSingleItem()
            .Because("two ways to record that a person flew a flight are two records that can "
                   + "disagree about who did the work. Found: " + string.Join(", ", built));
    }

    [Test]
    public async Task The_two_are_not_the_same_flag()
    {
        // THE DEFECT THIS PREVENTS. If the lease's marker also emitted
        // `loop.attended`, every fleet flight somebody merely WATCHED would
        // claim a person flew it - an agent's work recorded as a human's, on a
        // platform whose whole premise is that work is recorded.
        var source = File.ReadAllText(
            Path.Combine(Root(), "Gg.Runner", "RunnerLoop.cs"));

        await Assert.That(source).Contains("new LoopAttended");
        await Assert.That(source).Contains("lease.Attended");

        // SCOPED TO THE METHOD THAT BUILDS THE FACT, not to the distance
        // between two strings. This asked whether one exact line appeared
        // between the first mention of each, and both halves of that were
        // wrong: the session moved from the hold up into WorkAsync, which put
        // the lease's marker BEFORE the fact rather than hundreds of lines
        // after it, and the line it looked for had since been wrapped over
        // three - so the assertion went on passing while measuring nothing.
        // What it always meant is here instead: the method that emits
        // `loop.attended` does not read whether a person is watching.
        var emitting = MethodContaining(source, "new LoopAttended");

        await Assert.That(emitting).DoesNotContain("lease.Attended")
            .Because("the two decisions are about different facts and must not become one: "
                   + "the LEASE's marker says somebody is watching, and the EXECUTOR having "
                   + "measured nothing says a person did the work. One flag driving both "
                   + "records an agent's work as a human's.");
    }

    /// <summary>The one method a string is inside, by its declaration.</summary>
    /// <remarks>
    /// <b>Crude on purpose, and it fails loudly rather than widening.</b> A
    /// slice that could not find its bounds would silently become "the whole
    /// file", which is how a scoped assertion turns into an unscoped one that
    /// still passes.
    /// </remarks>
    private static string MethodContaining(string source, string needle)
    {
        var at = source.IndexOf(needle, StringComparison.Ordinal);
        var opens = Regex.Matches(source[..at], @"\n    (private|public|internal) ");
        var start = opens.Count > 0
            ? opens[^1].Index
            : throw new InvalidOperationException($"no declaration precedes '{needle}'");

        var next = Regex.Match(source[(start + 8)..], @"\n    (private|public|internal) ");

        return next.Success
            ? source[start..(start + 8 + next.Index)]
            : throw new InvalidOperationException($"no declaration follows '{needle}'");
    }

    [Test]
    public async Task Nothing_on_this_path_asks_whether_it_is_remote()
    {
        // NO FORK. A remote hand-flight that took a different branch would be
        // FileRunnerStore.PathFor's defect arriving again - two paths that agree
        // today and drift the first time one is changed.
        var forks = Runner()
            .Where(l => Regex.IsMatch(l.Text, @"\b(isRemote|IsRemote|remotely|_remote)\b"))
            .Select(l => $"{l.File}:{l.Line}")
            .ToList();

        await Assert.That(forks).IsEmpty()
            .Because("one path, or the two drift. Found: " + string.Join(", ", forks));
    }

    [Test]
    public async Task The_scan_reads_the_runner_it_thinks_it_is_reading()
    {
        // The liveness half: every assertion above passes on an empty
        // enumeration, and a wrong directory is how that happens.
        await Assert.That(Runner().Any()).IsTrue();

        await Assert.That(Runner().Any(l => l.File == "RunnerLoop.cs")).IsTrue()
            .Because("the file every rule here is about has to be among the ones read.");

        await Assert.That(Regex.IsMatch("        using var attended = lease.Attended is true",
            @"\blease\.Attended\b")).IsTrue();
        await Assert.That(Regex.IsMatch("var x = invoked.Attended;", @"\blease\.Attended\b"))
            .IsFalse()
            .Because("the executor's answer is a different fact, and a scan that could not tell "
                   + "them apart would count it as a second reader of the lease.");
    }
}
