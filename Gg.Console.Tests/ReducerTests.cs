namespace Gg.Console.Tests;

public class ReducerTests
{
    [Test]
    public async Task ToggleHelpOpensAndCloses()
    {
        var open = Reducer.Reduce(new AppState(), Command.ToggleHelp);
        await Assert.That(open.Mode).IsEqualTo(UiMode.Help);
        await Assert.That(Reducer.Reduce(open, Command.ToggleHelp).Mode).IsEqualTo(UiMode.Normal);
    }

    [Test]
    public async Task FocusNextPaneCyclesBothPanes()
    {
        var state = new AppState();
        var next = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(next.FocusedPane).IsEqualTo(PaneId.Notes);
        await Assert.That(Reducer.Reduce(next, Command.FocusNextPane).FocusedPane).IsEqualTo(PaneId.Flights);
    }

    [Test]
    public async Task ShellHandledCommandsLeaveStateUntouched()
    {
        var state = new AppState { Notes = "keep me", SelectedFlight = 2 };
        await Assert.That(Reducer.Reduce(state, Command.Quit)).IsEqualTo(state);
        await Assert.That(Reducer.Reduce(state, Command.OpenEditor)).IsEqualTo(state);
    }
}
