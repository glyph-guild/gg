using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A document that has been applied can be read back by name.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM A REAL TENANT: "when I look at the envelope it still
/// doesn't have my score-hal stuff."</b> It did — the estate held the work
/// kind, byte for byte. What it did not have was any way to SHOW it.
/// </para>
/// <para>
/// <b><c>gg envelope show</c> is the root document, and reads like
/// everything.</b> It answers <c>GET /v1/envelope</c>, which carries the
/// floor: its obligations, its loops, its version. A work kind is a separate
/// document governing flights of that kind and does not appear there — so
/// somebody who applies one and looks at the only "show me the rules" verb
/// there is concludes it did not land.
/// </para>
/// <para>
/// <b>And the content was already in hand.</b> <c>ReadEstateAsync</c> returns
/// every document whole — <c>NamedEnvelopeState</c> carries the envelope or
/// the narrowing, the role, and the version that names it permanently. gg
/// fetched all of it to compute a diff and rendered none of it. Read,
/// held, and shown nowhere, which is the third time that shape has turned up
/// in this estate.
/// </para>
/// <para>
/// <b>The version is the point of reading it back.</b> A document you can see
/// but cannot name is one you cannot quote in an argument about what governed
/// a flight, and <c>score-hal@v1</c> is what an attribution will say.
/// </para>
/// </remarks>
public class AnAppliedDocumentCanBeReadBackTests
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

    /// <summary>An estate holding one applied work kind, and the name for it.</summary>
    private static StubControlPlane Holding(StubControlPlane stub)
    {
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
                    DeclaredBy = "Kevin Deenanauth",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };

        stub.Documents =
        [
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
    public async Task It_renders_the_document_the_name_holds()
    {
        await using var stub = Holding(new StubControlPlane());

        var said = VerbOutput.ToText(await Build(stub).AirspaceAsync("score-hal"));

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal)
            .Because("the obligations ARE the document - a person reading one back is "
                   + $"checking exactly this. Said:\n{said}");

        await Assert.That(said).Contains("score-hal@v1", StringComparison.Ordinal)
            .Because("and the version that names it permanently, because that is what an "
                   + "attribution will say governed a flight.");
    }

    [Test]
    public async Task A_name_the_estate_does_not_hold_says_so_and_says_what_does()
    {
        await using var stub = Holding(new StubControlPlane());

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            async () => await Build(stub).AirspaceAsync("score-hall"));

        await Assert.That(refused!.Message).Contains("score-hall", StringComparison.Ordinal)
            .Because("echoing what was asked for is what makes a typo visible.");

        await Assert.That(refused.Message).Contains("score-hal", StringComparison.Ordinal)
            .Because("and the names it DOES hold, because the commonest reason this is "
                   + "asked is a misremembered name - a bare 'not found' sends somebody to "
                   + "another verb to get the list.");
    }

    [Test]
    public async Task A_declared_name_with_no_document_is_a_different_answer()
    {
        // THE STATE SOMEBODY SPENT AN EVENING IN. A name can be declared - the
        // registration landed - with nothing applied to it yet, and that is
        // neither "no such name" nor a document. Saying the first would send
        // them to declare something that already exists.
        await using var stub = Holding(new StubControlPlane());

        stub.Documents = [];

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            async () => await Build(stub).AirspaceAsync("score-hal"));

        await Assert.That(refused!.Message)
            .Contains("nothing has been applied", StringComparison.OrdinalIgnoreCase)
            .Because("the name exists and holds no document, which is what an apply that "
                   + $"never ran leaves behind. Said: {refused.Message}");
    }

    [Test]
    public async Task Asking_for_no_name_still_lists_the_topology()
    {
        // THE VERB IT ALREADY WAS. `gg airspace show` answers the names; a name
        // narrows it to one document. Breaking the list to add the detail would
        // trade one gap for another.
        await using var stub = new StubControlPlane();

        var result = await Build(stub).AirspaceAsync();

        await Assert.That(result).IsTypeOf<VerbResult.AirspaceTopology>()
            .Because("no name means the whole topology, exactly as before.");
    }
}
