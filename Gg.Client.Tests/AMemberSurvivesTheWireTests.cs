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
