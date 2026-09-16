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
