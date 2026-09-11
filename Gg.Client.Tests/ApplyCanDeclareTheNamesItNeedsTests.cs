using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Apply knows which names its documents need, refuses up front when they do
/// not exist, and declares them when asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT USED TO DIE ON THE FIRST ONE.</b> An envelope applied to an
/// undeclared name is refused — <c>ProtocolSurface</c>: <i>"an envelope
/// applied to an undeclared name is refused pointing HERE, so the door ships
/// in the same contract as the refusal"</i> — and apply runs tightenings
/// first, so a new document is usually the first thing sent. The changeset
/// stopped there, having already applied whatever came before it, and the
/// person learned about exactly one missing name per attempt.
/// </para>
/// <para>
/// <b>So the check moves ahead of the first request.</b> Apply reads the
/// topology, names EVERY document whose name is missing, and sends nothing —
/// a changeset is something somebody meant as a whole, which is the same
/// reason an unreadable file refuses the lot.
/// </para>
/// <para>
/// <b>And declaring is OPT-IN, which is a safety property rather than
/// timidity.</b> A declared name cannot be quietly withdrawn: retirement is
/// <i>"a version rather than a deletion"</i> (ADR-0014) and its door has no
/// 200 — <i>"a document that stops applying removes every constraint in it at
/// once, so this is a widening by construction and always rides the gate"</i>.
/// So a typo in a filename would mint a permanent name whose removal needs an
/// approver. Apply declaring silently would make the cheapest mistake in the
/// system the most expensive to undo.
/// </para>
/// <para>
/// <b>The parent is root and is said out loud.</b> The tree gives the ROLE —
/// <c>AirspaceNames</c> maps directory to role both ways — and says nothing
/// about nesting, so anything deeper stays a deliberate
/// <c>gg airspace name … --under</c>. <c>FlightCommands.DeclareNameAsync</c>
/// already refuses to default a parent itself: <i>"the default belongs where
/// somebody can see it in the usage, not in a fallback halfway down a
/// client"</i>, so this states it rather than inheriting it.
/// </para>
/// </remarks>
public class ApplyCanDeclareTheNamesItNeedsTests
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
            // THE ONE THAT IS ALREADY SHARED. Four private copies of this class
            // exist in this suite; reaching for a fifth would be the fifth.
            new HeldSessionStore(ASession()));

    /// <summary>A tree holding one document for a name nobody declared.</summary>
    private static DirectoryInfo Tree()
    {
        var root = Directory.CreateTempSubdirectory("gg-declare-");

        Directory.CreateDirectory(Path.Combine(root.FullName, "airspace", "work-kinds"));

        // A WORK KIND THAT ACTUALLY PARSES. The first draft of this fixture
        // did not, and the apply refused it as an unreadable file before ever
        // reaching the topology - which is the tree read's own refusal working
        // correctly and this test measuring the wrong thing. Shaped after a
        // real one.
        // A WORK KIND THAT ACTUALLY PARSES, taken from the real tree whose
        // apply this test is about. The first two drafts did not parse, so the
        // apply refused them as unreadable files before ever reaching the
        // topology - the tree read working correctly and this test measuring
        // the wrong thing.
        File.WriteAllText(
            Path.Combine(root.FullName, "airspace", "work-kinds", "score-hal.yaml"),
            """
            context:
              scope: "**"
              constitution: "1.0.0"
            environments: dev
            repositories:
              - "JDX/JDNext"
            accepts:
              - tracker
              - repository
            produces:
              - loop.outcome
              - loop.digest
              - loop.question
              - destination.landed
            obligations:
              hal-in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              score:
                executor: frontier
                discharges:
                  - hal-in-scope
                moves:
                  - read
                  - search
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              agentic-backlog:
                kind: work-item-tracker
                may-perform:
                  - field
                may-write:
                  - "Custom.HAL"
                requires:
                  - hal-in-scope
            """);

        return root;
    }

    [Test]
    public async Task An_undeclared_name_is_refused_before_anything_is_sent()
    {
        await using var stub = new StubControlPlane();
        var tree = Tree();

        try
        {
            var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspaceApplyAsync(
                    tree.FullName, declareNames: false));

            await Assert.That(refused!.Message).Contains("score-hal", StringComparison.Ordinal)
                .Because("the name is what the remedy needs.");

            await Assert.That(refused.Message)
                .Contains("gg airspace name", StringComparison.Ordinal)
                .Because("the door that declares one is the remedy, and the contract says "
                       + "this refusal points at it.");

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("NOTHING IS SENT. A changeset is something somebody meant as a "
                       + "whole, so a name that cannot be reached stops the lot - the same "
                       + "reason an unreadable file does. Sent: "
                       + string.Join(", ", stub.AppliedNames));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Asked_to_declare_it_declares_under_root_and_then_applies()
    {
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.NameLive = new TopologyName
        {
            Name = "score-hal",
            Role = Roles.WorkKind,
            Parent = "root",
            DeclaredBy = "stub-principal",
            DeclaredAt = DateTimeOffset.UnixEpoch,
        };

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: true);

            await Assert.That(stub.DeclaredNames.Count).IsEqualTo(1);
            await Assert.That(stub.DeclaredNames[0].Name).IsEqualTo("score-hal");
            await Assert.That(stub.DeclaredNames[0].Role).IsEqualTo(Roles.WorkKind)
                .Because("the directory decided the role, and AirspaceNames reads that "
                       + "table both ways so the mapping cannot disagree with itself.");
            await Assert.That(stub.DeclaredNames[0].Parent).IsEqualTo("root")
                .Because("the tree says nothing about nesting, so the parent is root and is "
                       + "stated rather than guessed deeper down.");

            await Assert.That(stub.AppliedNames).Contains("score-hal")
                .Because("declared first, then applied - in that order, or the apply hits "
                       + "the refusal this exists to avoid.");

            var applied = (VerbResult.AirspaceApplied)result;

            await Assert.That(applied.Value.Declared.Select(d => d.Name)).Contains("score-hal")
                .Because("what an apply DID is what it reports, and declaring a name is a "
                       + "governance act rather than bookkeeping - a person who ran one "
                       + "keypress needs telling that a name now exists.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_declaration_that_gates_is_reported_and_its_document_is_not_applied()
    {
        // 202 ON THE NAME DOOR: "the registration widens what the tenant can
        // reach, so it rides a flight and the answer says who decides". The
        // name does not exist yet, so applying the document to it would be
        // refused - and reporting it as applied would send somebody looking
        // for a version nobody minted.
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.NamePending = new RegistrationPending
        {
            Flight = "GG-77",
            Awaiting = "platform-owner",
            Widens = "the names this tenant can reach",
        };

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: true);

            var applied = (VerbResult.AirspaceApplied)result;

            await Assert.That(applied.Value.Declared.Count).IsEqualTo(1);
            await Assert.That(applied.Value.Declared[0].Flight).IsEqualTo("GG-77");
            await Assert.That(applied.Value.Declared[0].Awaiting).IsEqualTo("platform-owner")
                .Because("a gate with nobody named is a gate a person cannot go and ask "
                       + "about.");

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("the name is not reachable until somebody decides, so the document "
                       + "waits with it. Sent: " + string.Join(", ", stub.AppliedNames));

            await Assert.That(applied.Value.Applied).IsEmpty()
                .Because("and nothing is reported as applied that was not.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_name_the_topology_already_holds_is_not_declared_again()
    {
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.Topology = new EnvelopeTopology
        {
            Names =
            [
                .. stub.Topology.Names,
                new TopologyName
                {
                    Name = "score-hal",
                    Role = Roles.WorkKind,
                    Parent = "root",
                    DeclaredBy = "somebody, earlier",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };

        try
        {
            await Build(stub).AirspaceApplyAsync(tree.FullName, declareNames: true);

            await Assert.That(stub.DeclaredNames).IsEmpty()
                .Because("declaring one that exists is a request that can only be refused, "
                       + "and asking for it every apply would make the log unreadable.");

            await Assert.That(stub.AppliedNames).Contains("score-hal");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
