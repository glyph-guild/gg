using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A hook talks to the daemon this flight was granted, not the machine's own.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.2-01, and the criterion caught a real defect.</b> The agent has been
/// pointed at its instance since ADR-0033 — <c>ClaudeCodeExecutor.PlaceInstance</c>
/// puts the socket in its environment — and the HOOK was not. So a
/// <c>prepare</c> or an <c>attach</c> would have reached whatever ambient
/// <c>DOCKER_HOST</c> the pool host happened to have.
/// </para>
/// <para>
/// <b>Which is the hazard <see cref="DockerInstanceDaemon"/> is built to avoid,
/// in its own words:</b> an unscoped client risks <i>"quietly emptying whatever
/// an ambient DOCKER_HOST pointed at — which on a pool host would be every
/// member on the machine."</i> A bring-up hook is a Docker client like any
/// other, so it needs the same scoping the reclaim path was given.
/// </para>
/// <para>
/// <b>The environment, because that is where a Docker client looks.</b> The CLI,
/// an AppHost and a compose file all read <c>DOCKER_HOST</c>; handing the
/// address any other way means teaching each of them separately — which is the
/// executor's own reasoning, reused rather than restated.
/// </para>
/// <para>
/// <b>And it is the same derivation, not a second one.</b>
/// <c>EnvironmentNaming.SocketFor</c> is the one place that convention lives on
/// this side; a hook given an address derived anywhere else could point at a
/// different socket from the agent working beside it in the same flight.
/// </para>
/// </remarks>
public class AHookRunsAgainstTheGrantedInstanceTests
{
    [Test]
    public async Task A_hook_is_handed_the_granted_instances_socket()
    {
        var start = new System.Diagnostics.ProcessStartInfo();

        StackScript.PlaceInstance(start, "gg-env-1");

        await Assert.That(start.Environment["DOCKER_HOST"])
            .IsEqualTo("unix:///srv/env/gg-env-1/run/docker.sock")
            .Because("the agent has been pointed at its instance since ADR-0033 and the hook "
                   + "was not, so a bring-up would have reached whatever this machine's "
                   + "default happened to be - on a pool host, every member on it.");
    }

    [Test]
    public async Task It_is_the_same_derivation_the_agent_gets()
    {
        // NOT A SECOND CONVENTION. EnvironmentNaming.SocketFor is the one place
        // this address is derived on this side, and a hook pointed somewhere
        // else could talk to a different daemon from the agent working beside it
        // in the same flight.
        var hook = new System.Diagnostics.ProcessStartInfo();
        StackScript.PlaceInstance(hook, "gg-env-2");

        await Assert.That(hook.Environment["DOCKER_HOST"])
            .IsEqualTo(Gg.Runner.Pools.EnvironmentNaming.SocketFor("gg-env-2"));
    }

    [Test]
    public async Task A_flight_hosted_nowhere_is_handed_nothing()
    {
        // EVERY FLIGHT IN THE FIELD. An unhosted flight holds no instance, so
        // there is no socket to name - and inventing one would point a hook at a
        // path that does not exist, which is a worse failure than having no
        // hooks at all.
        foreach (var nowhere in (string?[]) [null, "", "   "])
        {
            var start = new System.Diagnostics.ProcessStartInfo();

            StackScript.PlaceInstance(start, nowhere);

            await Assert.That(start.Environment.ContainsKey("DOCKER_HOST")).IsFalse()
                .Because("absent is absent: a flight on no instance has no daemon of its own, "
                       + "and naming one would be a claim about a socket nobody granted.");
        }
    }

    [Test]
    public async Task A_hooks_own_DOCKER_HOST_cannot_displace_it()
    {
        // THE EXECUTOR'S ORDER, AS A RULE HERE TOO. PlaceVariables skips a name
        // already present, so the platform's address is placed first and a
        // document's own becomes inert - "the platform granted this instance,
        // and a document naming another would point the flight at somebody
        // else's stack". A hook is handed the same protection.
        var start = new System.Diagnostics.ProcessStartInfo();
        start.Environment["DOCKER_HOST"] = "unix:///var/run/docker.sock";

        StackScript.PlaceInstance(start, "gg-env-1");

        await Assert.That(start.Environment["DOCKER_HOST"])
            .IsEqualTo("unix:///srv/env/gg-env-1/run/docker.sock")
            .Because("the platform granted this instance, and an inherited address naming "
                   + "another would point the hook at the host's own daemon - which is the "
                   + "one thing the instance exists to keep it away from.");
    }
}
