using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>The control plane's proposal door, answered in memory, and every proposal it was sent.</summary>
internal sealed class FakePlanningProposals : IPlanningProposals
{
    internal List<ItineraryProposal> Sent { get; } = [];

    internal ItineraryProposed Answer { get; set; } = new()
    {
        Itinerary = "ITN-7",
        Pass = "0199a3b1-0000-7000-8000-000000000001",
        Gates = [new LegGate { ObligationId = "plan-reviewed", Approver = "platform-owner" }],
    };

    public Task<ItineraryProposed> ProposeAsync(ItineraryProposal proposal, CancellationToken cancellationToken = default)
    {
        Sent.Add(proposal);
        return Task.FromResult(Answer);
    }
}
