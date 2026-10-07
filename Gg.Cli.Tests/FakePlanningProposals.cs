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

    private int? _refuseAfter;
    private string? _refusal;

    /// <summary>Answers the first <paramref name="answered"/> proposals, then refuses with <paramref name="why"/>.</summary>
    internal void RefuseAfter(int answered, string why) => (_refuseAfter, _refusal) = (answered, why);

    public Task<ItineraryProposed> ProposeAsync(ItineraryProposal proposal, CancellationToken cancellationToken = default)
    {
        Sent.Add(proposal);
        if (_refuseAfter is { } after && Sent.Count > after)
        {
            throw new InvalidOperationException(_refusal);
        }

        return Task.FromResult(Answer);
    }
}
