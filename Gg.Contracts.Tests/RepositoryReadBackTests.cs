namespace Gg.Contracts.Tests;

/// <summary>
/// What a registered repository reports about itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registered is not invoked.</b> A declaration a tenant can write and cannot
/// read back is one they cannot check, and the whole of ADR-0018 § 2 rests on a
/// person being able to see which repositories may speak.
/// <c>gg airspace repositories</c> renders this list.
/// </para>
/// <para>
/// <b>NOT <c>gg airspace pull</c>, which this comment used to say.</b> Measured
/// while slice thirty-eight added two members here: <c>AirspaceEstate</c> holds
/// <c>Documents</c> and <c>Strategies</c> and no repositories at all, so a pull
/// has never written a repository into a working copy. The correction matters
/// because the wrong sentence makes every member here look like it has a
/// round trip through apply that it does not have — what it actually has is a
/// person reading a list.
/// </para>
/// <para>
/// <b>Nullable here and nullable on the request, unlike <c>Credential</c>.</b>
/// That member is required on the way out because an absent declaration and a
/// declared <c>required</c> are the same fact. This one is the opposite: absent
/// and declared are DIFFERENT facts — off versus on — so the absence has to
/// survive the round trip as an absence.
/// </para>
/// </remarks>
public class RepositoryReadBackTests
{
    private static Destination ABound() => new()
    {
        Id = "what-a-pull-request-opens",
        Kind = DestinationKinds.Flight,
        Requires = [],
        Opens = ["review"],
    };

    private static RepositoryRegistered Registered(
        string? narrowings, Destination? nominates = null, NominationBudget? budget = null) => new()
    {
        Name = "payments",
        Provider = "forge.example",
        Id = "F_payments01",
        Path = "acme/payments-service",
        Credential = RepositoryCredentialModes.Required,
        Narrowings = narrowings,
        Nominates = nominates,
        Budget = budget,
        RegisteredBy = "an-architect",
        RegisteredAt = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero),
    };

    [Test]
    public async Task A_declared_directory_reads_back()
    {
        await Assert.That(Registered(".goodgrief/narrowings/").Narrowings)
            .IsEqualTo(".goodgrief/narrowings/");
    }

    [Test]
    public async Task Off_reads_back_as_off_rather_than_as_a_blank()
    {
        // The distinction the whole member exists to preserve. A reader that
        // cannot tell "declares nothing" from "declares the root of the tree"
        // cannot tell a repository that is not governed from one whose every
        // file is policy.
        await Assert.That(Registered(null).Narrowings).IsNull();
    }

    [Test]
    public async Task The_request_and_the_answer_agree_about_what_off_looks_like()
    {
        // Both nullable, deliberately, and NOT the arrangement Credential has
        // one member up: there an absent declaration and a declared value are
        // the same fact, so the answer is required. Here they are different
        // facts, so the answer is not.
        await Assert.That(typeof(RegisterRepositoryRequest)
            .GetProperty(nameof(RegisterRepositoryRequest.Narrowings))!.PropertyType)
            .IsEqualTo(typeof(string));
        await Assert.That(typeof(RepositoryRegistered)
            .GetProperty(nameof(RepositoryRegistered.Narrowings))!.PropertyType)
            .IsEqualTo(typeof(string));

        await Assert.That(RepositoryNarrowings.Invalid(Registered(null).Narrowings)).IsNull()
            .Because("whatever the answer carries for 'off' must be a legal declaration to "
                   + "send straight back, or a round trip through pull and apply refuses "
                   + "what it was just told.");
    }

    [Test]
    public async Task What_a_repository_nominates_reads_back_beside_what_it_may_spend()
    {
        // A CONTROL SOMEBODY SET AND CANNOT SEE IS A CONTROL THEY CANNOT
        // CHECK, which is this class's own opening sentence one member later.
        // The bound decides what a webhook nobody was watching may open in
        // this repository, and the budget decides how often - and until they
        // read back, the only way to find out what a repository nominates is
        // to push a commit and watch.
        var bound = ABound();
        var budget = new NominationBudget { Flights = 5, Window = "24h" };

        var registered = Registered(null, bound, budget);

        await Assert.That(registered.Nominates).IsEqualTo(bound);
        await Assert.That(registered.Budget).IsEqualTo(budget);
    }

    [Test]
    public async Task Absent_reads_back_as_absent_for_both_of_them()
    {
        // THE SAME ASYMMETRY `Narrowings` HAS, and for the same reason. Absent
        // and declared are DIFFERENT facts here - unbounded versus bounded -
        // so the absence has to survive the round trip as an absence, or a
        // reader cannot tell a repository nobody has bounded from one whose
        // bound they simply cannot see.
        var registered = Registered(null);

        await Assert.That(registered.Nominates).IsNull();
        await Assert.That(registered.Budget).IsNull();

        await Assert.That(Nullable.GetUnderlyingType(
            typeof(RepositoryRegistered).GetProperty(
                nameof(RepositoryRegistered.Budget))!.PropertyType) is null)
            .IsTrue()
            .Because("a reference type carries its own absence. What matters is that the "
                   + "member is not required, which is what lets every registration made "
                   + "before this existed read back honestly.");
    }

    [Test]
    public async Task The_request_and_the_answer_agree_about_the_bound_too()
    {
        // ONE SHAPE IN AND THE SAME SHAPE OUT. A listing that reported a bound
        // in some other form would be a second spelling of a governance
        // control, and the two would stop agreeing the first time either moved.
        await Assert.That(typeof(RegisterRepositoryRequest)
            .GetProperty(nameof(RegisterRepositoryRequest.Nominates))!.PropertyType)
            .IsEqualTo(typeof(RepositoryRegistered)
                .GetProperty(nameof(RepositoryRegistered.Nominates))!.PropertyType);

        await Assert.That(typeof(RegisterRepositoryRequest)
            .GetProperty(nameof(RegisterRepositoryRequest.Budget))!.PropertyType)
            .IsEqualTo(typeof(RepositoryRegistered)
                .GetProperty(nameof(RepositoryRegistered.Budget))!.PropertyType);

        await Assert.That(RegisterRepositoryRequest.Validate(new RegisterRepositoryRequest
        {
            Name = "payments",
            Provider = "a-forge",
            Id = "F_payments01",
            Path = "acme/payments-service",
            Nominates = Registered(null, ABound()).Nominates,
            Budget = Registered(null, ABound(), new NominationBudget
            {
                Flights = 5,
                Window = "24h",
            }).Budget,
        })).IsNull()
            .Because("what the answer carries must be a legal declaration to send straight "
                   + "back, which is the rule `Narrowings` states three tests up and the "
                   + "reason both are the same type rather than merely similar.");
    }
}
