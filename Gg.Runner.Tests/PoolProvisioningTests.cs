using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// What provisioning a pool host may and may not do on the operator's behalf.
/// </summary>
/// <remarks>
/// <para>
/// <b>The README was a list of five things to type.</b> That is a way to stand
/// up one machine by hand and it is not a way to stand up a host: every step is
/// a place to forget, and the one worth forgetting is the one that matters —
/// <c>PoolHostTests</c> holds that only the proxy sees the socket, and nothing
/// held that the person following the steps did not simply hand it to the
/// runner as well.
/// </para>
/// <para>
/// <b>Nothing in this repository ever opens the Docker socket.</b> No source
/// file names <c>docker.sock</c>, sets <c>DOCKER_HOST</c>, or runs the
/// <c>docker</c> binary; the runner reaches Docker as HTTP to
/// <c>GG_POOL_ENDPOINT</c> and by no other route. So a <c>gg</c> user in the
/// <c>docker</c> group gains nothing it uses and regains everything the proxy
/// exists to take away — and the README asked for exactly that, in step one.
/// </para>
/// <para>
/// <b>A group membership is invisible in every test about the control.</b> The
/// proxy still refuses out-of-scope reaches, the runner still goes through it,
/// and the socket is still mounted in one place. The bypass is not in any
/// artefact's contents; it is in who may open a file none of them mention.
/// </para>
/// </remarks>
public class PoolProvisioningTests
{
    private static string Host(string file) =>
        Path.Combine(RepoRoot(), "deploy", "pool-host", file);

    private static string CloudInit() => File.ReadAllText(Host("cloud-init.yaml"));

    /// <summary>The lines that do something, which is every line but a comment.</summary>
    private static IEnumerable<string> Uncommented(string text) =>
        text.Split('\n').Where(l => !l.TrimStart().StartsWith('#'));

    [Test]
    public async Task A_host_can_be_provisioned_from_something_other_than_a_list_of_steps()
    {
        await Assert.That(File.Exists(Host("cloud-init.yaml"))).IsTrue()
            .Because("five numbered steps in a README is how one machine gets stood up by hand, "
                   + "and every step is a place for the next person to differ from this one.");
    }

    [Test]
    public async Task Provisioning_builds_nothing_and_therefore_carries_no_compiler()
    {
        // THIS REPLACES a test that asserted the opposite, and the replacement
        // is the point rather than a regression. `Provisioning_installs_what_an_
        // AOT_build_actually_needs` held that clang and zlib1g-dev were present
        // because the host AOT-published this CLI at boot - found on a real
        // machine, after a publish that compiled for minutes and died on the
        // link. A host that installs a released artefact does not link anything,
        // so the requirement is gone and so is the reason for the test.
        //
        // What replaces it is the stronger statement: nothing here builds.
        var text = CloudInit();
        var lines = Uncommented(text).ToList();

        var building = lines
            .Where(l => l.Contains("dotnet publish", StringComparison.Ordinal)
                     || l.Contains("dotnet build", StringComparison.Ordinal)
                     || l.Contains("git clone", StringComparison.Ordinal))
            .ToList();

        await Assert.That(building).IsEmpty()
            .Because("a host that clones and builds runs whatever was on the default branch the "
                   + "minute it booted - no tag, no commit - so two hosts provisioned an hour "
                   + "apart run different code and report the same version. Found: "
                   + string.Join(" | ", building));

        foreach (var compiler in (string[])["clang", "build-essential", "zlib1g-dev"])
        {
            await Assert.That(lines.Any(l => l.Contains(compiler, StringComparison.Ordinal))).IsFalse()
                .Because($"'{compiler}' is on this machine only to link an AOT build, and nothing "
                       + "here builds any more. Left in, it is several minutes of apt and a "
                       + "standing invitation to start compiling on hosts again.");
        }

        // AND git STAYS, which is the half that is easy to get wrong. It was
        // never here for the clone: Gg.Runner/Vcs/GitInvocation.cs and
        // EnvironmentSurvey both shell out to `git`, so a pool host without it
        // provisions cleanly and fails on the first flight.
        await Assert.That(lines.Any(l => l.Contains("git", StringComparison.Ordinal))).IsTrue()
            .Because("the runner invokes git to do its actual work. Removing it along with the "
                   + "clone is the obvious next tidy-up and it breaks every flight on the host.");
    }

    [Test]
    public async Task The_binary_arrives_from_a_feed_at_a_version_somebody_named()
    {
        // RULE 7: pinning is the default, and *latest* is a choice somebody
        // makes. `dotnet tool install` with no --version takes whatever was
        // pushed last, and what was pushed last is attacker-controlled - a
        // stolen API key produces a package nuget.org signs and every client
        // accepts. A fleet provisioning on a schedule would take it.
        var install = Uncommented(CloudInit())
            .Where(l => l.Contains("dotnet tool install", StringComparison.Ordinal))
            .ToList();

        await Assert.That(install).IsNotEmpty()
            .Because("the tool is how a host gets gg now - there is nothing else here that puts a "
                   + "binary on the machine.");

        await Assert.That(install.Where(l => l.Contains("--version", StringComparison.Ordinal)))
            .IsNotEmpty()
            .Because("without it the host takes whatever nuget.org offers at the moment it boots, "
                   + "which is the unpinned-main exposure again wearing a package's clothes.");
    }

    [Test]
    public async Task The_binary_lands_where_the_runners_user_may_execute_it_and_not_rewrite_it()
    {
        // THE PROTECTION THAT WAS FREE, AND IS NOT ANY MORE. Until now the
        // runner ran as `gg` against a root-owned /usr/local/bin/gg, so the
        // filesystem enforced "the runner cannot replace its own executable".
        // `dotnet tool install -g` installs into the HOME of whoever runs it -
        // user-writable by construction - and hands that back.
        //
        // --tool-path under a root-owned prefix is what keeps it. The runner
        // executes the shim and cannot write it, exactly as before.
        var install = Uncommented(CloudInit())
            .Where(l => l.Contains("dotnet tool install", StringComparison.Ordinal))
            .ToList();

        await Assert.That(install.Where(l => l.Contains("--tool-path", StringComparison.Ordinal)))
            .IsNotEmpty()
            .Because("a --tool-path install goes where provisioning says; a global one goes to "
                   + "$HOME/.dotnet/tools, which is the runner's own directory.");

        var global = install
            .Where(l => l.Contains(" -g ", StringComparison.Ordinal)
                     || l.Contains("--global", StringComparison.Ordinal))
            .ToList();

        await Assert.That(global).IsEmpty()
            .Because("that puts the executable inside the account the runner runs as, and the "
                   + "runner is treated as hostile. Found: " + string.Join(" | ", global));
    }

    [Test]
    public async Task Unpacking_a_release_cannot_restore_the_ownership_recorded_in_it()
    {
        // FOUND ON A REAL MACHINE, and it defeated the test below rather than
        // tripping it. Provisioning unpacks the pool-host bundle as root, and
        // GNU tar run by root defaults to --same-owner: it restores the NUMERIC
        // uid and gid recorded in the archive. The archive is built on a hosted
        // CI runner, where the files belong to uid 1001.
        //
        // On the host that was migrated by hand, uid 1001 is `gg`. All three
        // files landed owned by the account the runner runs as - including the
        // systemd unit, which says which binary runs and as whom, and the
        // proxy's allowlist, which says what the runner may reach. That is the
        // exposure this whole step closed, arriving by a route no `chown`
        // appears on, so Nothing_provisioning_leaves_behind_is_handed_to_the
        // _runners_user stayed green throughout.
        //
        // It is not a coincidence worth relying on either way: cloud images
        // number the first ordinary account 1000, and cloud-init creates `gg`
        // right after one, so 1001 lands on it by construction rather than by
        // luck.
        var extracting = Uncommented(CloudInit())
            .Where(l => Regex.IsMatch(l, @"\btar\b.*\b(xz|xzf|x)\b|\btar\s+x"))
            .ToList();

        await Assert.That(extracting).IsNotEmpty()
            .Because("provisioning unpacks nothing, so either the bundle is gone or this guard is "
                   + "watching a command that no longer exists.");

        var unsafeExtracts = extracting
            .Where(l => !l.Contains("--no-same-owner", StringComparison.Ordinal)
                     && !Regex.IsMatch(l, @"\btar\s+[a-z]*o[a-z]*\b"))
            .ToList();

        await Assert.That(unsafeExtracts).IsEmpty()
            .Because("root's tar restores the uid recorded in the archive, and the archive was "
                   + "built somewhere else entirely. Ownership of what provisioning unpacks must "
                   + "come from the machine unpacking it. Found: "
                   + string.Join(" | ", unsafeExtracts));
    }

    [Test]
    public async Task Nothing_provisioning_leaves_behind_is_handed_to_the_runners_user()
    {
        // THE ExecStart-REWRITE PATH, closed. `chown -R gg:gg /opt/gg` gave the
        // account the runner runs as ownership of the directory the systemd
        // unit was linked out of - so the runner's user could rewrite its own
        // ExecStart and User=, needing only a daemon-reload or a reboot to take
        // effect. A reboot happens.
        //
        // Root ownership of the binary never covered this: it is a second route
        // to the same place, through the file that decides what the binary is.
        var granting = Uncommented(CloudInit())
            .Where(l => l.Contains("chown", StringComparison.Ordinal)
                     || l.Contains("chgrp", StringComparison.Ordinal))
            .Where(l => l.Contains("gg", StringComparison.Ordinal))
            .ToList();

        await Assert.That(granting).IsEmpty()
            .Because("whatever the runner's user owns, it can rewrite - and the unit says which "
                   + "binary runs and as whom. Found: " + string.Join(" | ", granting));
    }

    [Test]
    public async Task The_two_versions_this_file_pins_are_the_same_version()
    {
        // TWO PINS, ONE HOST. The tool carries the binary and a release tag
        // carries the pool-host bundle the proxy is started from. Nothing
        // relates them: they are separate strings in separate commands, and a
        // bundle from one version against a binary from another comes up
        // looking entirely healthy until the halves of some contract disagree.
        var text = CloudInit();

        var tool = Regex.Match(text, @"--version\s+(\d+\.\d+\.\d+)");
        var bundle = Regex.Match(text, @"releases/download/v(\d+\.\d+\.\d+)/");

        await Assert.That(tool.Success).IsTrue()
            .Because("no pinned tool version was found at all, so there is nothing to compare.");
        await Assert.That(bundle.Success).IsTrue()
            .Because("no pinned release tag was found at all, so there is nothing to compare.");
        await Assert.That(tool.Groups[1].Value).IsEqualTo(bundle.Groups[1].Value)
            .Because("the binary and the artefacts it runs against ship as one version. Found "
                   + $"tool {tool.Groups[1].Value} against bundle {bundle.Groups[1].Value}.");
    }

    [Test]
    public async Task Everything_provisioning_downloads_from_this_repository_is_something_a_release_publishes()
    {
        // THE STALE-COMMENT DEFECT, AS A TEST. cloud-init said "There is no
        // published gg release asset to curl" for as long as there had been
        // one, and nothing noticed, because prose and provisioning are checked
        // by different people on different days. The inverse is worse and it is
        // what this catches: provisioning that downloads a file no workflow
        // produces fails on a machine, at boot, where nobody is watching.
        var wanted = Regex
            .Matches(CloudInit(), @"releases/download/[^/\s]+/([^\s'""]+)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await Assert.That(wanted).IsNotEmpty()
            .Because("provisioning that downloads nothing from this repository has gone back to "
                   + "building, and this test would pass by having nothing to check.");

        // FOUND BY NAME, not by path. The directory workflows live in is a
        // forge's name, and ProviderNeutralityTests forbids one in any .cs file
        // so that gg stays neutral about which forge speaks. Assembling the
        // path in pieces to get past that guard is the rewording its own doc
        // comment warns about; the file name is the part this test needs.
        var publishWorkflow = Directory
            .EnumerateFiles(RepoRoot(), "publish-cli.yml", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("publish-cli.yml was not found in this repository");

        var publish = File.ReadAllText(publishWorkflow);

        var unpublished = wanted
            .Where(name => !publish.Contains(name, StringComparison.Ordinal))
            .ToList();

        await Assert.That(unpublished).IsEmpty()
            .Because("publish-cli.yml is the only thing that attaches assets to a gg release, so "
                   + "an asset it never names is a 404 at first boot. Found: "
                   + string.Join(", ", unpublished));
    }

    [Test]
    public async Task The_runner_user_is_never_given_the_docker_group()
    {
        // THE FINDING. Nothing in this repository opens the socket - the runner
        // speaks HTTP to the proxy and nothing else - so this membership grants
        // no capability the runner uses, and hands back every one the proxy was
        // built to withhold. It would be invisible in all four PoolHostTests.
        // Comments are skipped, and not as a concession: a group cannot be
        // granted in one. A guard that fires on the sentence explaining why the
        // membership is absent teaches people to word around the guard.
        var granting = Uncommented(CloudInit())
            .Where(l => l.Contains("docker", StringComparison.OrdinalIgnoreCase)
                     && (l.Contains("groups", StringComparison.OrdinalIgnoreCase)
                      || l.Contains("usermod", StringComparison.OrdinalIgnoreCase)
                      || l.Contains("gpasswd", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(granting).IsEmpty()
            .Because("the runner reaches Docker only through the proxy, so this group grants it "
                   + "nothing it uses and returns everything the proxy withholds. Found: "
                   + string.Join(" | ", granting));
    }

    [Test]
    public async Task Provisioning_re_embeds_neither_the_allowlist_nor_the_proxy_service()
    {
        // compose.yaml says it in its own comment: the config is REFERENCED,
        // never copied, because a second copy of the allowlist is a second place
        // for one half of a contract to drift. Cloud-init is the obvious place
        // to paste both, and the paste would work on the day it was written.
        var text = CloudInit();

        await Assert.That(text.Contains("proxy_pass", StringComparison.Ordinal)).IsFalse()
            .Because("that is the proxy's config inlined here, and PoolPrefixTests compares the "
                   + "repository's copy against PoolNaming - not this one.");
        await Assert.That(text.Contains("image: nginx", StringComparison.Ordinal)).IsFalse()
            .Because("that is compose.yaml inlined here, and the socket mount travels with it - "
                   + "past the test that allows exactly one artefact to name the socket.");
    }

    [Test]
    public async Task Provisioning_stops_where_a_person_is_required()
    {
        // `gg runner maintain` refuses without a session, in its own words:
        // "Registering a runner is a person's action". Enabling the unit before
        // anybody has signed in buys a restart loop every ten seconds, and the
        // first real failure is indistinguishable from that noise.
        // What is asserted is what provisioning RUNS, not what it says. A first
        // formulation looked for the literal `enable --now`, which the list
        // form of a runcmd entry does not contain and the closing instructions
        // legitimately do - it would have passed for both wrong reasons.
        var starting = Uncommented(CloudInit())
            .Where(l => l.TrimStart().StartsWith("- [", StringComparison.Ordinal)
                     && l.Contains("gg-runner-maintain", StringComparison.Ordinal)
                     && (l.Contains("enable", StringComparison.Ordinal)
                      || l.Contains("start", StringComparison.Ordinal)))
            .ToList();

        await Assert.That(starting).IsEmpty()
            .Because("the runner cannot register until a person signs in on the machine, so "
                   + "starting it here is a unit that fail-loops until somebody does. Found: "
                   + string.Join(" | ", starting));
        await Assert.That(CloudInit().Contains("gg login", StringComparison.Ordinal)).IsTrue()
            .Because("the step provisioning cannot take is the one it must name, or the host is "
                   + "left looking finished and does nothing.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}
