using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A watch in a working copy is applied through the watch door, and pull writes one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The strategy's defect, one document class later, caught before anybody
/// hit it.</b> <c>AirspaceApplyAsync</c> dispatches <c>document.Strategy is {}
/// ? ApplyStrategyAsync : ApplyNamedAsync</c>, and its own comment records what
/// that cost the first time: <i>"a strategy arrived as an empty body and was
/// refused for carrying no document at all. A working copy with a strategy in
/// it had never been appliable."</i> Once the tree could read a watch, a working
/// copy holding one went down the same path to the same refusal.
/// </para>
/// <para>
/// <b>And pull is the other half of the round trip.</b> <c>AirspaceEstate</c>
/// held envelopes and strategies and nothing else, so a watch in force was
/// never written to a working copy — which makes a pull followed by an apply
/// look like the person deleted it, and <c>Retiring</c> is where that reading
/// would end up.
/// </para>
/// <para>
/// <b>A watch can be ordered better than a strategy can.</b> gg holds no
/// comparator for a strategy, so <c>Moves</c> orders it last and says so. It
/// holds one for a watch — <c>WatchDirection</c> — so a watch's direction is
/// computed rather than assumed.
/// </para>
/// </remarks>
public class AWatchAppliesThroughItsOwnDoorTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static ControlPlaneClient Client(StubControlPlane stub) =>
        new(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) });

    private static FlightCommands Build(StubControlPlane stub) =>
        new(Client(stub), new HeldSessionStore(ASession()));

    private static EnvelopeTopology Holding(StubControlPlane stub) => new()
    {
        Names =
        [
            .. stub.Topology.Names,
            new TopologyName
            {
                Name = "nightly-triage",
                Role = Roles.Watch,
                Parent = "root",
                DeclaredBy = "somebody, earlier",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    [Test]
    public async Task It_goes_to_the_watch_door_and_never_the_envelope_one()
    {
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAWatch();

        stub.Topology = Holding(stub);

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: false);

            await Assert.That(stub.AppliedWatches).Contains("nightly-triage");

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("the envelope door takes an Envelope or a Narrowing, and a watch is "
                       + "neither - it would arrive as an EMPTY BODY, which is the refusal a "
                       + "strategy got the first time. Sent to the envelope door: "
                       + string.Join(", ", stub.AppliedNames));

            await Assert.That(((VerbResult.AirspaceApplied)result).Value.Applied
                .Select(d => d.Name)).Contains("nightly-triage");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_watch_that_gates_is_reported_with_its_flight()
    {
        // THE DOOR DECLARES 202, SO A WATCH CAN BE WAITING. Reporting a gated
        // widening as landed would send somebody looking for a sweep that has
        // not changed.
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAWatch();

        stub.Topology = Holding(stub);
        stub.WatchDiverts = true;

        try
        {
            var watch = ((VerbResult.AirspaceApplied)await Build(stub)
                    .AirspaceApplyAsync(tree.FullName, declareNames: false))
                .Value.Applied.Single(d => d.Name == "nightly-triage");

            await Assert.That(watch.Flight).IsNotNull();
            await Assert.That(watch.Awaiting).IsNotNull();
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_estate_carries_the_watches_it_holds()
    {
        // THE PULL HALF. A third GET, joined into the same answer, so a watch in
        // force is something a working copy can hold at all.
        await using var stub = new StubControlPlane();

        stub.Watches =
        [
            new WatchState
            {
                Name = "nightly-triage",
                Version = "nightly-triage@v1",
                AppliedAt = DateTimeOffset.UnixEpoch,
                Watch = AnAirspaceTreeOnDisk.Watch(),
            },
        ];

        var estate = await Client(stub).ReadEstateAsync(StubControlPlane.IssuedSessionToken);

        await Assert.That(estate.Watches.Select(w => w.Name)).Contains("nightly-triage")
            .Because("an estate that never carried watches would render a working copy with "
                   + "none in it, and the apply after that pull would read as the person "
                   + "having deleted every one.");
    }

    [Test]
    public async Task Pull_writes_the_watch_and_the_tree_reads_it_back_unchanged()
    {
        await using var stub = new StubControlPlane();

        var estate = new AirspaceEstate
        {
            Documents = [],
            Strategies = [],
            Watches =
            [
                new WatchState
                {
                    Name = "nightly-triage",
                    Version = "nightly-triage@v1",
                    AppliedAt = DateTimeOffset.UnixEpoch,
                    Watch = AnAirspaceTreeOnDisk.Watch(),
                },
            ],
        };

        var root = Directory.CreateTempSubdirectory("gg-pull-").FullName;
        try
        {
            var written = AirspaceTree.Write(root, estate);

            await Assert.That(written.Written).Contains("airspace/watches/nightly-triage.yaml");

            var read = AirspaceTree.Read(root);

            await Assert.That(read.Unreadable).IsEmpty();
            await Assert.That(read.Documents.Single().Watch).IsNotNull();

            await Assert.That(AirspaceTree.Changed(read, estate)).IsEmpty()
                .Because("a document pulled and not edited is not a change. A pull followed "
                       + "by an apply that re-sent every watch would mint nothing on the "
                       + "stream - but it would still be a round trip nobody asked for.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_watch_is_ordered_by_its_own_comparator_rather_than_assumed()
    {
        // BETTER THAN A STRATEGY GETS, and the difference is deliberate. gg has
        // no strategy comparator and orders one last with a sentence saying so;
        // it has `WatchDirection`, so a watch whose filter changed is known to
        // widen and one whose period lengthened is known to tighten.
        var held = AnAirspaceTreeOnDisk.Watch();

        var estate = new AirspaceEstate
        {
            Documents = [],
            Strategies = [],
            Watches =
            [
                new WatchState
                {
                    Name = "nightly-triage",
                    Version = "nightly-triage@v1",
                    AppliedAt = DateTimeOffset.UnixEpoch,
                    Watch = held,
                },
            ],
        };

        TreeDocument Proposed(WatchDocument watch) => new()
        {
            Name = "nightly-triage",
            Role = Roles.Watch,
            Path = "airspace/watches/nightly-triage.yaml",
            Watch = watch,
        };

        var slower = FlightCommands.Moves(
            Proposed(held with { Trigger = new WatchTrigger { Every = "24h" } }), estate);

        await Assert.That(slower.Direction).IsEqualTo(Changeset.Tightening);

        var refiltered = FlightCommands.Moves(
            Proposed(held with { Filter = "SELECT [System.Id] FROM WorkItems" }), estate);

        await Assert.That(refiltered.Direction).IsEqualTo(Changeset.Widening);
        await Assert.That(refiltered.Field).IsEqualTo("filter");
    }
}
