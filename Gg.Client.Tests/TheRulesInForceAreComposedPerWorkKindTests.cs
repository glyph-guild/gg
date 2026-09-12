using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The rules in force for a work kind are the floor composed with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED: "the rules in force does not show anything for hal."</b> It
/// showed the floor. <c>gg envelope show</c> answers <c>GET /v1/envelope</c>,
/// which is the ROOT document — so a tenant with a work kind applied reads a
/// pane titled "the rules in force" that contains none of it, and concludes
/// their document is not governing anything.
/// </para>
/// <para>
/// <b>"The rules in force" is not one answer.</b> What governs a flight
/// depends on its work kind: the floor composed with that kind's document.
/// There is no door that returns it — the doors are the root envelope, one
/// named document, and the list — because the control plane composes per
/// flight, as it runs one.
/// </para>
/// <para>
/// <b>Composing here is NOT a second opinion, which is the only reason it is
/// allowed.</b> <c>EnvelopeComposition.Compose</c> lives in
/// <c>Gg.Contracts</c> — the wire contract both repositories build against —
/// so this is the same computation the control plane performs, not gg's
/// opinion about merge semantics. A composer written in this client would be
/// exactly the second source of truth ADR-0016 § 6 refuses.
/// </para>
/// <para>
/// <b>And a refusal is carried, not swallowed.</b> Composition can fail — a
/// layer moving a field it may not — and that failure IS the answer about what
/// governs: nothing does, until somebody fixes it.
/// </para>
/// </remarks>
public class TheRulesInForceAreComposedPerWorkKindTests
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

    /// <summary>
    /// A floor, verbatim from a tree the parser accepted.
    /// </summary>
    /// <remarks>
    /// <b>Whole, because a trimmed one does not parse</b> - and a layer whose
    /// document came back null is refused by the composer as "the wrong
    /// document shape for the root role", which is the composer working while
    /// the test measures its own fixture.
    /// </remarks>
    private const string Root =
        """
        context:
          scope: "**"
          constitution: "1.0.0"
        environments: dev
        repositories:
          - "JDX/agile-cortex"
          - "JDX/JDNext"
        instructions:
          - "Keep your summary under 120 words. An operator reads these in a queue."
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
          widen-root:
            check: human
            when: "envelope widens"
            approver: platform-owner
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - edit
              - read
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    private static StubControlPlane Holding(StubControlPlane stub)
    {
        stub.Topology = new EnvelopeTopology
        {
            Names =
            [
                new TopologyName
                {
                    Name = "root",
                    Role = Roles.Root,
                    DeclaredBy = "the floor exists; nobody declares it",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
                new TopologyName
                {
                    Name = "score-hal",
                    Role = Roles.WorkKind,
                    Parent = "root",
                    DeclaredBy = "Kevin Deenanauth",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },

                // DECLARED, so asking for its rules in force reaches the
                // wrong-role refusal rather than the no-such-name one. Two
                // different sentences, and the test is about the first.
                new TopologyName
                {
                    Name = "dev",
                    Role = Roles.Strategy,
                    Parent = "root",
                    DeclaredBy = "Kevin Deenanauth",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };

        stub.Documents =
        [
            new NamedEnvelopeState
            {
                Name = "root",
                Role = Roles.Root,
                Version = "v7",
                UpdatedAt = DateTimeOffset.UnixEpoch,
                UpdatedBy = "Kevin Deenanauth",
                Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(Root).Envelope,
            },
            new NamedEnvelopeState
            {
                Name = "score-hal",
                Role = Roles.WorkKind,
                Version = "score-hal@v1",
                UpdatedAt = DateTimeOffset.UnixEpoch,
                UpdatedBy = "Kevin Deenanauth",
                Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(
                    AnAirspaceTreeOnDisk.WorkKind).Envelope,
            },
        ];

        return stub;
    }

    [Test]
    public async Task It_carries_the_work_kinds_own_rules()
    {
        await using var stub = Holding(new StubControlPlane());

        var said = VerbOutput.ToText(await Build(stub).RulesInForceAsync("score-hal"));

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal)
            .Because("the work kind's obligation is the thing somebody looked for and did "
                   + $"not find. Said:\n{said}");

        await Assert.That(said).Contains("score", StringComparison.Ordinal)
            .Because("and its loop, because that is what actually runs.");
    }

    [Test]
    public async Task And_the_floors_rules_as_well_because_both_govern()
    {
        await using var stub = Holding(new StubControlPlane());

        var said = VerbOutput.ToText(await Build(stub).RulesInForceAsync("score-hal"));

        await Assert.That(said).Contains("in-scope", StringComparison.Ordinal)
            .Because("the floor still governs a flight of this kind - that is what makes "
                   + "this a COMPOSITION rather than a second way to read one document.");
    }

    [Test]
    public async Task A_name_that_is_not_a_work_kind_is_refused_by_name()
    {
        await using var stub = Holding(new StubControlPlane());

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            async () => await Build(stub).RulesInForceAsync("dev"));

        await Assert.That(refused!.Message).Contains("work kind", StringComparison.OrdinalIgnoreCase)
            .Because("composition is per work kind - it is what a flight has - and asking "
                   + "for a strategy's rules in force is asking a question that has no "
                   + $"answer. Said: {refused.Message}");
    }

    [Test]
    public async Task A_composition_that_refuses_says_so_rather_than_answering_the_floor()
    {
        // THE WORST FAILURE WOULD BE QUIET. A layer that moves a field it may
        // not makes the composition refuse, and falling back to the floor would
        // show rules that are NOT in force under a title saying they are.
        await using var stub = Holding(new StubControlPlane());

        stub.Documents =
        [
            stub.Documents[0],
            stub.Documents[1] with
            {
                Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(
                    AnAirspaceTreeOnDisk.WorkKind.Replace(
                        "environments: dev",
                        "environments: production",
                        StringComparison.Ordinal)).Envelope,
            },
        ];

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            async () => await Build(stub).RulesInForceAsync("score-hal"));

        await Assert.That(refused!.Message).IsNotEmpty()
            .Because("the refusal IS the answer about what governs: nothing does, until "
                   + "somebody fixes it.");
    }
}
