using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Submit, then observe until an outcome or a bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>A component rather than a verb, because two more transports will need
/// it.</b> A web surface and a chat surface each submit something and then watch
/// for it to become true, and each renders the waiting differently. What they
/// share is the loop and the three answers it can give; what differs is only how
/// the waiting looks. Writing this inside <c>gg decide</c> would mean the second
/// caller reimplements the bound, the backoff and - the part that matters - the
/// distinction between <i>no</i> and <i>not yet</i>.
/// </para>
/// <para>
/// <b>Three outcomes, and the third one is why this exists.</b> A submission can
/// be answered no, or it can be unanswered. Collapsing those into one non-zero
/// turns a slow worker into a recorded rejection, which is the exact failure the
/// strict-CQRS change would otherwise introduce.
/// </para>
/// <para>
/// <b>Time is injected.</b> The bound is the subject of half these tests, so a
/// real clock would make them either slow or flaky, and the loop would be
/// untestable at exactly the boundary that matters.
/// </para>
/// </remarks>
public class SubmitAndObserveTests
{
    /// <summary>A clock that only moves when the loop asks to wait.</summary>
    private sealed class Ticking
    {
        private DateTimeOffset _now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

        internal List<TimeSpan> Waits { get; } = [];

        internal DateTimeOffset Now() => _now;

        internal Task DelayAsync(TimeSpan span, CancellationToken cancellationToken)
        {
            Waits.Add(span);
            _now = _now.Add(span);
            return Task.CompletedTask;
        }
    }

    private static SubmitAndObserve Loop(Ticking clock) => new(clock.DelayAsync, clock.Now);

    private static ObservationBound Bound(double seconds = 30) => new()
    {
        Wait = TimeSpan.FromSeconds(seconds),
        FirstDelay = TimeSpan.FromMilliseconds(200),
        MaxDelay = TimeSpan.FromSeconds(2),
    };

    private static Func<CancellationToken, Task<string?>> Accepts() => _ => Task.FromResult<string?>(null);

    // ---- the three states ----

    [Test]
    public async Task Something_already_visible_is_decided_without_waiting()
    {
        // THE FAST PATH, AND TODAY IT IS THE ONLY PATH. The server still writes
        // synchronously, so the first observation succeeds - and a loop that
        // slept before looking would add latency to every decision for no reason.
        var clock = new Ticking();

        var observed = await Loop(clock).RunAsync(
            Accepts(), _ => Task.FromResult<string?>("satisfied"), Bound());

        await Assert.That(observed.State).IsEqualTo(ObservationStates.Decided);
        await Assert.That(observed.Outcome).IsEqualTo("satisfied");
        await Assert.That(observed.Polls).IsEqualTo(1);
        await Assert.That(clock.Waits).IsEmpty()
            .Because("it looked before it slept, which is the difference between a bound and a "
                   + "delay.");
    }

    [Test]
    public async Task A_refused_submission_never_observes()
    {
        // NOT A POLL AT ALL. The submission was answered, and the answer was no.
        // Observing after that would be waiting for something nobody wrote.
        var clock = new Ticking();
        var polled = 0;

        var observed = await Loop(clock).RunAsync(
            _ => Task.FromResult<string?>("The work changed while this decision was being made."),
            _ => { polled++; return Task.FromResult<string?>("satisfied"); },
            Bound());

        await Assert.That(observed.State).IsEqualTo(ObservationStates.Refused);
        await Assert.That(observed.Because).Contains("The work changed");
        await Assert.That(polled).IsEqualTo(0);
        await Assert.That(observed.Outcome).IsNull();
    }

    [Test]
    public async Task Nothing_visible_within_the_bound_is_not_yet_visible_and_never_refused()
    {
        // THE ONE THAT MUST NOT COLLAPSE. A bound that expired says nothing about
        // whether the decision was recorded - and reporting it as a refusal would
        // turn a slow worker into a rejection nobody made.
        var clock = new Ticking();

        var observed = await Loop(clock).RunAsync(
            Accepts(), _ => Task.FromResult<string?>(null), Bound(seconds: 5));

        await Assert.That(observed.State).IsEqualTo(ObservationStates.NotYetVisible);
        await Assert.That(observed.State).IsNotEqualTo(ObservationStates.Refused);
        await Assert.That(observed.Because).Contains("not")
            .Because("the sentence has to keep 'we do not know' apart from 'you were told no', "
                   + "because that is the whole distinction this component exists for.");
        await Assert.That(observed.Outcome).IsNull();
    }

    [Test]
    public async Task Every_state_the_vocabulary_names_is_one_this_loop_can_reach()
    {
        var clock = new Ticking();

        var reached = new List<string>
        {
            (await Loop(clock).RunAsync(
                Accepts(), _ => Task.FromResult<string?>("satisfied"), Bound())).State,
            (await Loop(clock).RunAsync(
                _ => Task.FromResult<string?>("no"),
                _ => Task.FromResult<string?>(null), Bound())).State,
            (await Loop(new Ticking()).RunAsync(
                Accepts(), _ => Task.FromResult<string?>(null), Bound(seconds: 1))).State,
        };

        await Assert.That(reached.Distinct().Order().ToList())
            .IsEquivalentTo(ObservationStates.All.Order().ToList());
    }

    // ---- the bound ----

    [Test]
    public async Task It_becomes_visible_after_a_few_polls()
    {
        var clock = new Ticking();
        var polls = 0;

        var observed = await Loop(clock).RunAsync(
            Accepts(),
            _ => Task.FromResult(++polls < 4 ? null : "satisfied"),
            Bound());

        await Assert.That(observed.State).IsEqualTo(ObservationStates.Decided);
        await Assert.That(observed.Polls).IsEqualTo(4);
        await Assert.That(clock.Waits.Count).IsEqualTo(3)
            .Because("one wait between each pair of looks, and none before the first.");
    }

    [Test]
    public async Task The_wait_backs_off_rather_than_spinning()
    {
        // A BUSY LOOP AGAINST A CONTROL PLANE IS A DENIAL OF SERVICE WITH GOOD
        // INTENTIONS. Doubling, capped, so a long wait costs a handful of
        // requests rather than thousands.
        var clock = new Ticking();

        await Loop(clock).RunAsync(
            Accepts(), _ => Task.FromResult<string?>(null), Bound(seconds: 10));

        await Assert.That(clock.Waits[0]).IsEqualTo(TimeSpan.FromMilliseconds(200));
        await Assert.That(clock.Waits[1]).IsEqualTo(TimeSpan.FromMilliseconds(400));
        await Assert.That(clock.Waits.All(w => w <= TimeSpan.FromSeconds(2))).IsTrue()
            .Because("capped, so a thirty-second bound does not end with one thirty-second "
                   + "sleep that cannot be interrupted.");
        await Assert.That(clock.Waits.Count).IsLessThan(30)
            .Because("and a bounded wait that made hundreds of requests would be the busy loop "
                   + "with extra steps.");
    }

    [Test]
    public async Task The_bound_is_never_overrun()
    {
        var clock = new Ticking();

        var observed = await Loop(clock).RunAsync(
            Accepts(), _ => Task.FromResult<string?>(null), Bound(seconds: 3));

        await Assert.That(observed.WaitedSeconds).IsLessThanOrEqualTo(3);
        await Assert.That(clock.Waits.Aggregate(TimeSpan.Zero, (a, b) => a + b))
            .IsLessThanOrEqualTo(TimeSpan.FromSeconds(3))
            .Because("a bound somebody was told about and then exceeded is worse than no bound.");
    }

    [Test]
    public async Task The_bound_it_waited_against_is_carried_rather_than_hidden()
    {
        // SO THE OUTPUT CAN SAY IT. "We do not know yet" is only actionable next
        // to how long we looked - otherwise nobody can tell a bound that is too
        // short from a control plane that is stuck.
        var observed = await Loop(new Ticking()).RunAsync(
            Accepts(), _ => Task.FromResult<string?>(null), Bound(seconds: 7));

        await Assert.That(observed.BoundSeconds).IsEqualTo(7);
        await Assert.That(observed.Because).Contains("7");
    }

    [Test]
    public async Task A_cancelled_wait_stops_rather_than_reporting_an_answer()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.That(async () => await Loop(new Ticking()).RunAsync(
                Accepts(), _ => Task.FromResult<string?>(null), Bound(), cancel.Token))
            .Throws<OperationCanceledException>()
            .Because("somebody pressing ctrl-c has not been told anything, and returning a state "
                   + "would be inventing one.");
    }
}
