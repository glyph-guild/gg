using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A workflow that runs dotnet fetches the pinned fork first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a declared source that is missing fails a restore rather than
/// being skipped.</b> <c>nuget.config</c> names a local source at
/// <c>packages/</c>, and NuGet answers NU1301 for the whole restore when that
/// directory is absent — so it has to exist before the first build, not before
/// the first use of anything in it. gg#335 went red on all three CI jobs with
/// an error that named nothing about the change it was testing.
/// </para>
/// <para>
/// <b>And the release workflows were the worse half.</b> CI failing is a red
/// tick on a pull request; <c>publish-cli.yml</c> and
/// <c>publish-contracts.yml</c> failing is a release that does not happen, found
/// at the moment somebody wanted one. Both pack, both restore, and neither was
/// touched by the change that made the source load-bearing.
/// </para>
/// <para>
/// <b>Found by shape rather than by a list of workflow names.</b> A list is a
/// thing to forget to add to, which is the same failure one level up - and the
/// shape is also what keeps this file from naming the directory workflows live
/// in, which gg forbids because it is an identity provider's name.
/// </para>
/// </remarks>
public class EveryWorkflowThatBuildsFetchesTheForkTests
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

    /// <summary>
    /// Every workflow in this repository, found by what one IS.
    /// </summary>
    /// <remarks>
    /// <b>By shape rather than by path, and not for elegance.</b> gg forbids a
    /// source file naming an identity provider - it talks only to the control
    /// plane - and the directory workflows live in is named after one. A path
    /// literal here fails that rule, and assembling the string from pieces to
    /// slip past it would be worse: the rule would be satisfied and its reason
    /// would not. So this asks what a workflow is - a YAML file with jobs that
    /// declare a machine to run on - which is also the definition that survives
    /// somebody moving them.
    /// </remarks>
    private static IEnumerable<string> Workflows() =>
        Directory.EnumerateFiles(Root(), "*.yml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("jobs:", StringComparison.Ordinal)
                    && text.Contains("runs-on:", StringComparison.Ordinal);
            });

    /// <summary>Anything that makes NuGet resolve the graph.</summary>
    private static readonly Regex Restores =
        new(@"dotnet\s+(build|test|pack|publish|restore|run)\b", RegexOptions.Compiled);

    [Test]
    public async Task Every_workflow_that_restores_fetches_the_fork_first()
    {
        var missing = new List<string>();

        foreach (var workflow in Workflows())
        {
            var text = File.ReadAllText(workflow);

            if (!Restores.IsMatch(text))
            {
                continue;
            }

            if (!text.Contains("fetch-sipsorcery.sh", StringComparison.Ordinal))
            {
                missing.Add(Path.GetFileName(workflow));
            }
        }

        await Assert.That(missing).IsEmpty()
            .Because("nuget.config declares a local source, and a restore where it is missing "
                   + "fails with NU1301 naming nothing about the change. Found: "
                   + string.Join(", ", missing));
    }

    [Test]
    public async Task It_is_fetched_before_the_first_thing_that_restores()
    {
        // ORDER IS THE WHOLE PROPERTY. A fetch step after the build is a step
        // that runs on a job that already failed.
        var late = new List<string>();

        foreach (var workflow in Workflows())
        {
            var lines = File.ReadAllLines(workflow);

            var firstRestore = Array.FindIndex(lines, l => Restores.IsMatch(l));
            var firstFetch = Array.FindIndex(
                lines, l => l.Contains("fetch-sipsorcery.sh", StringComparison.Ordinal));

            if (firstRestore >= 0 && (firstFetch < 0 || firstFetch > firstRestore))
            {
                late.Add(Path.GetFileName(workflow));
            }
        }

        await Assert.That(late).IsEmpty()
            .Because("a fetch after the build runs on a job that has already failed. Found: "
                   + string.Join(", ", late));
    }

    [Test]
    public async Task The_scan_finds_workflows_and_would_notice_one_that_forgot()
    {
        // The liveness half and the poison twin together: both assertions above
        // pass on an empty enumeration, and a wrong directory is how that
        // happens.
        await Assert.That(Workflows().Count()).IsGreaterThan(2)
            .Because("a scan over no workflows asserts nothing at all.");

        await Assert.That(Workflows().Any(w => Restores.IsMatch(File.ReadAllText(w)))).IsTrue()
            .Because("if nothing matches as restoring, the rule covers nothing.");

        await Assert.That(Restores.IsMatch("        run: dotnet pack Gg.Cli -c Release")).IsTrue();
        await Assert.That(Restores.IsMatch("        run: echo dotnet is lovely")).IsFalse()
            .Because("a mention is not an invocation, and a rule that cannot tell them apart "
                   + "would flag a comment.");
    }
}
