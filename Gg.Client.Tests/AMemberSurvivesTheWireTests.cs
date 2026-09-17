using System.Text.Json;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A member's host survives the wire, and a runner without one writes no key.
/// </summary>
/// <remarks>
/// <b>The shape is asserted in Gg.Contracts.Tests; this is the serializer half.</b>
/// That project takes no dependency that could reach a JsonSerializerContext, so
/// the round trip belongs here, beside the context the client actually reads
/// the fleet with.
/// </remarks>
public class AMemberSurvivesTheWireTests
{
    private static RunnerSummary Member() => new()
    {
        RunnerId = "01a0a8d4-ae13-74e9-af76-7aae5b144764",
        Label = "gg-pool-ui-1",
        State = "idle",
        HostRunnerId = "01a0a856-eac3-7671-9f0e-000000000000",
    };

    private static RunnerSummary Laptop() => new()
    {
        RunnerId = "01a0a81c-2d70-7393-a355-fa3d134ba06c",
        Label = "Kevins-MBP",
        State = "offline",
    };

    [Test]
    public async Task The_host_comes_back_off_the_wire()
    {
        var json = JsonSerializer.Serialize(Member(), ProtocolJsonContext.Default.RunnerSummary);
        var back = JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.RunnerSummary)!;

        await Assert.That(back.HostRunnerId).IsEqualTo(Member().HostRunnerId)
            .Because("the console groups on this, so a value that did not survive the "
                   + "journey would flatten the list without saying why.");
    }

    [Test]
    public async Task Gg_runners_puts_a_member_under_its_host_and_indents_it()
    {
        // THE SAME SHAPE THE CONSOLE DRAWS, on the surface a person is more
        // likely to be looking at when they go hunting for a machine. Listed
        // flat, the three machines somebody operates scatter among the members
        // their pools created.
        //
        // THE MEMBER IS FIRST HERE, which is what the fleet's own order gives
        // once a pool has been rebuilt a few times.
        var text = VerbOutput.ToText(new VerbResult.Runners(new RunnerList
        {
            Runners =
            [
                Member(),
                new RunnerSummary
                {
                    RunnerId = "01a0a856-eac3-7671-9f0e-000000000000",
                    Label = "vmlinux001",
                    State = "idle",
                },
                Laptop(),
            ],
        }));

        var lines = text.Split('\n');
        var member = Array.FindIndex(
            lines, l => l.Contains("gg-pool-ui-1", StringComparison.Ordinal));
        var host = Array.FindIndex(
            lines, l => l.Contains("vmlinux001", StringComparison.Ordinal));

        await Assert.That(member).IsEqualTo(host + 1)
            .Because("a member reads as belonging to the machine above it, and a row between "
                   + "them breaks that.");
        await Assert.That(lines[member].StartsWith("  ", StringComparison.Ordinal)).IsTrue()
            .Because("adjacency alone is just two rows; the indent is what says one warmed "
                   + "the other. Drawn: " + lines[member]);
        await Assert.That(lines[host].StartsWith(" ", StringComparison.Ordinal)).IsFalse()
            .Because("a machine is flush, or the indent says nothing.");
    }

    [Test]
    public async Task Gg_runners_groups_a_machine_under_its_resident()
    {
        // THE SAME RULE THE CONSOLE KEEPS, on the surface a person reads when
        // hunting for a machine: the maintainer and the members sit beneath
        // the resident as peers, not the members beneath the maintainer.
        const string resident = "01a06572-a784-7000-8000-000000000000";
        const string maintainer = "01a0632b-e971-7000-8000-000000000000";

        var text = VerbOutput.ToText(new VerbResult.Runners(new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "m1", Label = "gg-pool-ui-1", State = "idle",
                    Machine = "vmlinux001", HostRunnerId = maintainer,
                },
                new RunnerSummary
                {
                    RunnerId = maintainer, Label = "vmlinux001:maintain", State = "offline",
                    Machine = "vmlinux001",
                },
                new RunnerSummary
                {
                    RunnerId = resident, Label = "vmlinux001", State = "idle",
                    Machine = "vmlinux001",
                },
                Laptop(),
            ],
        }));

        var lines = text.Split('\n');
        int Line(string label) => Array.FindIndex(
            lines, l => l.Contains(label + " ", StringComparison.Ordinal)
                     || l.EndsWith(label, StringComparison.Ordinal));

        var residentLine = Line("vmlinux001");
        var maintainerLine = Line("vmlinux001:maintain");
        var memberLine = Line("gg-pool-ui-1");

        await Assert.That(maintainerLine).IsGreaterThan(residentLine);
        await Assert.That(memberLine).IsGreaterThan(residentLine);
        await Assert.That(lines[maintainerLine].StartsWith("  ", StringComparison.Ordinal))
            .IsTrue()
            .Because("the maintainer runs on the machine, so it sits under it. Drawn:\n" + text);
        await Assert.That(lines[memberLine].StartsWith("    ", StringComparison.Ordinal))
            .IsFalse()
            .Because("a member is a peer of the maintainer under the machine, not a "
                   + "grandchild of it. Drawn:\n" + text);
        await Assert.That(lines[residentLine].StartsWith(" ", StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    public async Task Gg_runners_puts_a_member_with_no_machine_under_its_hosts_machine()
    {
        // THE CONSOLE'S CASE ON THIS SURFACE: members minted before their
        // maintainer stated a machine carry none, and pointing at a maintainer
        // that is itself nested sent them back out flush.
        const string resident = "01a06572-a784-7000-8000-000000000000";
        const string maintainer = "01a0632b-e971-7000-8000-000000000000";

        var text = VerbOutput.ToText(new VerbResult.Runners(new RunnerList
        {
            Runners =
            [
                Laptop(),
                new RunnerSummary
                {
                    RunnerId = resident, Label = "vmlinux001", State = "idle",
                    Machine = "vmlinux001",
                },
                new RunnerSummary
                {
                    RunnerId = maintainer, Label = "vmlinux001:maintain", State = "offline",
                    Machine = "vmlinux001",
                },
                new RunnerSummary
                {
                    RunnerId = "m1", Label = "gg-pool-ui-1", State = "idle",
                    HostRunnerId = maintainer,
                },
            ],
        }));

        var lines = text.Split('\n');
        int Line(string label) => Array.FindIndex(
            lines, l => l.Contains(label + " ", StringComparison.Ordinal)
                     || l.EndsWith(label, StringComparison.Ordinal));

        var memberLine = Line("gg-pool-ui-1");

        await Assert.That(memberLine).IsGreaterThan(Line("vmlinux001"));
        await Assert.That(lines[memberLine].StartsWith("  ", StringComparison.Ordinal)).IsTrue()
            .Because("with no machine of its own, a member is on its maintainer's. Drawn:\n" + text);
        await Assert.That(lines[memberLine].StartsWith("    ", StringComparison.Ordinal)).IsFalse()
            .Because("and a peer of the maintainer there, not its grandchild. Drawn:\n" + text);
    }

    [Test]
    public async Task A_runner_with_no_host_writes_no_key()
    {
        // OMITTED RATHER THAN NULL. A control plane that has not learned this
        // field sends the body it has always sent, and this keeps the body gg
        // sends for a laptop identical to that one - so the two are
        // indistinguishable to any reader, which is what lets the field ship
        // before the control plane fills it.
        var json = JsonSerializer.Serialize(Laptop(), ProtocolJsonContext.Default.RunnerSummary);

        await Assert.That(json).DoesNotContain("HostRunnerId", StringComparison.Ordinal);
        await Assert.That(
                JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.RunnerSummary)!
                    .HostRunnerId)
            .IsNull();
    }
}
