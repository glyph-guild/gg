using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Every applied document is readable in one request.
/// </summary>
/// <remarks>
/// <para>
/// <b>BECAUSE THE PANE DRAWS WHATEVER THE CURSOR IS ON.</b> The airspace tab
/// shows the selected document as applied, and a console that fetched one per
/// row would be making a request on every arrow key — which
/// <c>ConsoleStart</c> refuses by name: <i>"reading on the arrow key would be
/// I/O inside a UI session"</i>.
/// </para>
/// <para>
/// <b>One request, not one per name.</b> <c>gg airspace show &lt;name&gt;</c>
/// answers a single document and would be N requests to fill a tree;
/// <c>ReadEstateAsync</c> already joins the two GETs that return all of them,
/// and this is that read given a verb so the console can use it.
/// </para>
/// <para>
/// <b>Strategies come with them.</b> They read through their own door and land
/// in their own list — a fact about the wire rather than about what somebody
/// is looking at — and a pane that could draw a work kind and not a strategy
/// would be showing the wire's shape instead of the tree's.
/// </para>
/// </remarks>
public class TheEstatesDocumentsAreReadableAtOnceTests
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

    [Test]
    public async Task It_answers_every_document_the_estate_holds()
    {
        await using var stub = new StubControlPlane();

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

        var result = await Build(stub).AirspaceDocumentsAsync();

        var estate = ((VerbResult.AirspaceDocuments)result).Value;

        await Assert.That(estate.Documents.Select(d => d.Name)).Contains("score-hal")
            .Because("the pane draws this without asking again, which is the whole reason "
                   + "one request answers all of them.");

        await Assert.That(estate.Documents[0].Envelope).IsNotNull()
            .Because("and WHOLE - a list of names would leave the pane fetching bodies per "
                   + "row, which is the thing this avoids.");
    }

    [Test]
    public async Task Strategies_come_back_with_them()
    {
        await using var stub = new StubControlPlane();

        stub.Strategies =
        [
            new EnvironmentStrategyState
            {
                Name = "dev",
                Version = "dev@v5",
                AppliedAt = DateTimeOffset.UnixEpoch,
                Strategy = Gg.Contracts.Authoring.EnvelopeYaml.ParseStrategy(
                    AnAirspaceTreeOnDisk.Strategy).Strategy!,
            },
        ];

        var estate = ((VerbResult.AirspaceDocuments)
            await Build(stub).AirspaceDocumentsAsync()).Value;

        await Assert.That(estate.Strategies.Select(s => s.Name)).Contains("dev")
            .Because("a strategy is a row in the same tree. That it reads through a "
                   + "different door is the wire's shape, not the person's.");
    }
}
