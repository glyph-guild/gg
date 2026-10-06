using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>The control plane's two planning reads, answered in memory, and every call they were asked.</summary>
internal sealed class FakePlanningReads : IPlanningReads
{
    internal ItineraryMenu Menu { get; set; } = new()
    {
        Planner = "plan",
        DestinationId = "the-plan",
        WorkKinds = ["implement", "triage"],
        Repositories = ["JDNext", "agile-cortex"],
        Environments = ["dev"],
    };

    /// <summary>When set, the menu read throws this instead of answering.</summary>
    internal Exception? MenuFails { get; set; }

    /// <summary>When set, the check throws this instead of answering.</summary>
    internal Exception? CheckFails { get; set; }

    /// <summary>The verdict each leg is given, by subject; absent subjects open.</summary>
    internal Dictionary<string, (string Verdict, string Reason)> Verdicts { get; } = [];

    internal List<string> Asked { get; } = [];

    public Task<ItineraryMenu> MenuAsync(string planner, CancellationToken cancellationToken = default)
    {
        Asked.Add("menu " + planner);
        return MenuFails is { } failure ? Task.FromException<ItineraryMenu>(failure) : Task.FromResult(Menu);
    }

    public Task<ItineraryCheck> CheckAsync(ItineraryDraft draft, CancellationToken cancellationToken = default)
    {
        Asked.Add("check " + draft.Legs.Count);
        if (CheckFails is { } failure)
        {
            return Task.FromException<ItineraryCheck>(failure);
        }

        return Task.FromResult(new ItineraryCheck
        {
            Planner = draft.Planner,
            DestinationId = "the-plan",
            Legs = [.. draft.Legs.Select(leg => Verdicts.TryGetValue(leg.Subject!, out var given)
                ? Leg(leg, given.Verdict, given.Reason)
                : Leg(leg, LegVerdicts.Opens, $"A '{leg.WorkKind}' flight would open."))],
        });
    }

    private static LegCheck Leg(FlightNomination leg, string verdict, string reason) => new()
    {
        Subject = leg.Subject!,
        WorkKind = leg.WorkKind,
        Verdict = verdict,
        Reason = reason,
        Obligations = [],
        Gates = [],
        PassedOver = [],
    };
}
