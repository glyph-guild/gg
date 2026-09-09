using System.Text.RegularExpressions;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Where an offer travels, and where it must not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing connects inbound to a laptop, and nothing is going to.</b> A
/// runner heartbeats and a console refreshes, so an offer rides a response the
/// machine already asked for — the mechanism <c>HeartbeatAccepted</c> already
/// uses for console introductions, whose own remark is the argument:
/// <i>"a runner gains no new loop, no long poll and no listening socket, so its
/// outbound-only posture survives by construction."</i>
/// </para>
/// <para>
/// <b>"Push" is what it feels like to an operator; a carried offer is what it
/// is.</b> Saying so in a test rather than a comment is what keeps the next
/// person from building the channel.
/// </para>
/// </remarks>
public partial class AnOfferRidesThePollThatExistsTests
{
    [Test]
    public async Task The_heartbeat_response_is_what_carries_one()
    {
        var accepted = new HeartbeatAccepted
        {
            NextHeartbeatSeconds = 30,
            Offered = new OfferedConfiguration
            {
                Version = "offer@v1",
                OfferedAt = DateTimeOffset.UnixEpoch,
                Settings = [new OfferedSetting { Key = "stun-servers", Value = "stun:x:1" }],
            },
        };

        await Assert.That(accepted.Offered).IsNotNull();
        await Assert.That(accepted.Offered!.Settings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_heartbeat_that_carries_none_is_the_ordinary_one()
    {
        // Absent, not empty. A control plane offering nothing sends no member,
        // and a machine one version behind reads the response unchanged - the
        // absent-member hazard AbsentCollectionsSurviveTheWireTests exists for.
        var accepted = new HeartbeatAccepted { NextHeartbeatSeconds = 30 };

        await Assert.That(accepted.Offered).IsNull()
            .Because("nullable, because absence MEANS something here: nothing is offered.");
    }

    [Test]
    public async Task No_route_was_added_to_fetch_one()
    {
        // THE RATCHET, and it is about what did NOT happen. An endpoint for
        // configuration would be a second way to reach a machine and the start
        // of the channel this design exists to avoid.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
        var client = File.ReadAllText(Path.Combine(root, "Gg.Client", "ControlPlaneClient.cs"));

        var routes = Route().Matches(client).Select(m => m.Groups[1].Value).ToList();

        await Assert.That(routes).IsNotEmpty()
            .Because("a scan that found no routes would pass for a client full of them.");

        var offered = routes
            .Where(r => r.Contains("config", StringComparison.OrdinalIgnoreCase)
                     || r.Contains("offer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(offered).IsEmpty()
            .Because("an offer rides the heartbeat. A route of its own is a second way to "
                   + "reach a machine: " + string.Join(", ", offered));
    }

    [GeneratedRegex("\"(/v1/[^\"]*)\"")]
    private static partial Regex Route();
}
