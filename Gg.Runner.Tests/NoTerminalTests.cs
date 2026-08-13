using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// No PTY, no evaluation on this side, and write only where it was declared.
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
/// <b>The write assertion has been amended, not deleted.</b> Slice one said the
/// runner has no path that writes to a remote; that day arrived, somebody did
/// want the agent to open a pull request, and the property that mattered was
/// never the absence of the word. It was that nothing can push without something
/// declared changing. So the scan still runs and now names the files write is
/// allowed to live in - a second path is caught exactly as before.
/// </para>
/// <para>
/// The evaluation assertion is unchanged and is not negotiable in the same way:
/// a verdict computed here removes the product's reason to exist.
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

    // ---- write exists now, and only where it was declared ----

    /// <summary>
    /// Slice one asserted the runner has no path that writes to a remote. It has
    /// one now, and this is that assertion AMENDED rather than deleted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original test's value was never "the string push appears nowhere" -
    /// it was <b>nothing can push, and you would have to change something
    /// declared to make it able to</b>. That property survives, narrowed: write
    /// lives in a named set of files, and a second path anywhere else in this
    /// project still fails the build exactly as it did before.
    /// </para>
    /// <para>
    /// The <i>files</i> are listed rather than a count, because a count grows
    /// silently and a name has to be typed by somebody who then has to explain
    /// it in a diff.
    /// </para>
    /// </remarks>
    [Test]
    public async Task The_only_write_path_in_the_runner_is_the_one_that_was_declared()
    {
        // INVOKING a write, which is not the same as naming the method that does.
        // `PushAsync` was in this pattern and was removed deliberately when the port
        // split in two: it is now the legitimate name of a port method, so it appears
        // at the declaration and at the one call site by design. Leaving it here would
        // have meant allow-listing RunnerLoop.cs for a write path - which is what this
        // scan exists to refuse - so the pattern narrowed to invocations, and the test
        // below pins the three places the NAME may appear.
        var writes = new Regex(
            @"""push""|\bpush\b\s*\+|git\s+push|CreatePullRequest|OpenPullRequest|pulls",
            RegexOptions.Compiled);

        var offenders = RunnerSources()
            .Where(f => writes.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEquivalentTo((string[])
            [
                // The plan. Checkable without a remote, and it cannot force.
                "GitInvocation.cs",

                // The adapter behind IDestinationAdapter, which is the escalation
                // itself: a port that did not exist in slice one.
                "HttpsDestinationAdapter.cs",
            ])
            .Because("write arrived as a declared destination and nowhere else. A path here that is "
                   + "not one of these is the thing this test has always been for. Found: "
                   + string.Join(", ", offenders));

        await Assert.That(writes.IsMatch("await git(\"push\", remote);")).IsTrue()
            .Because("the scan has to be able to see one.");
    }

    [Test]
    public async Task The_push_port_is_named_in_exactly_three_places()
    {
        // The compensating assertion for narrowing the pattern above, and it is
        // stricter than what it replaced: the port method may be DECLARED once,
        // IMPLEMENTED once, and CALLED once. A fourth file naming it is a second
        // caller, which is how a write path appears with no new invocation anywhere -
        // and the old scan could not have told the difference.
        var naming = new Regex("PushAsync", RegexOptions.Compiled);

        var files = RunnerSources()
            .Where(f => naming.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(files).IsEquivalentTo((string[])
            [
                // Declared.
                "DestinationPort.cs",

                // Implemented, and the only file that invokes git push.
                "HttpsDestinationAdapter.cs",

                // Called, once, on a permission the control plane granted.
                "RunnerLoop.cs",
            ])
            .Because("found: " + string.Join(", ", files));

        await Assert.That(naming.IsMatch("await adapter.PushAsync(request);")).IsTrue()
            .Because("and the scan can see a call, so the list above means something.");
    }

    [Test]
    public async Task The_loop_never_pushes_and_only_carries_out_a_decision()
    {
        // Where the amendment could rot. The adapter is allowed to push; the
        // loop is what decides whether to call it, and a loop that shelled out
        // to git itself would be a second write path wearing the first one's
        // permission.
        var loop = RunnerSources().Single(f => Path.GetFileName(f) == "RunnerLoop.cs");
        var code = CodeOf(loop);

        foreach (var direct in (string[])["git ", "GitInvocation", "\"push\"", "HttpClient"])
        {
            await Assert.That(code).DoesNotContain(direct)
                .Because($"'{direct}' in the loop would be a write path that never passed through "
                       + "the port where writing was declared.");
        }

        await Assert.That(code).Contains("IDestinationAdapter")
            .Because("it reaches a remote only through the port, or not at all.");
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
