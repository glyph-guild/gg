namespace Gg.Console.Tests;

/// <summary>
/// The flight modal's second tab is the gate.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was called Evidence, and that named the exhibits rather than the
/// question.</b> A gate is the decision waiting on somebody; evidence is the
/// material an obligation declares that decision needs. The two are not the
/// same and the difference is load-bearing:
/// <c>ObligationReceptor</c> assembles the case BEFORE opening the gate, and
/// evidence is OPTIONAL - <c>declared = obligation.Evidence ?? []</c>, and the
/// assembler only runs when that is non-empty. So every payload belongs to a
/// gate and plenty of gates have none.
/// </para>
/// <para>
/// <b>Naming the tab for the question makes its empty sentence true rather
/// than merely accurate.</b> "Nothing is waiting on you for this flight" is an
/// answer about a GATE. Under a tab called Evidence it read as "no exhibits
/// were filed", which is a different claim and one this console cannot make.
/// </para>
/// </remarks>
public class TheSecondTabIsTheGateTests
{
    [Test]
    public async Task The_tab_is_titled_for_the_decision_not_the_exhibits()
    {
        await Assert.That(FlightDetails.GateTitle).IsEqualTo("Gate")
            .Because("the tab is the question waiting on this flight. The exhibits are "
                   + "what fills it, and only sometimes.");
    }

    [Test]
    public async Task The_second_tab_is_named_for_it_too()
    {
        var state = new AppState { Mode = UiMode.FlightDetail };

        await Assert.That(Reducer.Reduce(state, Command.NextFlightTab).FlightTab)
            .IsEqualTo(FlightTab.Gate)
            .Because("the state and the strip have to agree about what the tab is, or the "
                   + "next reader has two names for one thing.");
    }

    [Test]
    public async Task The_key_still_reaches_it()
    {
        var offered = Keymap.Bindings(new KeymapContext(UiMode.FlightDetail));

        var binding = offered.Single(b => b.Command == Command.NextFlightTab);

        await Assert.That(binding.Description).IsEqualTo("gate")
            .Because("the hint line names the tab it opens; left saying 'evidence' it would "
                   + "be the one place the old name survived, which is how two names for "
                   + "one thing start.");
    }
}
