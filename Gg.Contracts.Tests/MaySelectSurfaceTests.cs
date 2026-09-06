using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The sets a destination permits a nomination to select from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only new control in part C, and it is a bound rather than a
/// capability.</b> A nomination already names a work kind against
/// <c>Opens</c>; an environment and a repository are the same shape - optional,
/// structured, declared rather than parsed. Membership validation is free
/// afterwards: an environment flows into the launch request the chart already
/// refuses if uncharted, and a repository into the one ingress already refuses
/// if unregistered. What this adds is the destination's own bound, which is
/// what stops a classifier sending work to production when the tenant meant it
/// to choose between two development environments.
/// </para>
/// <para>
/// <b>Refused, not clamped.</b> A nomination naming something outside these
/// sets opens nothing. Clamping to the nearest permitted value would be the
/// platform choosing where somebody else's work runs and reporting success.
/// </para>
/// <para>
/// <b>On a flight destination or nowhere</b>, the rule <c>Opens</c> and
/// <c>PreserveUnadmitted</c> already hold: only a flight destination opens
/// anything, so a selection bound on any other kind is a permission somebody
/// believes they granted and nothing will ever read.
/// </para>
/// </remarks>
public class MaySelectSurfaceTests
{
    private static Destination AFlightDestination(DestinationSelection? maySelect) => new()
    {
        Id = "triage-opens-work",
        Kind = DestinationKinds.Flight,
        Requires = [],
        Opens = ["implement"],
        MaySelect = maySelect,
    };

    private static Envelope Around(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "classify",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "20m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    [Test]
    public async Task It_names_two_sets_and_nothing_else()
    {
        // ENUMERATED EXACTLY, the way FlightNomination's own shape is. A member
        // added here is a dimension an agent may choose along, and the pressure
        // runs the same way it does on the nomination: every one somebody wants
        // makes the selection more useful and makes it configuration an agent
        // writes.
        var members = typeof(DestinationSelection)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(DestinationSelection.Environments),
            nameof(DestinationSelection.Repositories),
        });
    }

    [Test]
    public async Task A_selection_bound_belongs_only_on_a_destination_that_opens_flights()
    {
        // S30.4-05, and the PreserveUnadmitted precedent: a knob that silently
        // does nothing is a permission somebody believes they granted.
        var elsewhere = new Destination
        {
            Id = "forge",
            Kind = DestinationKinds.PullRequest,
            Requires = [],
            MaySelect = new DestinationSelection { Environments = ["dev"] },
        };

        var diagnosis = Envelope.Validate(Around(elsewhere));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains(DestinationKinds.PullRequest);
        await Assert.That(diagnosis!).Contains(DestinationKinds.Flight)
            .Because("the refusal has to say which kind does read it, or the author is told "
                   + "they are wrong and not what would be right.");
    }

    [Test]
    public async Task A_flight_destination_may_declare_one()
    {
        await Assert.That(Envelope.Validate(Around(AFlightDestination(
            new DestinationSelection
            {
                Environments = ["dev", "staging"],
                Repositories = ["payments"],
            })))).IsNull();
    }

    [Test]
    public async Task Declaring_none_is_the_ordinary_state()
    {
        // Null rather than empty sets. Most destinations bound no selection at
        // all, and an empty list is a different statement - it says an agent may
        // choose from nothing, which S30.4-08 turns into no menu rather than an
        // invitation to guess.
        var destination = AFlightDestination(maySelect: null);

        await Assert.That(destination.MaySelect).IsNull();
        await Assert.That(Envelope.Validate(Around(destination))).IsNull();
    }

    [Test]
    public async Task It_holds_nothing_that_could_widen_what_a_flight_may_do()
    {
        var members = typeof(DestinationSelection)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var forbidden in (string[])
            ["Moves", "Scope", "Budget", "WallClock", "Obligations", "Approver", "Requires",
             "Executor", "Opens", "WorkKinds", "Destinations"])
        {
            await Assert.That(members.Contains(forbidden, StringComparer.Ordinal)).IsFalse()
                .Because($"'{forbidden}' here would let a destination hand a classifier the "
                       + "regime its nomination runs under, which is the option this design "
                       + "rejected when it refused to put a move on the nomination itself.");
        }
    }
}
