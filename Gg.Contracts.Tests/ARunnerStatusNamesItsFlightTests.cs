using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner's status says which flight it is on, and when it last beat.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a watch can now outlive a flight.</b> A channel used to exist only
/// while one flight did, so what the tail carried could not change subject under
/// a reader. A watcher attached to an idle machine follows whatever it claims
/// next - and a pane that silently starts drawing a different flight's output is
/// a pane that is lying by omission.
/// </para>
/// <para>
/// <b>The NUMBER crosses and the id does not.</b> <c>GG-84</c> is what a person
/// reads, it is unique within a tenant, and it is already what
/// <c>RunnerSummary.CurrentFlightNumber</c> carries. The id is what the runner
/// builds a live-view path out of; it needs it on the machine and nothing off
/// the machine needs it, so it stays there.
/// </para>
/// <para>
/// <b>And the beat is a SECOND silence worth telling apart.</b>
/// <see cref="RunnerStatusReport.At"/> says the channel answered; <c>BeatAt</c>
/// says the machine is still talking to the control plane. A runner reachable
/// over a peer connection while partitioned from the control plane reads as
/// healthy on one and stopped on the other, and only one of those is going to
/// be given work.
/// </para>
/// </remarks>
public class ARunnerStatusNamesItsFlightTests
{
    private static DateTimeOffset At(string when) => DateTimeOffset.Parse(when, null);

    [Test]
    public async Task A_status_carries_the_flight_and_the_beat()
    {
        var report = new RunnerStatusReport
        {
            Doing = "working a flight",
            At = At("2026-09-12T18:00:00Z"),
            FlightNumber = "GG-84",
            BeatAt = At("2026-09-12T17:59:48Z"),
        };

        await Assert.That(report.FlightNumber).IsEqualTo("GG-84");
        await Assert.That(report.BeatAt).IsEqualTo(At("2026-09-12T17:59:48Z"));
    }

    [Test]
    public async Task Absent_is_idle_rather_than_unknown()
    {
        // NOT REQUIRED, AND THAT IS THE ANSWER FOR AN IDLE MACHINE. A required
        // member would force a sentinel, and every sentinel here would have to
        // be told apart from a flight number by whoever read it.
        var idle = new RunnerStatusReport
        {
            Doing = "idle",
            At = At("2026-09-12T18:00:00Z"),
        };

        await Assert.That(idle.FlightNumber).IsNull()
            .Because("a runner flying nothing has no number to give, and saying so is "
                   + "what lets a watcher wait rather than report a gap.");

        await Assert.That(idle.BeatAt).IsNull()
            .Because("and a runner that has not beaten since the channel opened has "
                   + "nothing to report either - which is the case worth seeing.");
    }

    [Test]
    public async Task Both_survive_being_stripped()
    {
        // EVERYTHING OFF A RUNNER IS HOSTILE TEXT until it has been through
        // Stripped(), and a member that is not copied there arrives as null on
        // every surface that strips - silently absent, and indistinguishable
        // from idle.
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.Status,
            Status = new RunnerStatusReport
            {
                Doing = "working a flight",
                At = At("2026-09-12T18:00:00Z"),
                FlightNumber = "GG-84[31m",
                BeatAt = At("2026-09-12T17:59:48Z"),
            },
        };

        var clean = said.Stripped();

        await Assert.That(clean.Status!.FlightNumber).IsEqualTo("GG-84")
            .Because("a flight number is text off a machine like any other, and this one "
                   + "is about to be drawn in a terminal.");

        await Assert.That(clean.Status!.BeatAt).IsEqualTo(said.Status!.BeatAt)
            .Because("an instant has nothing to strip and must not be dropped on the way "
                   + "through.");
    }

    [Test]
    public async Task The_surface_declares_them()
    {
        var members = ProtocolSurface.JsonMembers[typeof(RunnerStatusReport)];

        await Assert.That(members).Contains("flightNumber");
        await Assert.That(members).Contains("beatAt");

        await Assert.That(members.Any(m => m.Contains("flightId", StringComparison.Ordinal)))
            .IsFalse()
            .Because("the id stays on the machine that needs it for a path.");
    }
}
