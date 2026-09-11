using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A strategy document is applied through the strategy door, not the envelope
/// one.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED IN THE WORLD, on a tenant with three documents.</b> The apply
/// sent every changed document to <c>PUT /v1/airspace/envelopes/{name}</c>,
/// including <c>airspace/strategies/dev.yaml</c>, and the control plane
/// answered exactly what was wrong:
/// </para>
/// <para>
/// <i>"An apply carries a document, and this one carries neither an envelope
/// nor a narrowing. An empty body is not a way to retire a name - retiring is
/// a terminal version, gated and attributed like any other change."</i>
/// </para>
/// <para>
/// <b>Because <c>Body</c> fills two members and a strategy is in neither.</b>
/// <c>NamedEnvelopeApply</c> carries an <c>Envelope</c> or an
/// <c>EnvelopeNarrowing</c>; <c>TreeDocument.Strategy</c> is a third thing with
/// a door of its own — <c>PUT /v1/airspace/strategies/{name}</c>, and
/// <c>ApplyStrategyAsync</c> has been on the client the whole time, called by
/// nothing. So a working copy containing a strategy has never been appliable,
/// and the failure landed on whichever document sorted first among the
/// widenings.
/// </para>
/// <para>
/// <b>It is the port-nothing-calls shape again</b>, and the reason it survived
/// is that the strategy door has its own verb — <c>gg strategy apply</c> — so
/// the method had a caller. Just not the one that applies a working copy.
/// </para>
/// </remarks>
public class AStrategyAppliesThroughItsOwnDoorTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    /// <summary>A tree holding one strategy, shaped after a real one.</summary>
    private static DirectoryInfo Tree()
    {
        var root = Directory.CreateTempSubdirectory("gg-strategy-");

        Directory.CreateDirectory(Path.Combine(root.FullName, "airspace", "strategies"));

        File.WriteAllText(
            Path.Combine(root.FullName, "airspace", "strategies", "dev.yaml"),
            """
            kind: docker-host
            environment: dev
            inventory:
              pool: gg-pool-dev
              size: 2
              warm: 1
            pull-point: resident-runner
            image: "127.0.0.1:5000/gg-member@sha256:fa54cda495558afe3a281744a5a05165e6461dfb18b8300a4299798fd2db7254"
            bounds:
              pool-max: 2
            """);

        return root;
    }

    /// <summary>A topology that already holds the name, so nothing is declared.</summary>
    private static EnvelopeTopology Holding(StubControlPlane stub) => new()
    {
        Names =
        [
            .. stub.Topology.Names,
            new TopologyName
            {
                Name = "dev",
                Role = Roles.Strategy,
                Parent = "root",
                DeclaredBy = "somebody, earlier",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    [Test]
    public async Task It_goes_to_the_strategy_door_and_never_the_envelope_one()
    {
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.Topology = Holding(stub);

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: false);

            await Assert.That(stub.AppliedStrategies).Contains("dev")
                .Because("a strategy has its own door and its own body type, and "
                       + "ApplyStrategyAsync has been on the client all along with no "
                       + "caller that applies a working copy.");

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("the envelope door takes an Envelope or a Narrowing. A strategy "
                       + "is neither, so it arrived as an EMPTY BODY - which the control "
                       + "plane refused, correctly, as not carrying a document at all. "
                       + "Sent to the envelope door: " + string.Join(", ", stub.AppliedNames));

            var applied = (VerbResult.AirspaceApplied)result;

            await Assert.That(applied.Value.Applied.Select(d => d.Name)).Contains("dev")
                .Because("and it is reported like any other document, because to the person "
                       + "applying a working copy it IS one.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_strategy_that_gates_is_reported_with_its_flight()
    {
        // THE STRATEGY DOOR DIVERTS TOO. A strategy that widens what a pool may
        // hold rides a gate exactly as an envelope widening does, and reporting
        // it as landed would send somebody looking for a pool that has not
        // changed size.
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.Topology = Holding(stub);
        stub.StrategyDiverts = true;

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: false);

            var dev = ((VerbResult.AirspaceApplied)result).Value.Applied
                .Single(d => d.Name == "dev");

            await Assert.That(dev.Flight).IsNotNull();
            await Assert.That(dev.Awaiting).IsNotNull()
                .Because("a gate with nobody named is a gate a person cannot go and ask "
                       + "about - the same rule the envelope door's 202 follows.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
