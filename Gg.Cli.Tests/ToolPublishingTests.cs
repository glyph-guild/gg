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
    /// <summary>Every workflow in this repository, found rather than named.</summary>
    /// <remarks>
    /// <b>The directory these live in is a forge's name</b>, and
    /// <c>ProviderNeutralityTests</c> forbids one in any <c>.cs</c> file — for a
    /// good reason that has nothing to do with this test: gg stays
    /// provider-neutral so a second adapter ships without the binary changing.
    /// Spelling the path in pieces to slip past the guard is the "teaches people
    /// to reword" failure that guard's own doc comment warns about, so the
    /// workflow is located by the only part of it this test actually cares
    /// about — its file name.
    /// </remarks>
    private static IEnumerable<string> WorkflowFiles() => Directory
        .EnumerateFiles(RepoRoot(), "*.yml", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string WorkflowPath(string name) =>
        WorkflowFiles().FirstOrDefault(f => Path.GetFileName(f) == name)
        ?? throw new InvalidOperationException($"no workflow named {name} was found in this repository");

    private static string Workflow(string name) => File.ReadAllText(WorkflowPath(name));

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
    public async Task There_is_no_long_lived_key_that_can_publish_this_package()
    {
        // THE TRUST ROOT, REMOVED RATHER THAN GUARDED. A nuget.org API key can
        // push ANY version under this id, and repository signing does not help:
        // it proves a package came through nuget.org's pipeline, not that
        // glyph-guild sent it, so a stolen key produces a package nuget.org
        // signs and every client accepts.
        //
        // Trusted publishing means there is no such key to steal. The workflow
        // presents an OIDC token, nuget.org checks it against a policy naming
        // this repository and this workflow FILE, and returns an API key good
        // for one hour and one push. A leaked workflow log is worth nothing an
        // hour later, and there is no secret to rotate.
        //
        // ASSERTED AS A PRESENCE FIRST. "No long-lived key is referenced" is
        // true of a repository that publishes nothing at all, so the exchange
        // has to be shown to exist before its absence means anything.
        var keyed = File.ReadAllLines(WorkflowPath("publish-cli.yml"))
            .Where(l => l.Contains("--api-key", StringComparison.Ordinal))
            .ToList();

        await Assert.That(keyed).IsNotEmpty()
            .Because("nuget.org refuses an anonymous push, so a CLI publish workflow with no key "
                   + "at all is one that never publishes - and this guard would pass forever.");

        await Assert.That(keyed.Where(l => l.Contains("steps.login.outputs.NUGET_API_KEY", StringComparison.Ordinal)))
            .IsNotEmpty()
            .Because("the key has to come from the token exchange rather than from storage, or "
                   + "the long-lived credential is simply back under another name.");

        // AND NO STORED ONE ANYWHERE, across every workflow. NUGET_USER is
        // deliberately not matched: it is a nuget.org profile name, not a
        // credential, and the worst a leak of it costs is a username.
        var stored = WorkflowFiles()
            .SelectMany(f => File.ReadAllLines(f).Select(l => (file: Path.GetFileName(f), line: l)))
            .Where(x => Regex.IsMatch(x.line, @"secrets\.[A-Z_]*NUGET[A-Z_]*(KEY|TOKEN)"))
            .Select(x => $"{x.file}: {x.line.Trim()}")
            .ToList();

        await Assert.That(stored).IsEmpty()
            .Because("a stored publishing key is the one credential in either repository that "
                   + "would let somebody else ship gg to a fleet, and trusted publishing exists "
                   + "so that none has to be kept. Found: " + string.Join(" | ", stored));
    }

    [Test]
    public async Task The_job_that_holds_the_OIDC_token_grants_it_and_pins_what_receives_it()
    {
        // TWO PROPERTIES OF ONE JOB, and both are invisible in a diff that
        // looks like tidying.
        //
        // id-token: write is what lets the runner mint the OIDC token at all.
        // A `permissions:` block REPLACES the default set rather than adding to
        // it, so trimming this job's permissions - which reads like good
        // hygiene - stops publishing entirely, at the one step that only runs
        // on main after a version bump.
        //
        // And the action receiving that token is third-party code with the
        // ability to exchange it. A movable tag means whoever controls the tag
        // chooses what runs in a job holding a credential for nuget.org. This
        // is the one place in this repository where a SHA is worth the
        // awkwardness.
        var job = NugetJob();

        await Assert.That(job).Contains("id-token: write")
            .Because("without it the OIDC request fails at run time on main, long after whoever "
                   + "trimmed the permissions block has stopped looking.");

        var uses = job
            .Split('\n')
            .Where(l => l.Contains("uses:", StringComparison.Ordinal))
            .ToList();

        await Assert.That(uses).IsNotEmpty()
            .Because("no action is used in this job at all, so there is nothing to pin and the "
                   + "token exchange is not happening the way this test describes.");

        var unpinned = uses
            .Where(l => !Regex.IsMatch(l, @"@[0-9a-f]{40}\b"))
            .ToList();

        await Assert.That(unpinned).IsEmpty()
            .Because("a tag can be moved to point at anything; this job can trade its token for "
                   + "the right to publish gg. Found: " + string.Join(" | ", unpinned));
    }

    /// <summary>The block of publish-cli.yml that defines the nuget job.</summary>
    /// <remarks>
    /// Scoped rather than searched whole, because "the file mentions
    /// <c>id-token: write</c> somewhere" is satisfied by a permission granted to
    /// a job that does not need it while the one that does goes without.
    /// </remarks>
    private static string NugetJob()
    {
        var lines = File.ReadAllLines(WorkflowPath("publish-cli.yml"));
        var start = Array.FindIndex(lines, l => l.StartsWith("  nuget:", StringComparison.Ordinal));

        if (start < 0)
        {
            throw new InvalidOperationException("publish-cli.yml defines no job named 'nuget'");
        }

        var after = lines
            .Skip(start + 1)
            .TakeWhile(l => !Regex.IsMatch(l, @"^  [A-Za-z0-9_-]+:\s*$"));

        return string.Join('\n', after);
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
        var lines = File.ReadAllLines(WorkflowPath("ci.yml"));

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
