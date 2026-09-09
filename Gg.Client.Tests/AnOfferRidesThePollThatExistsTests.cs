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
/// <para>
/// <b>The ratchet below was pointed at the wrong file, and its sentence nearly
/// refused the right answer.</b> It scanned <c>ControlPlaneClient</c> — the
/// CONSOLE's client — while saying "a second way to reach a machine". Reach is
/// inbound. A person typing <c>gg config offered</c> is the machine starting the
/// conversation, so it adds no reach at all, and refusing it on this rule would
/// have left the directed tier permanently unexercisable: the keys that need a
/// person arrive on a runner's heartbeat, and <c>Gg.Runner</c> cannot reference
/// <c>Gg.Client</c>.
/// </para>
/// <para>
/// <b>So it now guards the machine the rule is about.</b>
/// <c>RunnerProtocolClient</c> is the outbound-only half, and a configuration
/// route there would be a runner reaching for settings rather than reading what
/// rode its poll. Who may fetch an offer at all is asserted one level up, on the
/// declaration itself, by <c>AWaitingOfferIsFetchedByAPersonTests</c>.
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

    /// <summary>Every /v1 path a source file mentions.</summary>
    /// <remarks>
    /// Reads a sibling project's source from disk on purpose. The claim is about
    /// what the RUNNER may call, and a reference to <c>Gg.Runner</c> from here
    /// would be the edge <c>ProjectReferenceTests</c> refuses.
    /// </remarks>
    private static List<string> RoutesIn(string project, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return [.. Route()
            .Matches(File.ReadAllText(Path.Combine(root, project, file)))
            .Select(m => m.Groups[1].Value)];
    }

    [Test]
    public async Task The_runner_gained_no_route_to_fetch_one()
    {
        // THE RATCHET, and it is about what did NOT happen. The runner is the
        // machine nothing may reach; a configuration route in its client would
        // be it reaching for settings rather than reading what rode the poll it
        // was already making.
        var routes = RoutesIn("Gg.Runner", "RunnerProtocolClient.cs");

        await Assert.That(routes).IsNotEmpty()
            .Because("a scan that found no routes would pass for a client full of them.");

        var reaching = routes
            .Where(r => r.Contains("config", StringComparison.OrdinalIgnoreCase)
                     || r.Contains("offer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(reaching).IsEmpty()
            .Because("a runner takes what rides its heartbeat. A route of its own is the "
                   + "start of the channel this design exists to avoid: "
                   + string.Join(", ", reaching));
    }

    [Test]
    public async Task The_console_has_exactly_one_and_it_is_the_declared_read()
    {
        // A PERSON MAY ASK, ONCE. The verb is why the directed tier is
        // exercisable at all, so the console's client does have a route here -
        // and one is the number, because a second would be a way to reach this
        // that whoever added it chose the shape of.
        var offered = RoutesIn("Gg.Client", "ControlPlaneClient.cs")
            .Where(r => r.Contains("config", StringComparison.OrdinalIgnoreCase)
                     || r.Contains("offer", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offered).IsEquivalentTo((string[])["/v1/configuration/offered"])
            .Because("the declared read, and nothing beside it: "
                   + string.Join(", ", offered));
    }

    [GeneratedRegex("\"(/v1/[^\"]*)\"")]
    private static partial Regex Route();
}
