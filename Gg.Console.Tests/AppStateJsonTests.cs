namespace Gg.Console.Tests;

public class AppStateJsonTests
{
    [Test]
    public async Task StateRoundTripsThroughJsonUnchanged()
    {
        var state = new AppState
        {
            Mode = UiMode.Help,
            FocusedPane = PaneId.Notes,
            Flights = ["flight-1", "flight-2", "flight-3"],
            SelectedFlight = 1,
            Notes = "line one\nline two",
        };

        var back = AppStateJson.Deserialize(AppStateJson.Serialize(state));

        await Assert.That(back.Mode).IsEqualTo(state.Mode);
        await Assert.That(back.FocusedPane).IsEqualTo(state.FocusedPane);
        await Assert.That(back.Flights).IsEquivalentTo(state.Flights);
        await Assert.That(back.SelectedFlight).IsEqualTo(state.SelectedFlight);
        await Assert.That(back.Notes).IsEqualTo(state.Notes);
        await Assert.That(AppStateJson.Serialize(back)).IsEqualTo(AppStateJson.Serialize(state));
    }
}
