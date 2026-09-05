using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// What a person can fly against, and what has already been flown.
/// </summary>
/// <remarks>
/// <para>
/// <b>The half of a browser that needs no forge reach at all.</b> It answers
/// the question a person in this product actually has - <i>what have I already
/// flown, and what is still waiting?</i> - from reads that do not touch a
/// tracker: the tenant's registered repositories, and the intent correlation.
/// </para>
/// <para>
/// <b>One of the two reads was not wired, which the plan had the other way
/// round.</b> The correlation was complete on both sides. The repositories read
/// existed only on the SERVER: the console wraps <c>AirspaceAsync</c>, which is
/// the topology - envelope names and roles - and nothing in this binary called
/// <c>/v1/airspace/repositories</c> at all.
/// </para>
/// </remarks>
public class PaneContentTests
{
    private static RegisteredRepositories Two() => new()
    {
        Repositories =
        [
            new RepositoryRegistered
            {
                Name = "payments",
                Provider = "atracker",
                Id = "1",
                Path = "acme/payments",
                Credential = RepositoryCredentialModes.Required,
                Narrowings = null,
                Ref = "refs/heads/main",
                RegisteredBy = "somebody",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
            new RepositoryRegistered
            {
                Name = "widgets",
                Provider = "atracker",
                Id = "2",
                Path = "acme/widgets",
                Credential = RepositoryCredentialModes.None,
                Narrowings = null,
                RegisteredBy = "somebody",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    // ---- S29.2-01 ----

    [Test]
    public async Task The_registered_repositories_are_shown()
    {
        var text = VerbOutput.ToText(new VerbResult.AirspaceRepositories(Two()));

        await Assert.That(text).Contains("payments");
        await Assert.That(text).Contains("acme/widgets")
            .Because("the path is what a person matches against what they see in a forge; "
                   + "the registered name alone is a local alias.");
        await Assert.That(text).Contains("@refs/heads/main")
            .Because("a pinned ref decides what a flight starts from, and a listing that "
                   + "hides it shows two repositories that look identical and are not.");
    }

    [Test]
    public async Task An_empty_registry_says_so_rather_than_answering_blank()
    {
        var text = VerbOutput.ToText(
            new VerbResult.AirspaceRepositories(new RegisteredRepositories { Repositories = [] }));

        await Assert.That(text).Contains("No repositories are registered")
            .Because("a tenant with nothing registered and a tenant whose read failed look "
                   + "identical as a blank answer, and only one of them is a next action.");
        await Assert.That(text).Contains("gg airspace register")
            .Because("saying what is missing without saying how to supply it is half an answer.");
    }

    [Test]
    public async Task The_repositories_result_round_trips_as_json()
    {
        // The kind is dispatched through a switch whose default THROWS, so a
        // result with no arms is loud rather than silent - and this is the test
        // that would have caught two of the three arms being added and not the
        // third.
        var json = VerbOutput.ToJson(new VerbResult.AirspaceRepositories(Two()));
        var back = VerbOutput.Parse(VerbResultKinds.AirspaceRepositories, json);

        await Assert.That(back).IsTypeOf<VerbResult.AirspaceRepositories>();
        await Assert.That(((VerbResult.AirspaceRepositories)back).Value.Repositories.Count)
            .IsEqualTo(2);
    }

    // ---- S29.2-02, -03 and -04: what has already been flown ----

    [Test]
    public async Task A_work_item_correlates_through_provider_and_id()
    {
        // The console asks with the ticket's two halves joined the way the
        // control plane parses them, and PastedIntent is what produced them
        // from something a person pasted.
        var read = PastedIntent.Of("atracker#18398");

        await Assert.That(read.Provider).IsEqualTo("atracker");
        await Assert.That(read.Id).IsEqualTo("18398");
        await Assert.That($"{read.Provider}#{read.Id}").IsEqualTo("atracker#18398")
            .Because("?intent= takes provider#id whole and escapes it, so the two halves have "
                   + "to rejoin exactly as they were split or the correlation asks about a "
                   + "different work item.");
    }

    [Test]
    public async Task Several_flights_for_one_item_are_all_shown_oldest_first()
    {
        var flights = new FlightList
        {
            Flights =
            [
                Flown("GG-9", "2026-09-01T10:00:00Z"),
                Flown("GG-14", "2026-09-03T10:00:00Z"),
            ],
        };

        var text = VerbOutput.ToText(new VerbResult.Flights(flights));

        await Assert.That(text).Contains("GG-9");
        await Assert.That(text).Contains("GG-14")
            .Because("a classify flight and what it opened are one thread, and showing only "
                   + "the newest would hide the half that explains the other.");
        await Assert.That(text.IndexOf("GG-9", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("GG-14", StringComparison.Ordinal))
            .Because("oldest first is the correlation surface's own ordering, so the thread "
                   + "reads in the order it happened.");
    }

    [Test]
    public async Task A_uri_intent_correlates_through_nothing_and_that_is_a_stated_limit()
    {
        // S29.2-04. ?intent= parses provider#id ONLY. A flight opened from a
        // pasted URL is invisible to this query, so a surface that answered "no
        // flights" would be reporting an absence it cannot actually see.
        var read = PastedIntent.Of("https://tracker.invalid/x/_workitems/edit/18398");

        await Assert.That(read.Uri).IsNotNull();
        await Assert.That(read.Provider).IsNull()
            .Because("a uri intent has no provider and no id, which is exactly why it cannot "
                   + "be correlated - and the surface has to say that rather than show an "
                   + "empty list that reads as 'never flown'.");
    }

    private static FlightSummary Flown(string number, string created) => new()
    {
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = number,
        Name = "a flight",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Ticket, Text = null },
        CreatedAt = DateTimeOffset.Parse(created, System.Globalization.CultureInfo.InvariantCulture),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.11.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v1",
        Attempts = 1,
        Facts = [],
    };
}
