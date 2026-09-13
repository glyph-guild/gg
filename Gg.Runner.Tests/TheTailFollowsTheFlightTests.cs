using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// What <c>tail-log</c> answers follows whatever this runner is flying now.
/// </summary>
/// <remarks>
/// <para>
/// <b>The log used to be chosen once, when the session was built.</b> That was
/// exactly right while a session existed for one flight: the path WAS the
/// narrowing, and a filter applied afterwards would have been a promise instead
/// of a fact. A watch that outlives a flight has no such moment to choose in -
/// there is no flight when somebody attaches to an idle machine.
/// </para>
/// <para>
/// <b>So the question is asked at read time, and the narrowing survives whole.</b>
/// One flight at a time, the one this machine is running now, named from the
/// lease it holds. Never a journal: that would hand somebody every flight the
/// runner has ever run, including other people's.
/// </para>
/// <para>
/// <b>And this is what makes following automatic.</b> The watcher goes on
/// asking the same question once a second; the answer simply starts being a
/// different file. Nothing negotiates, nothing re-attaches, and there is no
/// moment where one end knows and the other does not.
/// </para>
/// </remarks>
public class TheTailFollowsTheFlightTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static LeaseGranted ALease(int number) => new()
    {
        LeaseId = $"lease-{number}",
        Generation = 1,
        FlightId = $"flight-{number}",
        FlightNumber = FlightRef.Format(number),
        Repos = [],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(30),
        RenewWithinSeconds = 30,
    };

    [Test]
    public async Task An_idle_runner_has_nothing_to_tail()
    {
        var says = new WhatThisRunnerSays(
            new SilentObserver(), Logs(), () => T0);

        var read = says.Tail(40);

        await Assert.That(read.Lines).IsEmpty()
            .Because("a machine flying nothing is not a machine hiding something - an "
                   + "empty tail is what lets a watcher sit and wait.");
    }

    [Test]
    public async Task A_claim_is_what_gives_it_something()
    {
        var says = new WhatThisRunnerSays(
            new SilentObserver(), Logs(("flight-84", "GG-84 is talking")), () => T0);

        says.Claimed(ALease(84));

        await Assert.That(says.Tail(40).Lines).Contains("GG-84 is talking")
            .Because("the lease names the flight, and the flight names the file.");
    }

    [Test]
    public async Task And_the_next_flight_is_what_it_follows_next()
    {
        // THE WHOLE POINT. A watcher attached while this machine was idle asks
        // the same question throughout; what changes is which file answers it.
        var says = new WhatThisRunnerSays(
            new SilentObserver(),
            Logs(("flight-84", "GG-84 is talking"), ("flight-85", "GG-85 is talking")),
            () => T0);

        says.Claimed(ALease(84));
        says.Released("lease-84", "landed");

        await Assert.That(says.Tail(40).Lines).IsEmpty()
            .Because("between flights there is nothing to show, and showing the last one "
                   + "would be a landed flight pretending to still be running.");

        says.Claimed(ALease(85));

        await Assert.That(says.Tail(40).Lines).Contains("GG-85 is talking")
            .Because("and the second flight is followed exactly as the first was, which "
                   + "is what `stay attached' means.");

        await Assert.That(says.Tail(40).Lines).DoesNotContain("GG-84 is talking")
            .Because("one flight at a time is the narrowing that survives; a journal here "
                   + "would hand somebody every flight this machine has run.");
    }

    /// <summary>The live views this machine has, by flight id.</summary>
    private static Func<string, IReadOnlyLog> Logs(params (string FlightId, string Line)[] views)
    {
        var byFlight = views.ToDictionary(v => v.FlightId, v => v.Line, StringComparer.Ordinal);

        return flightId => byFlight.TryGetValue(flightId, out var line)
            ? new OneLine(line)
            : new NoLog();
    }

    private sealed class OneLine(string line) : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([line], false);
    }

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }
}
