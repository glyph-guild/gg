using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg runners</c> says which flight each environment instance is hosting.
/// </summary>
/// <remarks>
/// <para>
/// <b>S54.5-02, and it could not be built until there were rows to read.</b>
/// `scheduling.gg_environment_instance` had readers and no writer until
/// good-grief#617; a listing over it would have been a pane that is always
/// empty, which is the shape this repository keeps finding.
/// </para>
/// <para>
/// <b>Under the host, because an instance belongs to one.</b> It is a UNIX user
/// on a machine (ADR-0034), the socket is a path on that machine, and only a
/// runner that IS that machine can open it (good-grief#627). So the instances
/// hang off the host's row, exactly as a pool member does, rather than forming a
/// listing of their own where a reader would have to join them back by hand.
/// </para>
/// <para>
/// <b>Free is a state worth a line.</b> The question a person asks of this
/// listing is "why is my preview not starting", and the two answers — every
/// instance is taken, or there are none — read completely differently. A row
/// only for the busy ones cannot tell them apart.
/// </para>
/// </remarks>
public class RunnersSayWhatIsHostedTests
{
    private static RunnerSummary AHost(params HostedInstance[] instances) => new()
    {
        RunnerId = "01a0a856-eac3-7671-9f0e-000000000000",
        Label = "vmlinux001",
        State = "idle",
        Resident = true,
        Instances = instances,
    };

    private static string TextOf(RunnerSummary host) =>
        VerbOutput.ToText(new VerbResult.Runners(new RunnerList { Runners = [host] }));

    [Test]
    public async Task An_instance_hosting_a_flight_says_which_one()
    {
        var text = TextOf(AHost(new HostedInstance
        {
            Environment = "ui",
            Instance = "gg-env-1",
            FlightNumber = "GG-412",
        }));

        await Assert.That(text).Contains("gg-env-1");
        await Assert.That(text).Contains("GG-412")
            .Because("the whole question this answers is which flight is using the stack, and "
                   + "an instance named without its flight is a row that says only that it "
                   + "exists.");
        await Assert.That(text).Contains("ui")
            .Because("a grant is made against the environment, and a tenant with two of them "
                   + "cannot read a list of bare slot names.");
    }

    [Test]
    public async Task A_free_instance_says_so_rather_than_being_left_out()
    {
        // THE QUESTION THIS LISTING EXISTS FOR is "why is my preview not
        // starting", and "every instance is taken" and "there are none" read
        // completely differently. A listing of only the busy ones cannot tell
        // those two apart.
        var text = TextOf(AHost(new HostedInstance
        {
            Environment = "ui",
            Instance = "gg-env-2",
        }));

        await Assert.That(text).Contains("gg-env-2");
        await Assert.That(text).Contains("free");
    }

    [Test]
    public async Task Each_instance_is_under_the_host_that_has_it()
    {
        var text = TextOf(AHost(
            new HostedInstance { Environment = "ui", Instance = "gg-env-1", FlightNumber = "GG-412" },
            new HostedInstance { Environment = "ui", Instance = "gg-env-2" }));

        var lines = text.Split('\n');
        var host = Array.FindIndex(lines, l => l.Contains("vmlinux001", StringComparison.Ordinal));
        var first = Array.FindIndex(lines, l => l.Contains("gg-env-1", StringComparison.Ordinal));
        var second = Array.FindIndex(lines, l => l.Contains("gg-env-2", StringComparison.Ordinal));

        await Assert.That(host).IsGreaterThanOrEqualTo(0);
        await Assert.That(first).IsEqualTo(host + 1)
            .Because("an instance reads as belonging to the machine above it, and only a "
                   + "machine that IS the host can open its socket.");
        await Assert.That(second).IsEqualTo(first + 1);
    }

    [Test]
    public async Task A_runner_with_no_instances_gains_no_lines()
    {
        // EVERY RUNNER IN THE FLEET, and this listing is what somebody reads
        // when a flight is waiting. A heading per machine saying "instances:
        // none" would be noise on every row of every tenant that hosts nothing.
        var text = VerbOutput.ToText(new VerbResult.Runners(new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "01a0a856-eac3-7671-9f0e-000000000001",
                    Label = "laptop",
                    State = "idle",
                },
            ],
        }));

        await Assert.That(text.Split('\n').Length).IsEqualTo(1);
        await Assert.That(text).DoesNotContain("free");
    }

    [Test]
    public async Task A_control_plane_that_does_not_say_is_not_reported_as_having_none()
    {
        // NULL IS NOT AN EMPTY LIST. A gg newer than the control plane it is
        // pointed at receives no member at all, and drawing "free" rows or a
        // heading from that would be this binary inventing a fact about a
        // machine - the `Whose` column's rule one method over.
        var text = TextOf(AHost());

        await Assert.That(text.Split('\n').Length).IsEqualTo(1);
    }
}
