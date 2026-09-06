using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// That the runner role cannot reach the update path at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>This used to be enforced by the filesystem, for free.</b> The resident
/// runner runs as <c>User=gg</c> against a root-owned <c>/usr/local/bin/gg</c>,
/// so the process that runs customer code could not write its own executable —
/// the OS holding it apart from the console, and from itself. The non-negotiable
/// is that <i>the runner is treated as hostile</i>.
/// </para>
/// <para>
/// <b>Moving to a .NET tool put that ownership in question</b>, and
/// provisioning answers it with <c>--tool-path</c> under a root-owned prefix.
/// But a control that depends on how somebody installed the binary is a control
/// that a different install removes silently, so the boundary is asserted in
/// code as well: nothing the runner role reaches may name the update path.
/// </para>
/// <para>
/// <b>Asserted over source text</b>, the same way <c>LiveStreamingTests</c>
/// bounds what a UI session may reach. It is the thing that fails when somebody
/// adds a version check to the claim loop because it was convenient.
/// </para>
/// </remarks>
public class UpdateBoundaryTests
{
    /// <summary>Everything the runner role is.</summary>
    /// <remarks>
    /// The whole project rather than a list of files, because a list is a thing
    /// to forget to add to and this boundary is about the role, not about the
    /// three files that happen to implement it today.
    /// </remarks>
    private static IEnumerable<string> RunnerSources() => Directory
        .EnumerateFiles(Path.Combine(RepoRoot(), "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Test]
    public async Task The_runner_role_names_nothing_that_could_update_this_binary()
    {
        // Rule 2. Not "does not call it today" - may not NAME it, so that the
        // next person wiring a convenience has to delete a test with a reason
        // in it rather than add a line.
        var forbidden = new (string What, Regex Pattern)[]
        {
            ("the update advice", new Regex(@"\bUpdateAdvice\b")),
            ("the install shape", new Regex(@"\bInstallShape\b|\bInstallKind\b")),
            ("a tool update", new Regex(@"dotnet\s+tool\s+(update|install)")),
        };

        var offenders = new List<string>();

        foreach (var file in RunnerSources())
        {
            var text = File.ReadAllText(file);

            foreach (var (what, pattern) in forbidden)
            {
                if (pattern.IsMatch(text))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {what}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("the runner is treated as hostile, and until this move the OS enforced that "
                   + "it could not replace what runs it. That guarantee is now an install "
                   + "detail, so the boundary lives here too. Found: "
                   + string.Join(" | ", offenders));
    }

    [Test]
    public async Task The_guard_would_notice_if_the_runner_reached_for_it()
    {
        // THE HALF THAT MATTERS. The test above is an absence, and an absence
        // check that cannot fail is worse than none - it reports a boundary
        // nobody is holding. These are the shapes a real reach takes.
        var patterns = new Regex[]
        {
            new(@"\bUpdateAdvice\b"),
            new(@"\bInstallShape\b|\bInstallKind\b"),
            new(@"dotnet\s+tool\s+(update|install)"),
        };

        foreach (var reach in (string[])
                 ["var advice = UpdateAdvice.For(shape, current);",
                  "if (InstallShape.Current.Kind == InstallKind.ToolPath)",
                  "Process.Start(\"dotnet tool update -g GlyphGuild.Gg.Cli\");",
                  "// just run dotnet tool install here"])
        {
            await Assert.That(patterns.Any(p => p.IsMatch(reach))).IsTrue()
                .Because($"'{reach}' is the runner reaching the update path and the guard did not "
                       + "see it.");
        }
    }

    [Test]
    public async Task The_runner_role_is_actually_where_this_looks()
    {
        // And that the scan has something to scan. A path typo makes every
        // assertion above pass over an empty sequence.
        await Assert.That(RunnerSources().Count()).IsGreaterThan(20)
            .Because("Gg.Runner is the runner role and it is not four files. An empty or tiny "
                   + "sequence here means the boundary is being asserted over nothing.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }
}
