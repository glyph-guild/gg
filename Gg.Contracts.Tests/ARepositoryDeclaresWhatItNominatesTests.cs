using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What a repository's pull requests may nominate, and what those draw on.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE BOUND IS A FLIGHT DESTINATION, NOT A MENU THAT RESEMBLES ONE.</b>
/// ADR-0022 § 5 states it for a watch and it is reached one nominator earlier
/// here: <c>opens:</c>, <c>may-select</c>, <c>opens-as</c> and
/// <c>requires</c>, the same record an agent's nominating flight carries. A
/// second menu-shaped member would need its own validation, its own direction
/// arms and its own way to be reached by a narrowing — which is three places
/// for two spellings to stop agreeing.
/// </para>
/// <para>
/// <b>The repository is the nominator, which is why it carries both.</b> The
/// bound is the destination; the bounds are the budget. § 5 lists a watch's
/// budget among its <i>bounds</i>, beside rate and active hours, rather than
/// on the destination — so the budget belongs to whoever nominates, and a
/// pull request's nominator is the repository it is in.
/// </para>
/// <para>
/// <b>ABSENT IS TODAY'S BEHAVIOUR, and this is stated rather than defaulted
/// past.</b> Every repository registered before this exists has no bound, and
/// each one nominates exactly what it opens now: an un-kinded flight under the
/// tenant's current envelope, drawing on no budget. A declared bound is the
/// word that ADDS a constraint, which is the exception <c>opens-as</c> made
/// and for its reason.
/// </para>
/// </remarks>
public class ARepositoryDeclaresWhatItNominatesTests
{
    private static RegisterRepositoryRequest ARegistration(
        Destination? nominates = null, NominationBudget? budget = null) => new()
    {
        Name = "payments",
        // NOT A REAL FORGE'S NAME. `NoSourceFileNamesAnIdentityProvider` refuses
        // one in this repository and it is right to: gg talks only to the
        // control plane, and a provider name in a public binary is that
        // boundary having leaked. The provider is a KEY the registrar chose,
        // which is exactly what makes a placeholder honest here.
        Provider = "a-forge",
        Id = "an-immutable-forge-id",
        Path = "acme/payments-service",
        Nominates = nominates,
        Budget = budget,
    };

    private static Destination ABound(string kind = DestinationKinds.Flight) => new()
    {
        Id = "what-a-pull-request-opens",
        Kind = kind,
        Requires = [],
        Opens = ["review"],
    };

    [Test]
    public async Task A_registration_with_no_bound_is_what_every_one_of_them_is_today()
    {
        // S38.3-06. The exception stated. A repository registered before this
        // member existed nominates what it opens now and draws on nothing, so
        // reading absent as unbounded changes no document anybody has written.
        var registration = ARegistration();

        await Assert.That(registration.Nominates).IsNull();
        await Assert.That(registration.Budget).IsNull();
        await Assert.That(RegisterRepositoryRequest.Validate(registration)).IsNull()
            .Because("a bound is what ADDS a constraint. Refusing a registration for not "
                   + "declaring one would refuse every registration in existence.");
    }

    [Test]
    public async Task The_bound_is_a_flight_destination_and_nothing_else()
    {
        // S38.3-01. `opens:`' own rule at the same door: only a flight opens
        // anything, so a bound of any other kind is a control that governs
        // nothing - and it reads to whoever wrote it as one they set.
        var refused = RegisterRepositoryRequest.Validate(
            ARegistration(ABound(kind: DestinationKinds.PullRequest)));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(DestinationKinds.PullRequest)
            .Because("the refusal names the kind, because an author reading it has to decide "
                   + "whether the kind is wrong or the member is.");
    }

    [Test]
    public async Task The_bound_is_held_to_the_same_rules_a_flights_destination_is()
    {
        // ONE SPELLING MEANS ONE VALIDATOR. `opens-as` reads two words and no
        // third; if this bound accepted a third, there would be two answers to
        // one question depending on where the destination was written down.
        var refused = RegisterRepositoryRequest.Validate(
            ARegistration(ABound() with { OpensAs = "sometimes" }));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("sometimes");
    }

    [Test]
    public async Task A_budget_is_a_count_of_flights_in_a_window()
    {
        // ADR-0022 section 4: denominated in what actually burns. Flights
        // opened is the one this slice counts, and the window is what makes it
        // a rate rather than a lifetime cap.
        var budget = new NominationBudget { Flights = 5, Window = "24h" };

        await Assert.That(RegisterRepositoryRequest.Validate(
            ARegistration(ABound(), budget))).IsNull();
    }

    [Test]
    public async Task A_budget_of_nothing_is_refused_rather_than_read_as_unbounded()
    {
        // ZERO AND ABSENT MUST NOT BE THE SAME VALUE. Absent is unbounded;
        // zero would be a repository that may nominate nothing at all, which
        // is a thing somebody might mean and must therefore not be reachable
        // by accident. It is refused until somebody asks for it by name.
        var refused = RegisterRepositoryRequest.Validate(
            ARegistration(ABound(), new NominationBudget { Flights = 0, Window = "24h" }));

        await Assert.That(refused).IsNotNull();
    }

    [Test]
    public async Task A_window_nobody_can_parse_is_refused_at_authoring()
    {
        // The duration grammar is deliberately narrow - whole seconds, minutes
        // or hours - and a window that parses into nothing would be a budget
        // that bounds nothing while reading as though it bounded something.
        var refused = RegisterRepositoryRequest.Validate(
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "a while" }));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("a while");
    }

    [Test]
    public async Task A_budget_without_a_bound_is_refused()
    {
        // A BUDGET IS A BOUND'S COMPANION, and one on its own is a control
        // over an act nothing declared. It reads as a limit somebody set; it
        // limits nothing, because an absent bound means the repository
        // nominates what it opens today.
        var refused = RegisterRepositoryRequest.Validate(
            ARegistration(budget: new NominationBudget { Flights = 5, Window = "24h" }));

        await Assert.That(refused).IsNotNull();
    }

    [Test]
    public async Task Widening_the_menu_is_a_widening_and_narrowing_it_is_not()
    {
        // S38.3-02. Reach added to a nominator nobody is watching is exactly
        // what a reviewer must be shown - and taking it away is not, because a
        // gate for removing reach is how a review practice gets abandoned.
        var widened = RepositoryDirection.Widening(
            ARegistration(ABound()),
            ARegistration(ABound() with { Opens = ["review", "implement"] }));

        await Assert.That(widened).IsNotNull()
            .Because("a repository whose pull requests may now open `implement` flights is "
                   + "reach nobody granted, arriving from a webhook.");

        var narrowed = RepositoryDirection.Widening(
            ARegistration(ABound() with { Opens = ["review", "implement"] }),
            ARegistration(ABound()));

        await Assert.That(narrowed).IsNull();
    }

    [Test]
    public async Task Raising_a_budget_is_a_widening_and_lowering_it_is_not()
    {
        // S38.3-02's other half, and the asymmetry is the same one. More
        // flights per window is more tokens spent with nobody watching;
        // fewer is somebody deciding to spend less.
        var raised = RepositoryDirection.Widening(
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "24h" }),
            ARegistration(ABound(), new NominationBudget { Flights = 50, Window = "24h" }));

        await Assert.That(raised).IsNotNull();
        await Assert.That(raised!.Field).Contains("budget");

        var lowered = RepositoryDirection.Widening(
            ARegistration(ABound(), new NominationBudget { Flights = 50, Window = "24h" }),
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "24h" }));

        await Assert.That(lowered).IsNull();
    }

    [Test]
    public async Task Removing_a_budget_is_a_widening_because_absent_means_unbounded()
    {
        // THE ONE A COMPARATOR GETS WRONG BY OMISSION. Absent is unbounded, so
        // deleting a line raises the ceiling to infinity - and a comparator
        // that only compared two numbers would read a deletion as no change at
        // all and let it through ungated.
        var removed = RepositoryDirection.Widening(
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "24h" }),
            ARegistration(ABound()));

        await Assert.That(removed).IsNotNull()
            .Because("absent means unbounded, so removing a budget is the largest widening "
                   + "this member can express.");
    }

    [Test]
    public async Task A_longer_window_for_the_same_count_is_a_tightening()
    {
        // The rate is what matters, not the numerator. Five flights a day
        // becomes five a week: the same count over more time is less reach,
        // and a comparator reading only `Flights` would call it no change.
        var slower = RepositoryDirection.Widening(
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "24h" }),
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "168h" }));

        await Assert.That(slower).IsNull();

        var faster = RepositoryDirection.Widening(
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "168h" }),
            ARegistration(ABound(), new NominationBudget { Flights = 5, Window = "24h" }));

        await Assert.That(faster).IsNotNull()
            .Because("five a day is five times the reach of five a week, and a comparator "
                   + "that read only the count would let the change through ungated.");
    }
}
