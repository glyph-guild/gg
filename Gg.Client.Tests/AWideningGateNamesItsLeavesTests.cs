using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// A pending gate carries the leaves a widening reaches, each with its field and reason, and
/// <c>gg gates</c> prints them under the gate; a control plane that predates it sends none
/// (S81.1-01, ADR-0041 Decision 6).
/// </summary>
public class AWideningGateNamesItsLeavesTests
{
    private static PendingGate AGate(params WideningLeaf[] leaves) => new()
    {
        FlightNumber = "GG-1200",
        ObligationId = "widen-root",
        Approver = "platform-owner",
        ManifestHash = new string('e', 64),
        Attempt = 1,
        Condition = "envelope widens",
        Because = "the proposed change widens obligations, and a widening is gated by the obligation the widened document declares",
        AwaitingSince = new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero),
        Widening = leaves,
    };

    private static readonly WideningLeaf[] Both =
    [
        new() { Leaf = "investigate", Field = "obligations", Because = "obligation 'investigate-in-scope' is gone" },
        new() { Leaf = "investigate-instructions", Field = "obligations", Because = "obligation 'investigate-in-scope' is gone" },
    ];

    [Test]
    public async Task The_leaves_cross_the_wire_and_an_older_control_plane_sends_none()
    {
        var json = JsonSerializer.Serialize(new GateList { Gates = [AGate(Both)] }, ProtocolJsonContext.Default.GateList);
        var read = JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.GateList)!;

        await Assert.That(read.Gates.Single().Widening.Select(l => l.Leaf))
            .IsEquivalentTo(new[] { "investigate", "investigate-instructions" });

        var old = JsonSerializer.Deserialize(
            json.Replace("\"widening\"", "\"ignoredByAnOlderPlane\"", StringComparison.Ordinal),
            ProtocolJsonContext.Default.GateList);
        await Assert.That(old).IsNotNull();

        await Assert.That(ProtocolSurface.JsonMembers[typeof(PendingGate)]).Contains("widening");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WideningLeaf)])
            .IsEquivalentTo(new[] { "leaf", "field", "because" });
        await Assert.That(Vocabulary.Types).Contains(typeof(WideningLeaf));
    }

    [Test]
    public async Task Gg_gates_prints_every_leaf_under_the_gate()
    {
        var text = VerbOutput.ToText(new VerbResult.Gates(new GateList { Gates = [AGate(Both)] }));

        await Assert.That(text).Contains("investigate-instructions");
        await Assert.That(text).Contains("obligation 'investigate-in-scope' is gone");
        await Assert.That(text.IndexOf("widens these leaves", StringComparison.Ordinal))
            .IsGreaterThan(text.IndexOf("because:", StringComparison.Ordinal))
            .Because("under the gate it is about, after the platform's own sentence. Printed: " + text);
    }

    [Test]
    public async Task A_gate_with_no_leaves_prints_as_it_always_did()
    {
        var text = VerbOutput.ToText(new VerbResult.Gates(new GateList { Gates = [AGate()] }));

        await Assert.That(text).DoesNotContain("widens these leaves");
    }
}
