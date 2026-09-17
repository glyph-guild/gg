using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A runner that will take no work recedes, and says why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flights tab's rule, one tab over.</b> There, colour means "this is
/// over"; here it means "this one will not answer". <c>busy</c> and <c>idle</c>
/// are both alive and both fine - one holds a lease, the other waits for one -
/// so neither is tinted and neither recedes. What a person scans a fleet for is
/// the machine that is not going to take the next flight.
/// </para>
/// <para>
/// <b>The cell is not a bare state, which is the trap here.</b>
/// <c>Rows.Runners</c> writes both facts or neither - "a runner can be parked
/// AND busy, draining, which is the reason to park anything" - so what arrives
/// is <c>idle · parked</c> as often as <c>idle</c>, and a match on the whole
/// cell would colour neither.
/// </para>
/// </remarks>
public class TheFleetShowsWhoWillAnswerTests
{
    [Test]
    public async Task A_runner_that_can_take_work_is_the_ordinary_foreground()
    {
        foreach (var alive in (string[])[RunnerStates.Busy, RunnerStates.Idle])
        {
            await Assert.That(RunnerLook.Tint(alive)).IsEqualTo(RunnerTint.None)
                .Because($"'{alive}' is a machine doing its job, and a tint on the ordinary "
                       + "case is a tint that says nothing.");
            await Assert.That(RunnerLook.IsAside(alive)).IsFalse();
        }
    }

    [Test]
    public async Task One_that_stopped_answering_recedes()
    {
        await Assert.That(RunnerLook.Tint(RunnerStates.Offline)).IsEqualTo(RunnerTint.Offline);
        await Assert.That(RunnerLook.IsAside(RunnerStates.Offline)).IsTrue();
    }

    [Test]
    public async Task And_so_does_one_somebody_withheld()
    {
        // BOTH HALVES OF THE CELL. The row writes `idle · parked` because the
        // two facts are independent, and a match on the whole cell would see
        // neither of them.
        await Assert.That(RunnerLook.Tint("idle · parked")).IsEqualTo(RunnerTint.Parked);
        await Assert.That(RunnerLook.Tint("busy · parked")).IsEqualTo(RunnerTint.Parked);

        await Assert.That(RunnerLook.IsAside("idle · parked")).IsTrue()
            .Because("a parked runner is alive and will still take nothing, which is the "
                   + "whole reason somebody parked it.");
    }

    [Test]
    public async Task Parked_outranks_offline_when_a_machine_is_both()
    {
        // THE CHOICE, SAID OUT LOUD. Offline is the ordinary condition of a
        // laptop; parked is a decision somebody made and may have forgotten.
        await Assert.That(RunnerLook.Tint("offline · parked")).IsEqualTo(RunnerTint.Parked);
    }

    [Test]
    public async Task A_maintainer_recedes_without_reading_as_stopped()
    {
        // IT WILL TAKE NO WORK, so it recedes. It has not stopped answering, so
        // it is not the offline tint: that one says the heartbeat went stale.
        await Assert.That(RunnerLook.IsAside("maintaining")).IsTrue();
        await Assert.That(RunnerLook.Tint("maintaining")).IsNotEqualTo(RunnerTint.Offline);
        await Assert.That(RunnerLook.Tint("maintaining · parked")).IsEqualTo(RunnerTint.Parked);
    }

    [Test]
    public async Task A_state_this_console_does_not_know_is_left_alone()
    {
        await Assert.That(RunnerLook.Tint("draining")).IsEqualTo(RunnerTint.None);
        await Assert.That(RunnerLook.IsAside("draining")).IsFalse();
        await Assert.That(RunnerLook.IsAside(null)).IsFalse();
        await Assert.That(RunnerLook.IsAside("")).IsFalse();
    }

    [Test]
    public async Task What_is_tinted_and_what_recedes_cannot_disagree()
    {
        foreach (var cell in (string[])
        [
            RunnerStates.Busy, RunnerStates.Idle, RunnerStates.Offline,
            "idle · parked", "offline · parked", "draining", "",
        ])
        {
            await Assert.That(RunnerLook.IsAside(cell))
                .IsEqualTo(RunnerLook.Tint(cell) is not RunnerTint.None);
        }
    }

    [Test]
    public async Task Every_state_in_the_vocabulary_is_answered()
    {
        // ARTICLE XI OVER THE VOCABULARY: a state the contract declares and
        // this has never heard of renders as a machine ready to take work,
        // which is the one thing it may well not be.
        var declared = typeof(RunnerStates)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        await Assert.That(declared).IsNotEmpty();

        var alive = new[] { RunnerStates.Busy, RunnerStates.Idle };

        var unanswered = declared
            .Where(s => !alive.Contains(s) && RunnerLook.Tint(s) is RunnerTint.None)
            .ToList();

        await Assert.That(unanswered).IsEmpty()
            .Because("a state that is neither busy nor idle means the machine is not taking "
                   + "work, and it needs to say so. Unanswered: " + string.Join(", ", unanswered));
    }
}
