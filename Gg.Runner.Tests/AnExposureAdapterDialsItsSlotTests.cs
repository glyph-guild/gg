using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner dials the slot it was granted, with the credential for that slot,
/// and reports the address it was told rather than one it read back.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027 § 5.</b> The adapter runs on the runner, behind one port, on the
/// precedent the runner rule already sets for writing: <i>writing lives behind
/// <c>IDestinationAdapter</c> and nowhere else</i>. The connection is outbound,
/// the credential resolves on the machine that holds it, and the control plane
/// learns the URL rather than brokering the traffic.
/// </para>
/// <para>
/// <b>It reports the address it was GIVEN, and this is the assertion that
/// matters most.</b> GG-268 read its URL out of a tunnel client's own stdout,
/// which is how a preview came to have an address nobody had registered
/// anywhere. A granted slot already has a name; reading one back from the
/// provider would be the runner choosing again, and the two could differ
/// without anything noticing.
/// </para>
/// <para>
/// <b>And it holds no provider API access.</b> The whole interaction is running
/// a connector with one slot's credential. It cannot create a hostname, move
/// one, or see another slot — so the most a stolen slot credential can do is
/// serve content at the one address it was minted for.
/// </para>
/// </remarks>
public class AnExposureAdapterDialsItsSlotTests
{
    private static LeasePreview Granted() => new()
    {
        Exposure = "jdapp",
        Slot = 3,
        Hostname = "jdapp-03.example.dev",
        Credential = "local:exposure/jdapp-03",
    };

    private static ExposureRequest Request() => new()
    {
        Preview = Granted(),
        Secret = "not-a-real-tunnel-token",
        Port = 4200,
    };

    [Test]
    public async Task It_reports_the_address_it_was_granted()
    {
        var connector = new RecordingConnector();
        var adapter = new CloudflareExposureAdapter(connector);

        var served = await adapter.ServeAsync(Request(), CancellationToken.None);

        await Assert.That(served.Url).IsEqualTo("https://jdapp-03.example.dev")
            .Because("a granted slot already has a name. Reading one back out of a tunnel "
                   + "client's own output is the runner choosing again, and it is how GG-268 "
                   + "came to serve at an address no identity provider had ever been told "
                   + "about.");
    }

    [Test]
    public async Task It_dials_with_that_slots_credential_and_nothing_else()
    {
        var connector = new RecordingConnector();
        var adapter = new CloudflareExposureAdapter(connector);

        _ = await adapter.ServeAsync(Request(), CancellationToken.None);

        await Assert.That(connector.Token).IsEqualTo("not-a-real-tunnel-token");
        await Assert.That(connector.Port).IsEqualTo(4200)
            .Because("the connector reaches the served port on this machine's own loopback, "
                   + "which is why none of this needs a published port or a firewall rule.");
    }

    [Test]
    public async Task The_fact_it_reports_names_the_exposure_and_the_slot()
    {
        var connector = new RecordingConnector();
        var adapter = new CloudflareExposureAdapter(connector);

        var served = await adapter.ServeAsync(Request(), CancellationToken.None);

        await Assert.That(served.Exposure).IsEqualTo("jdapp");
        await Assert.That(served.Slot).IsEqualTo("03")
            .Because("the fact is reconciled against an inventory, and the slot is spelled the "
                   + "way the inventory spells it so the two can be compared without arithmetic.");
    }

    [Test]
    public async Task A_connector_that_will_not_start_is_reported_rather_than_thrown()
    {
        var adapter = new CloudflareExposureAdapter(
            new RecordingConnector { Refusal = "cloudflared is not on this machine" });

        var served = await adapter.ServeAsync(Request(), CancellationToken.None);

        await Assert.That(served.Url).IsNull();
        await Assert.That(served.Diagnosis).IsNotNull()
            .Because("a preview that cannot be served is a flight that still did its work. The "
                   + "gate should say the preview is gone rather than the flight failing, which "
                   + "is the same rule as a member that reset under one.");
    }

    /// <summary>A connector that records what it was asked and starts nothing.</summary>
    private sealed class RecordingConnector : IExposureConnector
    {
        internal string? Token { get; private set; }

        internal int Port { get; private set; }

        internal string? Refusal { get; init; }

        public Task<string?> RunAsync(string token, int port, CancellationToken cancellationToken)
        {
            Token = token;
            Port = port;
            return Task.FromResult(Refusal);
        }
    }
}
