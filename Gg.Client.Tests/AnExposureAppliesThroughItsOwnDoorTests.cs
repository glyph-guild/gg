using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// An exposure in a working copy is applied through the exposure door.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same defect a third time, and this one reached a live estate.</b>
/// <c>AirspaceApplyAsync</c> dispatches strategy, then profile, then watch, and
/// everything else falls through to the envelope door. Its own comment records
/// what that cost twice before — a strategy arrived as an empty body, then a
/// watch would have. An exposure has no arm at all, so applying a real one
/// stopped with the control plane's refusal: <i>"An apply carries a document,
/// and this one carries neither an envelope nor a narrowing."</i>
/// </para>
/// <para>
/// <b>Found by applying the tenant's own document</b>, after every unit test
/// about exposures passed. The tree could read one and the door could serve
/// one; nothing joined them.
/// </para>
/// </remarks>
public class AnExposureAppliesThroughItsOwnDoorTests
{
    [Test]
    public async Task It_goes_to_the_exposure_door_and_never_the_envelope_one()
    {
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAnExposure();

        // DECLARED FIRST, because a name the airspace does not hold is refused
        // before any door is knocked on - which would hide the arm this test
        // is about.
        stub.Topology = Holding(stub);

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: false);

            await Assert.That(stub.AppliedExposures).Contains("jdapp");

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("the envelope door takes an Envelope or a Narrowing, and an exposure "
                       + "is neither - it arrives as an EMPTY BODY and is refused for carrying "
                       + "no document at all. Sent to the envelope door: "
                       + string.Join(", ", stub.AppliedNames));

            await Assert.That(((VerbResult.AirspaceApplied)result).Value.Applied
                .Select(d => d.Name)).Contains("jdapp");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static EnvelopeTopology Holding(StubControlPlane stub) => new()
    {
        Names =
        [
            .. stub.Topology.Names,
            new TopologyName
            {
                Name = "jdapp",
                Role = Roles.Exposure,
                Parent = "root",
                DeclaredBy = "somebody, earlier",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static ControlPlaneClient Client(StubControlPlane stub) =>
        new(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) });

    private static FlightCommands Build(StubControlPlane stub) =>
        new(Client(stub), new HeldSessionStore(ASession()));

    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };
}
