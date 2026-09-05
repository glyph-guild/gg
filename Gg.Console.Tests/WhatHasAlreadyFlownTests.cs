using Gg.Console;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A work item that already has a flight says so, in the list.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.2-02, -03 and -04, and they cost nothing.</b> I recorded these as
/// unbuilt because the obvious version is one request per row — and it is not
/// needed. <c>FlightIntent</c> carries <c>Provider</c> and <c>Id</c>, and the
/// boot already holds every flight in <c>AppState.Flights</c>. So this is a
/// local join and **zero extra requests**, which is a different answer from
/// the one I gave.
/// </para>
/// <para>
/// <b>Computed, never stored.</b> Denormalising the match into
/// <c>BrowseRow</c> would put two copies of one fact in the model and let them
/// disagree after any refresh. The pane derives it from the two lists it
/// already has.
/// </para>
/// <para>
/// <b>A uri-intent flight correlates through nothing, and that is SAID.</b> A
/// flight opened from a pasted url has no provider and no id, so it can never
/// match a row here — and a list that silently showed nothing would be
/// reporting an absence it cannot actually see.
/// </para>
/// </remarks>
public class WhatHasAlreadyFlownTests
{
    private static FlightSummary AFlight(
        string number, string? provider, string? id, int day, string kind = "ticket") => new()
    {
        FlightId = "f-" + number,
        FlightNumber = number,
        Name = "whatever it was called",
        Intent = new FlightIntent { Kind = kind, Provider = provider, Id = id },
        CreatedAt = DateTimeOffset.UnixEpoch.AddDays(day),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1",
        EnvelopeVersion = "1",
        Attempts = 0,
        Facts = [],
    };

    private static AppState Browsing(params FlightSummary[] flights)
    {
        var state = Reducer.Browsed(
            new AppState { BrowseVisible = true, Flights = new FlightList { Flights = flights } },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [
                    new WorkItemSummary("18398", "A draft job fails", "New", "", null),
                    new WorkItemSummary("18471", "Story 3", "Active", "", null),
                ],
                null)));

        return state;
    }

    [Test]
    public async Task An_item_that_already_flew_shows_the_flight()
    {
        var text = PaneText.Browse(Browsing(AFlight("gg-14", "a-tracker", "18398", 1)));

        await Assert.That(text).Contains("gg-14");
    }

    [Test]
    public async Task An_item_that_has_not_flown_shows_nothing_of_the_kind()
    {
        var text = PaneText.Browse(Browsing(AFlight("gg-14", "a-tracker", "18398", 1)));
        var second = text.Split('\n').Single(line => line.Contains("18471"));

        await Assert.That(second).DoesNotContain("gg-")
            .Because("a marker on every row is a marker that says nothing.");
    }

    [Test]
    public async Task A_flight_for_another_tracker_does_not_count()
    {
        // The id alone is not the key. Two trackers can both hold an item
        // 18398, and matching on the number would attribute somebody else's
        // flight to this row.
        var text = PaneText.Browse(Browsing(AFlight("gg-14", "another", "18398", 1)));

        await Assert.That(text).DoesNotContain("gg-14");
    }

    [Test]
    public async Task Several_flights_are_all_shown_oldest_first()
    {
        // The correlation surface's own ordering, so a classify flight and what
        // it opened read as one thread rather than in whatever order the
        // control plane happened to answer.
        var text = PaneText.Browse(Browsing(
            AFlight("gg-20", "a-tracker", "18398", 9),
            AFlight("gg-14", "a-tracker", "18398", 1)));

        var row = text.Split('\n').Single(line => line.Contains("18398"));

        await Assert.That(row.IndexOf("gg-14", StringComparison.Ordinal))
            .IsLessThan(row.IndexOf("gg-20", StringComparison.Ordinal));
    }

    [Test]
    public async Task A_uri_intent_flight_correlates_through_nothing_and_the_pane_says_so()
    {
        // IT CANNOT BE MATCHED AND MUST NOT BE SILENT. `?intent=` takes
        // provider#id only, so a flight opened from a pasted url is invisible
        // to this join - and a list that showed nothing would be reporting an
        // absence it cannot see.
        var text = PaneText.Browse(Browsing(
            AFlight("gg-30", provider: null, id: null, day: 2, kind: "uri")));

        await Assert.That(text).DoesNotContain("gg-30");
        await Assert.That(text).Contains("pasted url")
            .Because("an absence this list cannot see has to be stated, not implied.");
    }

    [Test]
    public async Task With_no_uri_flights_the_caveat_is_not_printed()
    {
        // A footnote about a case that does not apply is noise that teaches
        // people to stop reading footnotes.
        var text = PaneText.Browse(Browsing(AFlight("gg-14", "a-tracker", "18398", 1)));

        await Assert.That(text).DoesNotContain("pasted url");
    }

    [Test]
    public async Task Nothing_fetched_yet_is_not_the_same_as_nothing_flown()
    {
        // Before the boot has answered, Flights is null. Rendering that as "no
        // flights" would tell a person their work has never been flown when
        // the truth is that nobody has looked.
        var state = Reducer.Browsed(
            new AppState { BrowseVisible = true },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

        await Assert.That(PaneText.Browse(state)).Contains("18398");
        await Assert.That(PaneText.Browse(state)).DoesNotContain("pasted url");
    }
}
