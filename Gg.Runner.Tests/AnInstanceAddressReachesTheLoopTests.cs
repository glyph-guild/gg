using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A loop is told which instance is hosting its stack, and derives the daemon
/// address from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The NAME crosses, never the path.</b> An instance is a UNIX user on a pool
/// host (ADR-0034), and the socket its daemon listens on is
/// <c>/srv/env/&lt;instance&gt;/run/docker.sock</c> — deployment knowledge, which
/// the control plane does not hold. It holds references rather than values, so
/// what rides the lease is <c>gg-env-1</c> and this side builds the rest.
/// </para>
/// <para>
/// <b>Held on both sides, exactly as the pool prefix is.</b>
/// <c>PoolNaming.ReservedPrefix</c> is <i>"a contract with the runner, held on
/// both sides"</i> and a test compares it against the proxy's own config. This is
/// that pattern for a second convention: <c>EnvironmentNaming</c> is one half and
/// <c>deploy/pool-host/environments.md</c> is the other, and the test below
/// reads the runbook rather than trusting that somebody updated it.
/// </para>
/// <para>
/// <b>Absent means hosted nowhere, and must stay cheap.</b> Every flight today
/// is hosted nowhere. A loop that was handed an empty string instead of nothing
/// would build <c>/srv/env//run/docker.sock</c> and point an AppHost at a
/// directory that exists — which is the shape of failure this whole slice keeps
/// finding, a thing that comes up and serves nobody.
/// </para>
/// </remarks>
public class AnInstanceAddressReachesTheLoopTests
{
    [Test]
    public async Task A_loop_carries_the_instance_that_hosts_it()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("instance")
            .Because("the grant is decided control-plane-side and the loop is the only thing "
                   + "that needs it, so it rides the lease a runner already collects rather "
                   + "than a route nobody else calls.");
    }

    [Test]
    public async Task The_address_is_derived_from_the_name_and_nothing_else()
    {
        await Assert.That(EnvironmentNaming.SocketFor("gg-env-1"))
            .IsEqualTo("unix:///srv/env/gg-env-1/run/docker.sock");
    }

    [Test]
    public async Task A_loop_hosted_nowhere_is_given_no_address()
    {
        // NULL RATHER THAN A PATH BUILT FROM NOTHING. `/srv/env//run/docker.sock`
        // resolves to a directory that exists, so the failure would be an
        // AppHost pointed at the wrong daemon rather than an error.
        await Assert.That(EnvironmentNaming.SocketFor(null)).IsNull();
        await Assert.That(EnvironmentNaming.SocketFor("   ")).IsNull();
    }

    [Test]
    public async Task The_runbook_and_this_binary_agree_on_where_the_socket_is()
    {
        // BOTH HALVES OF ONE CONVENTION, and the runbook is the half a person
        // follows by hand. PoolNaming's prefix earned this check the day the
        // proxy named the walk's pool and every other pool was refused at
        // create; a path that drifts here fails the same way, silently.
        var runbook = File.ReadAllText(Path.Combine(
            RepoRoot(), "deploy", "pool-host", "environments.md"));

        await Assert.That(runbook).Contains(EnvironmentNaming.SocketFor("gg-env-1")![7..])
            .Because("the runbook tells somebody to make the socket there by hand, and this "
                   + "binary is what connects to it. Two spellings of one path is a stack that "
                   + "comes up where nothing looks for it.");
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
