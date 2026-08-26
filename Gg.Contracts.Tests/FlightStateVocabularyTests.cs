using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The states a flight may be derived to be in, and the four that are endings.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0017's vocabulary, and it is closed because the only safe response to
/// an unknown value is to halt.</b> A seventh state is a version event rather
/// than a value quietly appearing — Contracts' rule, unchanged: a member may be
/// added freely, a value may not.
/// </para>
/// <para>
/// <b>Two of the six are readings rather than exits, and the distinction is
/// load-bearing.</b> <c>open</c> is the absence of an ending and <c>unknown</c>
/// is the absence of a record; neither is something that HAPPENED to a flight,
/// so neither may be written to the exit store nor declared reachable by a work
/// kind. Collapsing them into the exits would let a flight be recorded as having
/// ended in the state that means nobody knows how it ended.
/// </para>
/// <para>
/// <b>Ending and stopping are different axes.</b> A flight that halted,
/// exhausted its budget or was abandoned is not finished — it is stopped, and
/// slice seven exists to make exactly those resumable. There is deliberately no
/// state here for any of them: they are readings of an OPEN flight, and a
/// terminal state must never be inferred from a flight having gone quiet.
/// </para>
/// </remarks>
public class FlightStateVocabularyTests
{
    [Test]
    public async Task The_states_are_six_and_there_is_no_seventh()
    {
        await Assert.That(FlightStates.All).IsEquivalentTo((string[])
            [FlightStates.Open, FlightStates.Landed, FlightStates.Grounded,
             FlightStates.Withdrawn, FlightStates.Failed, FlightStates.Unknown])
            .Because("a seventh state is a design decision rather than an addition to a list: "
                   + "every prior reader halts on a value it does not know, which is what "
                   + "closing the vocabulary buys and what a quiet addition would spend.");
    }

    [Test]
    public async Task The_exits_are_four_and_every_one_is_a_state()
    {
        await Assert.That(FlightStates.Exits).IsEquivalentTo((string[])
            [FlightStates.Landed, FlightStates.Grounded,
             FlightStates.Withdrawn, FlightStates.Failed]);

        foreach (var exit in FlightStates.Exits)
        {
            await Assert.That(FlightStates.All).Contains(exit)
                .Because($"'{exit}' can be reached and cannot be rendered, which is a flight "
                       + "that ended in a state nothing can show.");
        }
    }

    [Test]
    public async Task Open_and_unknown_are_readings_rather_than_exits()
    {
        // THE DISTINCTION THE EXIT STORE DEPENDS ON. Open is the absence of an
        // ending and unknown is the absence of a record. Neither happened TO a
        // flight, so neither may be written down as something that did - and a
        // work kind that declared `unknown` reachable would be declaring that
        // its flights can end in the state meaning nobody knows how they ended.
        await Assert.That(FlightStates.Exits).DoesNotContain(FlightStates.Open);
        await Assert.That(FlightStates.Exits).DoesNotContain(FlightStates.Unknown);
    }

    [Test]
    public async Task Nothing_stopped_is_an_ending()
    {
        // ADR-0017's axis rule, asserted rather than trusted to prose. Slice
        // seven's whole wedge is that a halted or exhausted flight is resumable
        // by anyone from anywhere; a state for either here would be the first
        // step toward closing one.
        foreach (var stopped in (string[])["halted", "exhausted", "abandoned", "expired"])
        {
            await Assert.That(FlightStates.All).DoesNotContain(stopped)
                .Because($"'{stopped}' is a flight that STOPPED, and slice seven exists to "
                       + "make exactly those resumable. Ending and stopping are different "
                       + "axes and this vocabulary is only one of them.");
        }
    }

    [Test]
    public async Task Every_state_is_a_wire_value_somebody_can_read()
    {
        foreach (var state in FlightStates.All)
        {
            await Assert.That(state).IsNotEmpty();
            await Assert.That(state).IsEqualTo(state.ToLowerInvariant())
                .Because("these cross the wire and are compared ordinally on both sides.");
        }

        await Assert.That(FlightStates.All.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(FlightStates.All.Count)
            .Because("two names for one state is two states as far as any reader is concerned.");
    }
}
