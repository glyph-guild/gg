using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// What comes back when somebody asks what is offered.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three answers, and two of them are "nothing".</b> An offer, or a control
/// plane offering nothing, or an offer with no settings in it. The middle one is
/// the ordinary state of a fleet nobody is reconfiguring, so it has to read as
/// an answer rather than as a failure — and the third is a document that says
/// so, which is not the same thing and must not be flattened into it.
/// </para>
/// <para>
/// <b>The session travels, and so does the floor.</b> This is a governed door
/// like every other: three version headers and a 426 a person can act on.
/// </para>
/// </remarks>
public class TheOfferAPersonFetchesTests
{
    private static OfferedConfiguration AnOffer() => new()
    {
        Version = "offer@7",
        OfferedAt = DateTimeOffset.UnixEpoch,
        Settings =
        [
            new OfferedSetting { Key = OfferableKeys.StunServers, Value = "stun:relay.invalid:3478" },
            new OfferedSetting { Key = OfferableKeys.VcsHosts, Value = "forge=git.invalid" },
        ],
    };

    [Test]
    public async Task An_offer_comes_back_whole()
    {
        await using var stub = new StubControlPlane { Offered = AnOffer() };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };

        var offered = await new ControlPlaneClient(http)
            .OfferedConfigurationAsync(StubControlPlane.IssuedSessionToken);

        await Assert.That(offered).IsNotNull();
        await Assert.That(offered!.Version).IsEqualTo("offer@7");
        await Assert.That(offered.Settings.Select(s => s.Key))
            .IsEquivalentTo((string[])[OfferableKeys.StunServers, OfferableKeys.VcsHosts]);
    }

    [Test]
    public async Task Offering_nothing_is_null_rather_than_a_failure()
    {
        // 204. The ordinary state of a fleet nobody is reconfiguring, so the
        // client has to hand back an absence a caller can render as "nothing
        // is offered" rather than throwing on the common case.
        await using var stub = new StubControlPlane { Offered = null };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };

        var offered = await new ControlPlaneClient(http)
            .OfferedConfigurationAsync(StubControlPlane.IssuedSessionToken);

        await Assert.That(offered).IsNull();
    }

    [Test]
    public async Task An_offer_with_nothing_in_it_is_not_the_same_as_no_offer()
    {
        // A DOCUMENT THAT SAYS "NOTHING", which is a control plane having
        // withdrawn what it was offering rather than never having offered. It
        // has a version, so accepting it is recordable - and flattening it into
        // 204 would lose the only thing that tells the two apart.
        await using var stub = new StubControlPlane
        {
            Offered = new OfferedConfiguration
            {
                Version = "offer@8",
                OfferedAt = DateTimeOffset.UnixEpoch,
                Settings = [],
            },
        };

        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };

        var offered = await new ControlPlaneClient(http)
            .OfferedConfigurationAsync(StubControlPlane.IssuedSessionToken);

        await Assert.That(offered).IsNotNull();
        await Assert.That(offered!.Settings).IsEmpty();
        await Assert.That(offered.Version).IsEqualTo("offer@8");
    }

    [Test]
    public async Task It_calls_the_declared_path_with_the_session_and_the_headers()
    {
        await using var stub = new StubControlPlane { Offered = AnOffer() };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };

        await new ControlPlaneClient(http)
            .OfferedConfigurationAsync(StubControlPlane.IssuedSessionToken);

        await Assert.That(stub.ObservedPaths).Contains("/v1/configuration/offered");

        var headers = stub.ObservedHeaders[^1];

        await Assert.That(headers.ContainsKey(GgVersions.SessionHeader)).IsTrue()
            .Because("it is a person's read, and the declaration requires the session.");

        foreach (var required in ProtocolSurface.VersionHeaders)
        {
            await Assert.That(headers.ContainsKey(required)).IsTrue()
                .Because($"{required} is declared on every governed request.");
        }
    }

    [Test]
    public async Task A_gg_below_the_floor_is_told_so_here_too()
    {
        await using var stub = new StubControlPlane
        {
            Offered = AnOffer(),
            ProtocolFloorMessage = "gg 0.1 is below this control plane's floor",
        };

        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };

        await Assert.That(async () => await new ControlPlaneClient(http)
                .OfferedConfigurationAsync(StubControlPlane.IssuedSessionToken))
            .Throws<ProtocolTooOldException>();
    }
}
