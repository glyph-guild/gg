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
    public async Task Provisioning_installs_what_an_AOT_build_actually_needs()
    {
        // FOUND ON A REAL MACHINE, not here. cloud-init installs the .NET SDK
        // and then publishes this CLI - which is AOT - and NativeAOT shells out
        // to a platform linker. Without clang the publish dies at the last step
        // with "Platform linker ('clang' or 'gcc') not found in PATH", after
        // several minutes of successful compilation.
        //
        // CI could never catch it: GitHub's ubuntu image ships build-essential
        // and clang preinstalled, so the aot job proves the code AOT-publishes
        // and proves nothing about a machine provisioned from this file.
        var packages = CloudInit();

        await Assert.That(packages).Contains("clang")
            .Because("NativeAOT invokes a platform linker, and a cloud image has none. The build "
                   + "fails at the very end, long after everything looks like it is working.");
        await Assert.That(packages).Contains("zlib1g-dev")
            .Because("the ILCompiler links against zlib; without the headers the link fails for a "
                   + "second reason once the first is fixed.");
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
