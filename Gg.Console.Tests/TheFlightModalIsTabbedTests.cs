namespace Gg.Console.Tests;

/// <summary>
/// The flight modal has two tabs, and evidence is the second one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A flight's evidence belongs where the flight is.</b> The evidence pane
/// renders <c>AppState.Payload</c>, which is already per-flight - its own empty
/// sentence is "Nothing is waiting on you for THIS flight" - so it was a
/// top-level tab answering a question about whatever the cursor happened to be
/// on. Under the flight it is about the thing named in the title bar.
/// </para>
/// <para>
/// <b>The selected tab is model state, not view state.</b> This console tears
/// the UI down and rebuilds every view from <see cref="AppState"/> whenever the
/// terminal has to be given up; a tab remembered only by the widget would snap
/// back to the first one every time somebody pressed a key that ends a session.
/// That is the rule the whole console is built on and this is not the exception
/// to it.
/// </para>
/// <para>
/// <b>What this does NOT fix, stated so the tab is not mistaken for a
/// feature.</b> Nothing assigns <c>Payload</c>. The control plane assembles a
/// <c>GateEvidencePayload</c> in <c>ObligationReceptor</c>, uses it only to
/// decide whether to halt, and discards it; no endpoint serves one, so the
/// second tab renders its empty sentence for everybody. Moving the pane does
/// not fill it, and <c>StateAssignmentTests</c> still carries the entry saying
/// so.
/// </para>
/// </remarks>
public class TheFlightModalIsTabbedTests
{
    private static AppState Showing() => new()
    {
        Mode = UiMode.FlightDetail,
        Queue = [],
    };

    [Test]
    public async Task A_flight_opens_on_its_details()
    {
        var opened = Reducer.Reduce(new AppState(), Command.ShowFlight);

        await Assert.That(opened.FlightTab).IsEqualTo(FlightTab.Details)
            .Because("the detail is what the key was pressed for; evidence is the second "
                   + "thing somebody goes looking for, never the first thing they are shown.");
    }

    [Test]
    public async Task Tab_moves_to_the_evidence_and_back()
    {
        var evidence = Reducer.Reduce(Showing(), Command.NextFlightTab);

        await Assert.That(evidence.FlightTab).IsEqualTo(FlightTab.Evidence);

        var back = Reducer.Reduce(evidence, Command.NextFlightTab);

        await Assert.That(back.FlightTab).IsEqualTo(FlightTab.Details)
            .Because("two tabs and one key, so the key has to come back - a person who "
                   + "overshoots with no way back is a person stuck in a modal.");
    }

    [Test]
    public async Task Reopening_a_flight_starts_on_the_details_again()
    {
        var onEvidence = Showing() with { FlightTab = FlightTab.Evidence };

        var reopened = Reducer.Reduce(onEvidence, Command.ShowFlight);

        await Assert.That(reopened.FlightTab).IsEqualTo(FlightTab.Details)
            .Because("the tab is about the flight being read, not a preference. Left where "
                   + "it was, the next flight opens on an evidence pane belonging to the "
                   + "one before it.");
    }

    [Test]
    public async Task The_key_is_offered_while_a_flight_is_open()
    {
        var offered = Keymap.Bindings(new KeymapContext(UiMode.FlightDetail));

        await Assert.That(offered.Any(b => b.Command == Command.NextFlightTab)).IsTrue()
            .Because("a tab nothing reaches is a pane that does not exist. tab cycles the "
                   + "console's own bar, so it is the key a person will already try.");
    }

    [Test]
    public async Task The_evidence_tab_renders_the_same_evidence_the_pane_did()
    {
        // ONE RENDERER. A second copy would agree with this one until somebody
        // edited one of them, and what they would disagree about is what
        // "nothing is waiting" means.
        var state = Showing() with { FlightTab = FlightTab.Evidence, SelectedRow = 0 };

        await Assert.That(FlightDetails.Evidence(state)).IsEqualTo(PaneText.Evidence(state));
    }
}
