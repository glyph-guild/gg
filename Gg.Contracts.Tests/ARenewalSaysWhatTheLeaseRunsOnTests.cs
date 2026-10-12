using System.Text.Json;

namespace Gg.Contracts.Tests;

/// <summary>
/// Three wire types carry a person's model choice (slice seventy-six): the
/// launch request, where it is made at creation; the renewal, where a runner
/// holding a child learns it changed; and the flight summary, where a reader
/// sees the choice in force.
/// </summary>
/// <remarks>
/// <b>The renewal is the channel</b> because the lease is composed again on
/// every renewal and nothing else reaches a runner mid-run: the heartbeat is
/// about the runner, not the flight. A renewal written before this carries
/// null and means what it meant.
/// </remarks>
public class ARenewalSaysWhatTheLeaseRunsOnTests
{
    [Test]
    public async Task The_three_members_are_on_the_wire_surface()
    {
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(FlightLaunchRequest)]).Contains("model")
            .Because("gg fly --model has to reach ingress as a declared member, beside environment.");
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(LeaseRenewed)]).Contains("model")
            .Because("a runner holding a child learns a move only here.");
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(FlightSummary)]).Contains("model")
            .Because("gg show has to be able to say what was chosen.");
    }

    [Test]
    public async Task A_renewal_names_the_model_and_absent_reads_as_null()
    {
        var renewed = new LeaseRenewed
        {
            ExpiresAt = DateTimeOffset.UnixEpoch, Generation = 3, Model = "claude-sonnet-5-5",
        };

        var json = JsonSerializer.Serialize(renewed, JsonSerializerOptions.Web);
        await Assert.That(json).Contains("\"model\":\"claude-sonnet-5-5\"");

        var older = JsonSerializer.Deserialize<LeaseRenewed>(
            """{"expiresAt":"1970-01-01T00:00:00+00:00","generation":3}""", JsonSerializerOptions.Web);
        await Assert.That(older!.Model).IsNull()
            .Because("a control plane that predates the member renews as it always did.");
    }
}
