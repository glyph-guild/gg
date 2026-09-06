using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// The channel a person installs from, and what CI proves about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A release asset is not a feed.</b> The <c>.nupkg</c> has been attached to
/// every release since the tool was packable, and installing it still means
/// downloading a file first because <c>--add-source</c> wants a directory.
/// nuget.org is the channel that makes <c>dotnet tool install -g</c> and
/// <c>dotnet tool update -g</c> mean something — and the update path is the
/// whole point: <c>gg</c> deliberately builds no updater, so <c>dotnet</c> has
/// to be able to do it.
/// </para>
/// <para>
/// <b>The tool package is IL and the binary is not.</b> <c>publish-cli.yml</c>
/// packs with <c>-p:PublishAot=false</c> while CI's <c>aot</c> job proves the
/// NATIVE build runs. Those are two different artefacts, and the moment the
/// tool becomes the channel most people install through, the thing CI
/// smoke-tests stops being the thing people run. So something has to install
/// the package and invoke the command.
/// </para>
/// <para>
/// <b>And a job outside the gate proves nothing.</b> Branch protection points
/// at one check named <c>CI</c>, which passes or fails on its <c>needs</c>. A
/// job added to the file but not to that list runs, goes red, and merges — the
/// most expensive kind of test, because it looks like coverage.
/// </para>
/// </remarks>
public class ToolPublishingTests
{
    private static string Workflow(string name) =>
        File.ReadAllText(RepoFile(".github", "workflows", name));

    [Test]
    public async Task The_tool_package_is_pushed_to_a_feed_and_not_only_attached_to_a_release()
    {
        // `dotnet tool install --add-source` takes a DIRECTORY, not a URL, so a
        // .nupkg on a releases page is a download-then-install and never a
        // `dotnet tool update`. Since gg replaces no binary of its own, the
        // update path has to exist somewhere, and this is where.
        var publish = Workflow("publish-cli.yml");

        await Assert.That(publish).Contains("dotnet nuget push")
            .Because("attaching the package to a release makes it obtainable and does not make it "
                   + "installable by id, which is what `dotnet tool update -g` needs.");
        await Assert.That(publish).Contains("https://api.nuget.org/v3/index.json")
            .Because("the source has to be named. `dotnet nuget push` with no --source falls back "
                   + "to whatever nuget.config supplies, which on a hosted runner is not stated "
                   + "anywhere a reader of this file can see.");
    }

    [Test]
    public async Task The_key_that_can_publish_is_never_written_down()
    {
        // An API key on nuget.org can push ANY version of this package under
        // this id, and the plan that chose nuget.org says so in as many words:
        // repository signing proves the package came through nuget.org's
        // pipeline, not that glyph-guild pushed it. So the key is the trust
        // root, and a workflow file is public.
        // ASSERTED AS A PRESENCE FIRST, and not as taste. "No line carries a
        // literal key" is true of a repository that pushes nothing at all, so
        // on its own it is a guard that passes loudest exactly when there is
        // nothing to guard.
        //
        // AND IT IS SCOPED TO THIS FILE, which the first formulation was not:
        // publish-contracts.yml has pushed to GitHub Packages with an --api-key
        // since long before this, so a repository-wide search for the flag was
        // already satisfied by a different package going to a different
        // registry. It passed on the red commit, which is how it was found.
        var keyed = File.ReadAllLines(RepoFile(".github", "workflows", "publish-cli.yml"))
            .Where(l => l.Contains("--api-key", StringComparison.Ordinal))
            .ToList();

        await Assert.That(keyed).IsNotEmpty()
            .Because("nuget.org refuses an anonymous push, so a CLI publish workflow with no key "
                   + "at all is one that never publishes - and this guard would pass forever.");

        var literals = keyed
            .Where(l => !l.Contains("${{", StringComparison.Ordinal)
                     && !l.Contains("$NUGET_API_KEY", StringComparison.Ordinal))
            .ToList();

        await Assert.That(literals).IsEmpty()
            .Because("this repository is public, and a key that can push under this package id is "
                   + "the one credential that would let somebody else ship gg. Found: "
                   + string.Join(" | ", literals));
    }

    [Test]
    public async Task CI_installs_the_package_people_install_and_runs_the_command_they_type()
    {
        // THE ARTEFACT SWAP. Until now the thing CI proved runs - the AOT
        // binary - was also the thing a pool host ran. Moving provisioning to
        // the tool makes the IL package the common install while the `aot` job
        // keeps proving something else. Both still ship; only one was checked.
        var ci = Workflow("ci.yml");

        await Assert.That(ci).Contains("dotnet tool install")
            .Because("packing proves the package builds. Installing it and running the command is "
                   + "the only thing that proves the shape people actually get works at all.");
        await Assert.That(ci).Contains("--tool-path")
            .Because("a global install writes to the runner's HOME and leans on PATH; a tool-path "
                   + "install is invoked by the path it was put at, so a green step means the "
                   + "shim ran rather than that some other gg was found first.");
    }

    [Test]
    public async Task Every_job_in_CI_is_one_the_gate_waits_for()
    {
        // Branch protection points at a single check named `CI`, and that job
        // passes or fails on `needs`. A job absent from the list still runs and
        // still goes red on the Actions tab - and merges anyway. This holds the
        // list to the file rather than to whoever last added a job.
        var lines = File.ReadAllLines(RepoFile(".github", "workflows", "ci.yml"));

        var jobs = lines
            .SkipWhile(l => !l.StartsWith("jobs:", StringComparison.Ordinal))
            .Select(l => Regex.Match(l, "^  ([A-Za-z0-9_-]+):\\s*$"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Where(name => !string.Equals(name, "ci", StringComparison.Ordinal))
            .ToList();

        var needs = lines.FirstOrDefault(l => l.TrimStart().StartsWith("needs:", StringComparison.Ordinal))
            ?? string.Empty;

        var ungated = jobs.Where(j => !needs.Contains(j, StringComparison.Ordinal)).ToList();

        await Assert.That(ungated).IsEmpty()
            .Because("the gate is one check and it waits only on what it names, so a job it does "
                   + "not name is a red build that merges. Found: " + string.Join(", ", ungated)
                   + " (needs line: " + needs.Trim() + ")");
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory)
            : Path.Combine([dir.FullName, .. parts]);
    }
}
