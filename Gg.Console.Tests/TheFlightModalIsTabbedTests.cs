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
            .Because("a tab nothing reaches is a pane that does not exist.");

        await Assert.That(offered.Any(b => b.Key == KeyStroke.Char('v'))).IsTrue()
            .Because("v is what shows evidence one level up, so it is already the "
                   + "console's word for this.");
    }

    [Test]
    public async Task Tab_is_left_to_the_focus_it_moves_in_this_modal()
    {
        // THE REGRESSION THIS NEARLY WAS. tab cycles the bar in Normal mode, so
        // it looked like the obvious key here too - and an unresolved key falls
        // through to Terminal.Gui, which in THIS mode is what moves focus
        // between the intent, the fields and the log. Those fields are
        // focusable for exactly one reason: a Label cannot be copied out of and
        // the flight id is the value most often wanted out of this modal. Bind
        // tab here and OnScreenKeyDown marks it handled, so the flight id
        // becomes unreachable to save a keystroke.
        var offered = Keymap.Bindings(new KeymapContext(UiMode.FlightDetail));

        await Assert.That(offered.Any(b => b.Key == KeyStroke.TabKey)).IsFalse()
            .Because("it has to fall through to the focus traversal, which is the only way "
                   + "into the fields.");

        // The anchor: this mode does bind keys, so the absence above is a
        // decision rather than an empty map.
        await Assert.That(offered.Any(b => b.Key == KeyStroke.Esc)).IsTrue();
    }

    [Test]
    public async Task The_modal_body_is_a_tab_strip_with_the_three_regions_inside_one_tab()
    {
        // STRUCTURAL, BECAUSE NOTHING HERE CONSTRUCTS A ConsoleScreen. The view
        // layer in this project is asserted by reading it - LiveStreamingTests'
        // shape - so what a widget-level test would prove is spelled out as
        // what the source has to contain.
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("_flightTabs.Add(_flightDetailsTab)")
            .Because("the details are a tab, not the body - otherwise the strip has one "
                   + "half of the modal in it and the other half beside it.");

        await Assert.That(screen).Contains("_flightTabs.Add(_flightEvidenceTab)");

        await Assert.That(screen)
            .Contains("_flightDetailsTab.Add(_flightIntentPane, _flightFields, _flightLogPane)")
            .Because("all three regions move together. A modal whose log stayed outside the "
                   + "strip would show a flight's log under its evidence tab.");

        // THE SIZING THIS BREAKS IF IT IS FORGOTTEN. The intent measures the
        // room it shares with the fields and the log; that room is now the TAB,
        // which is a strip shorter than the body. Measured against the body the
        // intent takes rows that are not there and the log pays for them.
        await Assert.That(screen).Contains("_flightDetailsTab.Viewport.Height")
            .Because("the intent's cap has to measure the container the three regions "
                   + "actually share, which is no longer the body.");

        await Assert.That(screen).DoesNotContain("_flightBody.Viewport.Height")
            .Because("that is the pre-tab measure and it now overstates the room by the "
                   + "height of the strip.");
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
