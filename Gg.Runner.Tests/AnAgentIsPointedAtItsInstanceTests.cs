using System.Diagnostics;
using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// An agent working in a hosted flight is pointed at the daemon its flight was
/// granted, and a flight hosted nowhere is pointed at nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 1, and it exists because the whole chain above it was
/// built and connected to nothing.</b> The claim takes an instance, the lease
/// carries its name (S54.5-03), and <c>EnvironmentNaming.SocketFor</c> turns that
/// name into a socket — and before this, `SocketFor` had no production caller and
/// `DOCKER_HOST` appeared nowhere in either repository. Three members of this
/// slice family have now shipped declared and unread; this is the one that ends
/// it.
/// </para>
/// <para>
/// <b>The environment, because that is where a Docker client looks.</b> Every
/// tool an agent might reach for — the CLI, an AppHost, a compose file — reads
/// <c>DOCKER_HOST</c>. Handing the address any other way would mean teaching
/// each of them separately.
/// </para>
/// <para>
/// <b>Placed BEFORE the envelope's variables, so a document cannot replace
/// it.</b> <c>PlaceVariables</c> skips a name already present, for the reason it
/// states: <i>"a value from a git-tracked document must not be able to replace a
/// credential or a scratch directory."</i> A daemon address is the same kind of
/// thing — the platform granted this instance and a document naming another
/// would point the flight at somebody else's stack.
/// </para>
/// <para>
/// <b>Absent, never empty.</b> Every flight in the field is hosted nowhere, and
/// an empty <c>DOCKER_HOST</c> is not the same as an unset one: clients read ""
/// as "use the default" on some platforms and as a malformed address on others,
/// and neither is what "this flight hosts nothing" means.
/// </para>
/// </remarks>
public class AnAgentIsPointedAtItsInstanceTests
{
    private const string DockerHost = "DOCKER_HOST";

    private static ExecutorRequest Asking(
        string? instance, params (string Name, string Value)[] variables) => new()
    {
        WorkingDirectory = "/tmp/tree",
        Instance = instance,
        Variables = [.. variables.Select(v => new EnvelopeVariable
        {
            Name = v.Name,
            Value = v.Value,
        })],
    };

    [Test]
    public async Task A_hosted_flight_points_the_agent_at_its_own_daemon()
    {
        var info = new ProcessStartInfo();

        ClaudeCodeExecutor.PlaceInstance(info, Asking("gg-env-1"));

        await Assert.That(info.Environment[DockerHost])
            .IsEqualTo(EnvironmentNaming.SocketFor("gg-env-1"))
            .Because("compared against the naming rather than a literal, because the runbook "
                   + "and this binary hold the same convention and a second spelling here is "
                   + "a stack that comes up where nothing looks for it.");
    }

    [Test]
    public async Task A_flight_hosted_nowhere_is_pointed_at_nothing()
    {
        // NOT AN EMPTY STRING. A client reads "" as "use the default" on some
        // platforms and as a malformed address on others, and neither is what
        // "this flight hosts nothing" means.
        var info = new ProcessStartInfo();
        var before = info.Environment.Count;

        ClaudeCodeExecutor.PlaceInstance(info, Asking(instance: null));

        await Assert.That(info.Environment.ContainsKey(DockerHost)).IsFalse();
        await Assert.That(info.Environment.Count).IsEqualTo(before)
            .Because("every flight in the field is hosted nowhere, and a runner must behave "
                   + "for them exactly as it did before this existed.");
    }

    [Test]
    public async Task A_blank_instance_is_pointed_at_nothing_either()
    {
        // The same reason EnvironmentNaming refuses one: an empty name derives a
        // path under the environment root that exists, so the failure would be
        // an agent talking to the wrong daemon rather than to none.
        var info = new ProcessStartInfo();

        ClaudeCodeExecutor.PlaceInstance(info, Asking("   "));

        await Assert.That(info.Environment.ContainsKey(DockerHost)).IsFalse();
    }

    [Test]
    public async Task A_document_cannot_replace_the_address_it_was_granted()
    {
        // THE ORDER IS THE RULE. PlaceVariables skips a name already present, so
        // placing the granted address first is what makes a document's own
        // DOCKER_HOST inert. A document that could set it would point a flight
        // at another tenant's stack, or at the host's own daemon.
        var info = new ProcessStartInfo();
        var request = Asking("gg-env-1", (DockerHost, "unix:///var/run/docker.sock"));

        ClaudeCodeExecutor.PlaceInstance(info, request);
        ClaudeCodeExecutor.PlaceVariables(info, request);

        await Assert.That(info.Environment[DockerHost])
            .IsEqualTo(EnvironmentNaming.SocketFor("gg-env-1"));
    }

    [Test]
    public async Task An_unhosted_flight_may_still_declare_the_variable_itself()
    {
        // AND THAT IS DELIBERATE. Nothing was granted, so there is no platform
        // answer to protect: a tenant pointing its own agent somewhere is what
        // `variables:` is for, and ADR-0026 already says an envelope may set one
        // and never a secret.
        var info = new ProcessStartInfo();
        var request = Asking(instance: null, (DockerHost, "tcp://127.0.0.1:2375"));

        ClaudeCodeExecutor.PlaceInstance(info, request);
        ClaudeCodeExecutor.PlaceVariables(info, request);

        await Assert.That(info.Environment[DockerHost]).IsEqualTo("tcp://127.0.0.1:2375");
    }
}
