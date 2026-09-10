using System.Diagnostics;
using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// The two refusals a person actually hits on the airspace verbs are
/// sentences, not stack traces.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT, AND IT IS THE ONE THIS FILE'S NEIGHBOUR ALREADY FIXED
/// ONCE.</b> <c>gg airspace pull</c> into a working copy with uncommitted
/// changes does not refuse — it CRASHES. An unhandled
/// <c>DirtyWorkingCopyException</c>, a stack trace, and exit 134, which is
/// SIGABRT and is what gg looks like when it breaks. <c>gg airspace apply</c>
/// over a file that sits where a document goes and does not parse does the
/// same with <c>EnvelopeRefusedException</c>.
/// </para>
/// <para>
/// <b>The emitter beside them already knows better, in writing.</b>
/// <c>EmitAsync</c> catches seven exception types, and the comment on the
/// first says <i>"AN ANSWER, AND IT USED TO BE A CRASH. Every refusal on this
/// path left as an unhandled InvalidOperationException - a stack trace and
/// exit 134."</i> The airspace verbs were routed through that emitter and
/// their two refusals were not added to it. <c>EnvelopeAsync</c>, one function
/// away, catches both.
/// </para>
/// <para>
/// <b>These are the two the design named as the ones people hit.</b> The
/// unreadable-file refusal names the paths because applying the rest
/// <i>"would land part of a changeset somebody meant as a whole"</i>, and the
/// dirty-tree refusal names the files so somebody can commit or discard them.
/// Both messages are good; neither reaches anybody.
/// </para>
/// <para>
/// <b>Asserted against the built binary, because nothing smaller has caught
/// any of this.</b> Three defects in one afternoon were each invisible to a
/// suite that was green: a tool that refused every call, a console reading a
/// cached file, and this. Every one of them appeared the moment somebody ran
/// the program. Neither of these two paths needs a network or a session — both
/// refusals are raised before the first request — so the walk is cheap and
/// deterministic.
/// </para>
/// </remarks>
public class AnAirspaceRefusalIsASentenceTests
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

    /// <summary>The gg this test run was built beside.</summary>
    /// <remarks>
    /// The configuration is taken from where this assembly is rather than
    /// assumed, so a Release run tests the Release binary and neither reaches
    /// for one that a previous build left behind.
    /// </remarks>
    private static string Binary()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("cannot tell Debug from Release here.");

        var at = Path.Combine(
            Root(), "Gg.Cli", "bin", configuration, "net10.0",
            OperatingSystem.IsWindows() ? "gg.exe" : "gg");

        if (!File.Exists(at))
        {
            throw new InvalidOperationException(
                $"'{at}' is not there, so this walk would run nothing and pass. That is a "
              + "broken walk, never a clean tree.");
        }

        return at;
    }

    private sealed record Run(int Code, string Said);

    private static Run Ran(string root, params string[] verb)
    {
        var start = new ProcessStartInfo(Binary())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var word in verb)
        {
            start.ArgumentList.Add(word);
        }

        // THE ROOT FORCED, so this reads the tree the test made rather than
        // whatever the machine running it happens to be configured for.
        start.Environment["GG_AIRSPACE"] = root;

        using var child = Process.Start(start)
            ?? throw new InvalidOperationException("gg did not start.");

        var said = child.StandardOutput.ReadToEndAsync();
        var complained = child.StandardError.ReadToEndAsync();

        if (!child.WaitForExit(TimeSpan.FromMinutes(1)))
        {
            child.Kill(entireProcessTree: true);
            throw new InvalidOperationException("gg did not finish, so it reached a network.");
        }

        return new Run(
            child.ExitCode,
            said.GetAwaiter().GetResult() + complained.GetAwaiter().GetResult());
    }

    private static string Somewhere(string what) =>
        Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), $"gg-{what}-{Guid.NewGuid():N}")).FullName;

    [Test]
    public async Task Pulling_over_uncommitted_changes_says_which_files()
    {
        var tree = Somewhere("dirty");
        try
        {
            Git.Run(tree, "init", "-q");
            Git.Run(tree, "config", "user.email", "airspace@example.invalid");
            Git.Run(tree, "config", "user.name", "the airspace walk");
            Directory.CreateDirectory(Path.Combine(tree, "airspace", "narrowings"));
            await File.WriteAllTextAsync(
                Path.Combine(tree, "airspace", "narrowings", "pci.yaml"),
                "obligations:\n  pci:\n    check: human\n    approver: an-auditor\n");

            var run = Ran(tree, "airspace", "pull");

            await Assert.That(run.Said).DoesNotContain("Unhandled exception", StringComparison.Ordinal)
                .Because("a refusal a person is meant to act on arrived as a crash. Said:\n"
                       + run.Said);

            await Assert.That(run.Code).IsNotEqualTo(0)
                .Because("nothing was pulled, and a zero would tell a script it was.");

            await Assert.That(run.Code).IsNotEqualTo(134)
                .Because("134 is SIGABRT, which is what gg looks like when it breaks rather "
                       + "than when it declines.");

            await Assert.That(run.Said).Contains("uncommitted", StringComparison.OrdinalIgnoreCase)
                .Because("the list of files is the actionable part and the reason this "
                       + "exception carries them. Said:\n" + run.Said);
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task Applying_over_an_unreadable_document_names_it()
    {
        var tree = Somewhere("unreadable");
        try
        {
            Directory.CreateDirectory(Path.Combine(tree, "airspace", "narrowings"));
            await File.WriteAllTextAsync(
                Path.Combine(tree, "airspace", "narrowings", "broken.yaml"),
                "this is not: [a document\n  it does not: parse\n");

            var run = Ran(tree, "airspace", "apply");

            await Assert.That(run.Said).DoesNotContain("Unhandled exception", StringComparison.Ordinal)
                .Because("applying refuses on an unreadable file so a partial changeset "
                       + "cannot land - and that refusal is the whole point of it. Said:\n"
                       + run.Said);

            await Assert.That(run.Code).IsNotEqualTo(134)
                .Because("134 is SIGABRT. A refusal is a decision, not a fault.");

            await Assert.That(run.Said).Contains("broken.yaml", StringComparison.Ordinal)
                .Because("naming the file is what makes the refusal actionable. Said:\n"
                       + run.Said);
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }
}
