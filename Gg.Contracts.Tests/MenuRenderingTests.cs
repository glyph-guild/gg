using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What a classifier is offered, rendered from what bounds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A FINDING ABOUT THE EXISTING FEATURE, NOT THIS ONE.</b> The nomination
/// tool's description says "One of the work kinds you were offered." The prompt
/// was subject, what-to-do-when-it-cannot, resumption and feedback. Nothing
/// offered one. An agent has been asked to name a work kind it was never shown,
/// with a guess or a lucky convention as its only route to a valid name.
/// </para>
/// <para>
/// <b>Extending selection without fixing that would have made it worse.</b> Two
/// more fields to guess at, and rule 8 turns a wrong guess into a refused flight
/// rather than a clamped one - so the agent would be punished for not knowing
/// something nobody told it.
/// </para>
/// <para>
/// <b>One source, rendered once.</b> The menu comes from the same destination
/// that bounds admission - <c>opens</c> becomes the kinds on offer,
/// <c>may-select</c> becomes the environments and repositories - so what the
/// agent is offered and what admission will accept cannot drift apart. A second
/// place listing them would be a menu that goes stale silently.
/// </para>
/// </remarks>
public class MenuRenderingTests
{
    private static Envelope With(params Destination[] destinations) => new()
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
                Moves = [LoopMoves.Read, LoopMoves.Propose],
                Budget = new LoopBudget { WallClock = "20m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [.. destinations],
    };

    private static Destination Opening(
        IReadOnlyList<string>? opens, DestinationSelection? maySelect = null) => new()
    {
        Id = "triage-opens-work",
        Kind = DestinationKinds.Flight,
        Requires = [],
        Opens = opens,
        MaySelect = maySelect,
    };

    [Test]
    public async Task The_work_kinds_a_destination_opens_are_offered_by_name()
    {
        var menu = EnvelopeText.RenderMenu(With(Opening(["implement", "research"])));

        await Assert.That(menu).IsNotNull();
        await Assert.That(menu!).Contains("implement");
        await Assert.That(menu!).Contains("research")
            .Because("the tool's own description promises the agent was offered these, and "
                   + "until now nothing offered them.");
    }

    [Test]
    public async Task The_environments_and_repositories_it_permits_are_offered_too()
    {
        var menu = EnvelopeText.RenderMenu(With(Opening(
            ["implement"],
            new DestinationSelection
            {
                Environments = ["dev", "staging"],
                Repositories = ["payments"],
            })));

        await Assert.That(menu!).Contains("dev");
        await Assert.That(menu!).Contains("staging");
        await Assert.That(menu!).Contains("payments");
    }

    [Test]
    public async Task A_destination_that_permits_no_selection_offers_only_the_kinds()
    {
        // S30.4-08, half of it. An agent given no menu is not asked to choose:
        // a destination bounding no environments must not produce a heading with
        // nothing under it, which reads as "choose from none" rather than as
        // "this is not a choice you have".
        var menu = EnvelopeText.RenderMenu(With(Opening(["implement"])));

        await Assert.That(menu!).Contains("implement");
        await Assert.That(menu!.Contains("environment", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
        await Assert.That(menu!.Contains("repositor", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    [Test]
    public async Task An_envelope_that_opens_nothing_renders_no_menu_at_all()
    {
        // The other half of S30.4-08, and the case that matters most: most
        // flights are not classify flights. Their prompt must be byte for byte
        // what it was before any of this existed.
        var ordinary = new Destination
        {
            Id = "forge",
            Kind = DestinationKinds.PullRequest,
            Requires = ["in-scope"],
        };

        await Assert.That(EnvelopeText.RenderMenu(With(ordinary))).IsNull();
    }

    [Test]
    public async Task An_empty_permitted_set_is_not_an_invitation_to_guess()
    {
        // Empty is a real statement - the tenant considered it and permits
        // nothing - and it renders no heading, exactly as null does. The
        // difference between them lives in the document, not in the prompt: an
        // agent told "environments: none" would reasonably try to name one.
        var menu = EnvelopeText.RenderMenu(With(Opening(
            ["implement"], new DestinationSelection { Environments = [] })));

        await Assert.That(menu!.Contains("environment", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    [Test]
    public async Task It_says_the_menu_is_the_whole_of_what_admission_accepts()
    {
        // Rule 8 refuses rather than clamps, so an agent that names something
        // outside the menu loses the flight rather than getting a near miss.
        // That is worth saying in the same breath as the list.
        var menu = EnvelopeText.RenderMenu(With(Opening(
            ["implement"], new DestinationSelection { Environments = ["dev"] })));

        await Assert.That(menu!).Contains("refused");
    }
}
