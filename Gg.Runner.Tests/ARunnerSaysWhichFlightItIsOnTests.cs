using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner keeps the flight it is on, and lets go of it when it ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>It already knows and it was throwing the answer away.</b>
/// <c>Claimed(lease)</c> carries the flight's id and its number; the object that
/// answers <c>status</c> took the call, wrote "working a flight" and kept
/// neither. That was enough while a channel existed only for one flight -
/// whoever was watching had asked for THAT flight and could not be handed
/// another. A watch attached to an idle machine follows whatever it claims
/// next, so the answer has to say which.
/// </para>
/// <para>
/// <b>Every ending clears it, and the list is the point.</b> A flight number
/// left behind after a landing is worse than none: a watcher would go on naming
/// a flight that finished, and the one thing it is there to report is a change.
/// </para>
/// <para>
/// <b>The beat is separate from all of it.</b> Beating is something a runner
/// does whether or not it is flying, so recording one must not disturb the
/// flight, and a flight must not disturb the beat.
/// </para>
/// </remarks>
public class ARunnerSaysWhichFlightItIsOnTests
{
    private static DateTimeOffset At(string when) => DateTimeOffset.Parse(when, null);

    private static WhatThisRunnerSays ARunner() =>
        new(new SilentObserver(), _ => new NoLog(), () => At("2026-09-12T18:00:00Z"));

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
        ExpiresAt = At("2026-09-12T18:30:00Z"),
        RenewWithinSeconds = 30,
    };

    [Test]
    public async Task Claiming_a_flight_is_what_names_it()
    {
        var says = ARunner();

        await Assert.That(says.Status().FlightNumber).IsNull()
            .Because("a runner that has claimed nothing is on nothing.");

        says.Claimed(ALease(84));

        await Assert.That(says.Status().FlightNumber).IsEqualTo(FlightRef.Format(84))
            .Because("the lease carries the number and this is the only place it is "
                   + "learned - deriving it anywhere else would be a second answer.");
    }

    [Test]
    public async Task And_every_ending_lets_go_of_it()
    {
        // THE WHOLE LIST, because one ending left out is a watcher naming a
        // flight that finished - and it would be the rare ending, which is the
        // one nobody is looking at when it happens.
        var endings = new (string Name, Action<WhatThisRunnerSays> End)[]
        {
            ("released", s => s.Released("lease-84", "landed")),
            ("fenced", s => s.Fenced("lease-84")),
            ("idle", s => s.Idle()),
            ("waiting", s => s.Waiting(["acme/widgets"])),
            ("parked", s => s.Parked()),
            ("allowance spent", s => s.AllowanceSpent()),
        };

        foreach (var (name, end) in endings)
        {
            var says = ARunner();
            says.Claimed(ALease(84));
            end(says);

            await Assert.That(says.Status().FlightNumber).IsNull()
                .Because($"{name} is the end of a flight, and a number that outlives one "
                       + "is a watcher reporting work that is over.");
        }
    }

    [Test]
    public async Task A_beat_is_recorded_and_does_not_disturb_the_flight()
    {
        var says = ARunner();

        await Assert.That(says.Status().BeatAt).IsNull()
            .Because("a runner that has not beaten since this started has nothing to "
                   + "report, and that is the case worth seeing.");

        says.Beat(At("2026-09-12T17:59:48Z"));
        says.Claimed(ALease(84));
        says.Beat(At("2026-09-12T18:00:03Z"));

        await Assert.That(says.Status().BeatAt).IsEqualTo(At("2026-09-12T18:00:03Z"))
            .Because("the latest beat is the one that says it is still talking to the "
                   + "control plane; an older one would read as a machine going quiet.");

        await Assert.That(says.Status().FlightNumber).IsEqualTo(FlightRef.Format(84))
            .Because("beating is something a runner does whether or not it is flying.");
    }

    [Test]
    public async Task A_flight_does_not_disturb_the_beat()
    {
        var says = ARunner();
        says.Beat(At("2026-09-12T17:59:48Z"));

        says.Claimed(ALease(84));
        says.Released("lease-84", "landed");

        await Assert.That(says.Status().BeatAt).IsEqualTo(At("2026-09-12T17:59:48Z"))
            .Because("landing a flight says nothing about whether the control plane is "
                   + "still being reached, and clearing it would invent a silence.");
    }

    /// <summary>A log for a flight that wrote nothing. The absent-file answer.</summary>
    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }
}
