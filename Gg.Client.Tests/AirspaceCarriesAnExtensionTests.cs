using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// S77.2-01: gg airspace reads, applies, pulls and shows a work kind that
/// extends another.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared under its parent, parents first.</b> The tree says nothing about
/// nesting for any other document, so those still go under root; a child's
/// <c>extends:</c> is the one place a file names its parent, and a parent
/// declared after its child would be a name the child's declaration found
/// missing.
/// </para>
/// <para>
/// <b>Pulled back as authored, shown resolved.</b> The file is the change a
/// person wrote, and what it means is the leaf over its parents - so show
/// prints both.
/// </para>
/// </remarks>
public class AirspaceCarriesAnExtensionTests
{
    private const string Base = """
        brief: "Investigate the work item."
        context:
          scope: "**"
          constitution: "1.0.0"
        accepts:
          - repository
        produces:
          - landing.proposal
        instructions:
          - "Your whole result is the landing you propose."
        obligations:
          investigate-finished:
            check: machine
            rule: loop-not-exhausted
        loops:
          investigate:
            executor: frontier
            discharges:
              - investigate-finished
            moves:
              - propose-landing
              - read
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          none:
            kind: none
            requires:
              - investigate-finished

        """;

    private const string Child = """
        extends: investigate
        brief: "Investigate the work item and write the plan to its tracker."
        remove:
          instructions:
            - "Your whole result is the landing you propose."

        """;

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

    private static DirectoryInfo Tree()
    {
        var root = Directory.CreateTempSubdirectory("gg-tree-");
        var kinds = Path.Combine(root.FullName, "airspace", "work-kinds");
        Directory.CreateDirectory(kinds);
        File.WriteAllText(Path.Combine(kinds, "investigate.yaml"), Base);
        File.WriteAllText(Path.Combine(kinds, "investigate-instructions.yaml"), Child);
        return root;
    }

    private static NamedEnvelopeState State(string name, Envelope? envelope = null, EnvelopeExtension? extension = null) => new()
    {
        Name = name,
        Role = Roles.WorkKind,
        Version = $"{name}@v1",
        Envelope = envelope,
        Extension = extension,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        UpdatedBy = "stub-principal",
    };

    [Test]
    public async Task A_child_in_the_tree_reads_as_its_extension()
    {
        var tree = Tree();
        try
        {
            var read = AirspaceTree.Read(tree.FullName);

            await Assert.That(read.Unreadable).IsEmpty()
                .Because("a file with extends: is a work kind, read through its own door.");
            var child = read.Documents.Single(d => d.Name == "investigate-instructions");
            await Assert.That(child.Extension!.Extends).IsEqualTo("investigate");
            await Assert.That(child.Envelope).IsNull();
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Apply_declares_the_parent_first_and_the_child_under_it_and_sends_the_extension()
    {
        await using var stub = new StubControlPlane();
        stub.NameLive = new TopologyName
        {
            Name = "investigate",
            Role = Roles.WorkKind,
            Parent = "root",
            DeclaredBy = "stub-principal",
            DeclaredAt = DateTimeOffset.UnixEpoch,
        };
        var tree = Tree();
        try
        {
            await Build(stub).AirspaceApplyAsync(tree.FullName, declareNames: true);

            await Assert.That(stub.DeclaredNames.Select(d => $"{d.Name}<{d.Parent}"))
                .IsEquivalentTo(
                    new[] { "investigate<root", "investigate-instructions<investigate" },
                    TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(stub.ObservedBodies.Any(b =>
                    b.Contains("\"extension\"", StringComparison.Ordinal)
                    && b.Contains("\"extends\":\"investigate\"", StringComparison.Ordinal)))
                .IsTrue()
                .Because("the child crosses as the change it makes, never resolved.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_name_declared_under_another_parent_is_refused_before_anything_is_sent()
    {
        await using var stub = new StubControlPlane();
        stub.Topology = new EnvelopeTopology
        {
            Names =
            [
                new TopologyName
                {
                    Name = "investigate-instructions",
                    Role = Roles.WorkKind,
                    Parent = "root",
                    DeclaredBy = "stub-principal",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };
        var tree = Tree();
        try
        {
            var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspaceApplyAsync(tree.FullName, declareNames: true));

            await Assert.That(refused!.Message).Contains("investigate-instructions");
            await Assert.That(stub.AppliedNames).IsEmpty();
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Pull_writes_the_child_back_as_authored()
    {
        var root = Directory.CreateTempSubdirectory("gg-tree-");
        try
        {
            var extension = EnvelopeYaml.ParseExtension(Child).Extension!;
            AirspaceTree.Write(root.FullName, new AirspaceEstate
            {
                Documents = [State("investigate-instructions", extension: extension)],
                Strategies = [],
            });

            var written = File.ReadAllText(Path.Combine(
                root.FullName, "airspace", "work-kinds", "investigate-instructions.yaml"));

            await Assert.That(written).IsEqualTo(
                "based-on: investigate-instructions@v1\n" + EnvelopeText.Render(extension));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Show_prints_the_child_and_what_it_resolves_to()
    {
        await using var stub = new StubControlPlane();
        stub.Documents =
        [
            State("investigate", envelope: EnvelopeYaml.Parse(Base).Envelope),
            State("investigate-instructions", extension: EnvelopeYaml.ParseExtension(Child).Extension),
        ];

        var shown = await Build(stub).AirspaceAsync("investigate-instructions");
        var text = VerbOutput.ToText(shown);

        await Assert.That(text).Contains("extends: investigate");
        await Assert.That(text).Contains("resolved over 'investigate'");
        await Assert.That(text).Contains("Investigate the work item and write the plan to its tracker.");
        var resolved = text[text.IndexOf("resolved over", StringComparison.Ordinal)..];
        await Assert.That(resolved).DoesNotContain("Your whole result is the landing you propose.")
            .Because("the resolved view is the leaf: the sentence the child removes is gone from it.");
    }
}
