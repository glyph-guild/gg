namespace Gg.Console.Tests;

/// <summary>
/// S53.6-03. Something asks for the tab's read, so a console that has never
/// opened it still has it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE ONE FAILURE NOTHING ELSE CATCHES.</b> Every other ratchet a tab
/// trips is a switch that throws: miss the arm and the console dies loudly on
/// a frame. The read is the opposite. <c>ConsoleRefresh.ForTabAsync</c> ends in
/// <c>_ => Nothing</c>, so a tab with no arm simply never fetches - the state
/// member stays null, <c>HasRead</c> stays false, and the pane says "reading
/// the plans…" for ever. Nothing throws, nothing fails, and the tab is dead.
/// </para>
/// <para>
/// <b>Asserted over the source, because the alternative is a live control
/// plane.</b> What has to be true is that the refresh NAMES this tab - and a
/// test that started a console to find out would be asserting the network.
/// </para>
/// <para>
/// <b>And the arrival is the other half.</b> A read nobody asks for is as dead
/// as a read that does not exist, so the reducer must mark the refresh wanted
/// when somebody lands on an unread tab. That half is behaviour and is tested
/// as behaviour.
/// </para>
/// </remarks>
public class SomethingAsksForTheItinerariesReadTests
{
    private static string Source(string project, params string[] path) =>
        File.ReadAllText(Path.Combine([Root(), project, .. path]));

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    [Test]
    public async Task The_refresh_names_this_tab()
    {
        var refresh = Source("Gg.Console", "ConsoleRefresh.cs");

        var from = refresh.IndexOf("return tab switch", StringComparison.Ordinal);
        await Assert.That(from).IsGreaterThan(0)
            .Because("this test says nothing if the switch it reads has been renamed.");

        var arms = refresh[from..refresh.IndexOf("_ => Nothing", from, StringComparison.Ordinal)];

        await Assert.That(arms).Contains("TabId.Itineraries")
            .Because("without an arm the fall-through answers Nothing, the state stays null, "
                   + "and the pane says 'reading the plans' for ever - with nothing throwing "
                   + "and no other test failing. That is the dead tab.");
    }

    [Test]
    public async Task The_reader_it_names_exists_and_reaches_the_client()
    {
        // AN ARM THAT CALLED NOTHING WOULD PASS THE TEST ABOVE. The chain is
        // ConsoleData -> FlightCommands -> ControlPlaneClient, and each link
        // has to be there or the arm names a method that does not fetch.
        await Assert.That(Source("Gg.Console", "ConsoleData.cs"))
            .Contains("ItinerariesAsync");

        await Assert.That(Source("Gg.Client", "FlightCommands.cs"))
            .Contains("GetItinerariesAsync")
            .Because("the command wrapper must reach the client method that hits "
                   + "/v1/itineraries, or the read fetches nothing.");

        await Assert.That(Source("Gg.Client", "ControlPlaneClient.cs"))
            .Contains("\"/v1/itineraries\"")
            .Because("and that method must ask the door the contract declares.");
    }

    [Test]
    public async Task What_comes_back_is_projected_onto_the_tab()
    {
        // THE LAST LINK, and the one ProjectionParityTests guards generally: a
        // VerbResult with no arm in ConsoleData is a fetch whose answer is
        // dropped on the floor, which looks exactly like a fetch that never
        // happened.
        var data = Source("Gg.Console", "ConsoleData.cs");

        await Assert.That(data).Contains("VerbResult.Itineraries");
        await Assert.That(data).Contains("Itineraries = plans.Value");
    }

    [Test]
    public async Task Arriving_at_it_unread_asks_for_the_read()
    {
        // THE BEHAVIOURAL HALF. The board does the same: nothing fetches plans
        // at boot, so landing on the tab is what makes the request happen -
        // and a console that had never opened it would otherwise wait for a
        // refresh tick that only refreshes the tab in front of the person.
        var arrived = Reducer.Reduce(new AppState(), Command.ShowItinerariesTab);

        await Assert.That(arrived.ActiveTab).IsEqualTo(TabId.Itineraries);

        await Assert.That(arrived.Refresh.Wanted).IsTrue()
            .Because("arriving at an unread tab is what asks for its read, and a tab that "
                   + "never asks shows its waiting sentence for ever.");
    }

    [Test]
    public async Task Arriving_at_it_read_asks_for_nothing()
    {
        // The pair, because only the pair is the rule: a tab that asked again
        // on every arrival would refetch a page a person is already reading,
        // and the cursor would jump under them.
        var read = new AppState
        {
            Itineraries = new Gg.Contracts.BoardPage
            {
                Nominations = [],
                IncludedEnded = true,
            },
        };

        await Assert.That(Reducer.Reduce(read, Command.ShowItinerariesTab).Refresh.Wanted)
            .IsFalse();
    }
}
