using Gg.Client;
using Gg.Console;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A flight names the repository the person chose.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.1-03, and slice 28's S28.5-02, which are the same criterion.</b>
/// <c>FlightCommands.FlyAsync</c> has taken a <c>repository</c> parameter the
/// whole time and neither console path passed it, so the console could not do
/// what <c>gg fly --repo</c> does. I had marked this step done; it was not.
/// </para>
/// <para>
/// <b>Both paths carry it, because there are two ways to open a flight.</b> A
/// pasted intent and a picked work item are different doors into the same
/// write, and a repository that crossed on one of them would be a setting that
/// works depending on how you started.
/// </para>
/// <para>
/// <b>The title still does not cross.</b> A repository is a thing a person
/// CHOSE from a list this console showed them; a title is something a tracker
/// said. The first is an instruction and the second is content.
/// </para>
/// </remarks>
public class TheChosenRepositoryCrossesTests
{
    private static RegisteredRepositories One() => new()
    {
        Repositories =
        [
            new RepositoryRegistered
            {
                Name = "widgets",
                Provider = "a-forge",
                Id = "r-1",
                Path = "acme/widgets",
                Credential = "local:acme/widgets",
                RegisteredBy = "somebody",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static AppState Chose() =>
        Reducer.RepositoryChosen(
            ConsoleProjection.Apply(new AppState(), new VerbResult.AirspaceRepositories(One())));

    private static AppState Picked(AppState chosen) =>
        Reducer.Browsed(
            chosen with { BrowseVisible = true },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

    [Test]
    public async Task Flying_a_picked_item_names_the_chosen_repository()
    {
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Picked(Chose()), actions);

        await Assert.That(actions.Tickets).Count().IsEqualTo(1);
        await Assert.That(actions.Tickets[0].Repository).IsEqualTo("acme/widgets");
    }

    [Test]
    public async Task With_nothing_chosen_the_envelope_still_decides()
    {
        // THE ORDINARY STATE, and it must stay reachable. Passing an empty
        // string rather than null would be the console asserting a repository
        // named "" - a refusal at the control plane for a choice nobody made.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Picked(new AppState()), actions);

        await Assert.That(actions.Tickets[0].Repository).IsNull();
    }

    [Test]
    public async Task A_pasted_intent_names_it_too()
    {
        // Two doors into one write. A repository that crossed on one of them
        // would be a setting that works depending on how you started.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.Opened(Chose(), actions, new ConsoleDoubles.NoEditor());

        await Assert.That(actions.Intents).Count().IsEqualTo(1);
        await Assert.That(actions.Intents[0].Repository).IsEqualTo("acme/widgets");
    }

    [Test]
    public async Task The_title_still_does_not_cross()
    {
        // A repository is what a person CHOSE from a list this console showed
        // them; a title is what a tracker said. One is an instruction, the
        // other is content, and only one of them belongs in a write.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Picked(Chose()), actions);

        var sent = string.Join(
            " ", actions.Tickets.Select(t => $"{t.Provider} {t.Id} {t.Repository}"));

        await Assert.That(sent).DoesNotContain("A draft job fails");
    }

    [Test]
    public async Task Confirming_a_second_flight_names_it_as_well()
    {
        // The confirmation path opens the flight later, from the question
        // rather than from the selection - so it has to carry the repository
        // that was chosen when the question was asked.
        var actions = new ConsoleDoubles.Records(alreadyFlown: "gg-14 is already open.");

        var asked = ConsoleLoop.FlewPicked(Picked(Chose()), actions);
        var opened = ConsoleLoop.ConfirmedFlight(asked, actions);

        await Assert.That(actions.Tickets).Count().IsEqualTo(1);
        await Assert.That(actions.Tickets[0].Repository).IsEqualTo("acme/widgets");
        await Assert.That(opened.PendingFlight).IsNull();
    }
}
