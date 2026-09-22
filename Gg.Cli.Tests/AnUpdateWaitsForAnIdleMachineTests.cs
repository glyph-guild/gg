using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Slice forty-eight, rule 7: a machine is updated only when it holds no
/// flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail closed, because the cost is asymmetric.</b> Updating a machine that
/// turns out to be busy abandons a flight somebody is waiting on and loses
/// whatever it had done. Declining to update one that was actually idle costs
/// a wait until the next ask. So every uncertain case refuses.
/// </para>
/// <para>
/// <b>Chosen from three, by its owner.</b> The other two were stopping the
/// runner cleanly first - honest, but it interrupts a busy machine, which is
/// the thing this rule exists to avoid - and asking the control plane, which
/// would put a credential on a root process that has none today.
/// </para>
/// </remarks>
public class AnUpdateWaitsForAnIdleMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 20, 0, 0, TimeSpan.Zero);

    private static RunnerHolding Said(string? holding, int secondsAgo = 5) =>
        new(true, holding, Now.AddSeconds(-secondsAgo));

    [Test]
    public async Task A_machine_with_no_runner_may_be_updated()
    {
        await Assert.That(UpdateWindow.WhyNot(false, RunnerHolding.Absent, Now)).IsNull()
            .Because("nothing running is nothing to abandon - a laptop, or a host whose unit "
                   + "is stopped - and refusing here would make the safe case the one that "
                   + "cannot be updated. Failing closed is about not KNOWING, not about "
                   + "nothing being there.");
    }

    [Test]
    public async Task A_stopped_runner_with_a_stale_marker_left_behind_may_still_be_updated()
    {
        await Assert.That(UpdateWindow.WhyNot(false, Said("GG-251", secondsAgo: 9000), Now))
            .IsNull()
            .Because("a runner that stopped leaves its last word behind, and a marker nobody "
                   + "is writing must not read as `busy` for ever.");
    }

    [Test]
    public async Task An_idle_runner_may_be_updated()
    {
        await Assert.That(UpdateWindow.WhyNot(true, Said(null), Now)).IsNull();
    }

    // ---- the refusals ----

    [Test]
    public async Task A_runner_holding_a_flight_is_left_alone()
    {
        var why = UpdateWindow.WhyNot(true, Said("GG-251"), Now);

        await Assert.That(why).IsNotNull();
        await Assert.That(why!).Contains("GG-251")
            .Because("the refusal names what it is protecting, so a person deciding whether "
                   + "to wait can see what they would be interrupting.");
    }

    [Test]
    public async Task A_runner_that_has_said_nothing_is_not_assumed_idle()
    {
        await Assert.That(UpdateWindow.WhyNot(true, RunnerHolding.Absent, Now)).IsNotNull()
            .Because("this is the whole of failing closed: a runner is up, nothing says what "
                   + "it is doing, and the cheap assumption is the one that abandons a "
                   + "flight.");
    }

    [Test]
    public async Task A_marker_that_stopped_moving_is_a_refusal()
    {
        var why = UpdateWindow.WhyNot(true, Said(null, secondsAgo: 600), Now);

        await Assert.That(why).IsNotNull()
            .Because("the runner rewrites this as it beats, so one that stopped moving means "
                   + "the RUNNER stopped moving - which is exactly the state where it may "
                   + "still hold a lease the control plane has not taken back. It says idle, "
                   + "and it said so ten minutes ago.");
    }

    [Test]
    public async Task A_marker_from_the_future_is_a_refusal_too()
    {
        await Assert.That(UpdateWindow.WhyNot(true, Said(null, secondsAgo: -300), Now))
            .IsNotNull()
            .Because("a clock disagreeing is not evidence of anything, and subtracting into "
                   + "a negative age would otherwise pass every freshness check there is.");
    }

    [Test]
    public async Task A_marker_with_no_time_on_it_is_a_refusal()
    {
        await Assert.That(UpdateWindow.WhyNot(true, new RunnerHolding(true, null, null), Now))
            .IsNotNull()
            .Because("present but undated is the shape a truncated or half-written file has, "
                   + "and it cannot be told from a fresh one without a time.");
    }

    [Test]
    public async Task Every_refusal_says_what_would_happen_if_it_went_ahead()
    {
        foreach (var why in (string?[])
                 [UpdateWindow.WhyNot(true, RunnerHolding.Absent, Now),
                  UpdateWindow.WhyNot(true, Said("GG-251"), Now),
                  UpdateWindow.WhyNot(true, Said(null, secondsAgo: 600), Now)])
        {
            await Assert.That(why).IsNotNull();
            await Assert.That(why!.Length).IsGreaterThan(40)
                .Because("this refusal is read by somebody wondering why their machine did "
                       + "not update, and `busy` would send them looking.");
        }
    }
}
