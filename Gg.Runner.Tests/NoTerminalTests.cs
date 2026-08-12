using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// No PTY, no remote write, and no evaluation on this side.
/// </summary>
/// <remarks>
/// <para>
/// <b>The PTY is the bet the slice's schedule rests on.</b> Slice one removed
/// the PTY layer and called it the largest schedule risk; this slice assumed
/// headless invocation plus terminal-inherit would be enough and planned
/// around it. So it is asserted structurally rather than checked by whether
/// anything happened to work - a pseudo-terminal added quietly is the schedule
/// coming back without anybody deciding it should.
/// </para>
/// <para>
/// The other two are re-assertions, and both are worth re-making NOW: there is
/// an executor to tempt them for the first time. A write path arrives the day
/// somebody wants the agent to open a pull request, and an evaluation path
/// arrives the day somebody wants a verdict without a round trip.
/// </para>
/// </remarks>
public class NoTerminalTests
{
    /// <summary>
    /// A file's CODE, with its comments removed.
    /// </summary>
    /// <remarks>
    /// The rule is about allocating a terminal, not about mentioning one - and
    /// the place you most want to write the word is the comment explaining why
    /// there is none. Stripping comments keeps the scan blunt everywhere it
    /// matters, and the liveness half below asserts the distinction directly so
    /// this is a deliberate exclusion rather than a convenient one.
    /// </remarks>
    private static string CodeOf(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> RunnerSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory
            .EnumerateFiles(Path.Combine(root.FullName, "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }

    [Test]
    public async Task Nothing_in_the_runner_allocates_a_terminal()
    {
        var terminal = new Regex(
            @"\b(pty|Pty|PTY|ConPTY|openpty|forkpty|pseudoconsole|PseudoConsole|Terminal\.Gui)\b",
            RegexOptions.Compiled);

        var offenders = RunnerSources()
            .Where(f => terminal.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("headless invocation plus terminal-inherit is what this slice was scoped on. "
                   + "Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_executor_redirects_its_child_rather_than_handing_it_a_console()
    {
        // The positive half. "No PTY" is satisfied by an executor that does not
        // exist, so this asserts the one that does reads a pipe.
        var adapter = RunnerSources().Single(f => Path.GetFileName(f) == "ClaudeCodeExecutor.cs");
        var source = File.ReadAllText(adapter);

        await Assert.That(source).Contains("RedirectStandardOutput = true");
        await Assert.That(source).Contains("UseShellExecute = false")
            .Because("a shell would give the child whatever terminal this process has.");
    }

    [Test]
    public async Task The_scan_can_tell_a_terminal_from_an_ordinary_word()
    {
        // Liveness, and a guard against the pattern matching everything. Both
        // halves, because a regex that matched nothing and one that matched
        // every file would satisfy the assertion above equally.
        var terminal = new Regex(
            @"\b(pty|Pty|PTY|ConPTY|openpty|forkpty|pseudoconsole|PseudoConsole|Terminal\.Gui)\b",
            RegexOptions.Compiled);

        await Assert.That(terminal.IsMatch("var pty = openpty();")).IsTrue();
        await Assert.That(terminal.IsMatch("using var process = new Process();")).IsFalse();
        await Assert.That(RunnerSources().Any()).IsTrue();

        // The exclusion, asserted rather than assumed: a file that only talks
        // about a terminal is clean, and the same file with one line of code
        // is not.
        var explained = Path.GetTempFileName();
        try
        {
            File.WriteAllText(explained, "// there is no PTY here, deliberately\nvar x = 1;\n");
            await Assert.That(terminal.IsMatch(CodeOf(explained))).IsFalse();

            File.WriteAllText(explained, "// there is no PTY here\nvar h = openpty();\n");
            await Assert.That(terminal.IsMatch(CodeOf(explained))).IsTrue();
        }
        finally
        {
            File.Delete(explained);
        }
    }

    // ---- no write to a remote ----

    [Test]
    public async Task The_runner_has_no_path_that_writes_to_a_remote()
    {
        // The agent EDITS FILES - that is the job - and the edits go nowhere.
        // Write is a property of a declared destination, no envelope declares
        // one yet, and this is the structural half of proving it: there is no
        // code that could push even if something asked.
        var writes = new Regex(
            @"""push""|\bpush\b\s*\+|git\s+push|PushAsync|CreatePullRequest|OpenPullRequest",
            RegexOptions.Compiled);

        var offenders = RunnerSources()
            .Where(f => writes.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("no branch, no pull request, no push. Found: " + string.Join(", ", offenders));

        await Assert.That(writes.IsMatch("await git(\"push\", remote);")).IsTrue()
            .Because("the scan has to be able to see one.");
    }

    // ---- no evaluation on this side ----

    [Test]
    public async Task No_evaluation_path_exists_runner_side()
    {
        // Article IX, re-asserted now that there is an executor to tempt it.
        // The runner gathers, filters and reports; it never decides. Moving a
        // verdict here for latency will look reasonable exactly once, and it
        // removes the product's reason to exist - a customer can then patch
        // the runner and bypass governance entirely.
        var deciding = new Regex(
            @"ObligationEngine|ObligationVerdict|EvaluateObligation|\bVerdict\b|Satisfied|Violated",
            RegexOptions.Compiled);

        var offenders = RunnerSources()
            .Where(f => deciding.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the runner reports what it observed; the control plane decides what it means. "
                   + "Found: " + string.Join(", ", offenders));
    }
}
