using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The boot's reads overlap. It makes a request per flight and it does not
/// wait for each one before starting the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thirteen and a half seconds before the console drew anything.</b>
/// <c>ConsoleStart.LoadAsync</c> awaited every read in turn: the flight list,
/// the runners, then a log for each of fifty-two flights, then the gates, the
/// seed, the credentials, the identity and the why - fifty-nine round trips
/// laid end to end. Against a control plane 200ms away that is the whole
/// delay, and it grows by one round trip for every flight the tenant has ever
/// flown.
/// </para>
/// <para>
/// <b>The count is not the problem and is deliberately not changed.</b> A log
/// per flight is what fills the queue's two log-derived reasons and what makes
/// the detail modal free when somebody presses enter; dropping the reads would
/// take back the thing that was just asked for. What was wrong is that they
/// were sequential, and nothing about them says they have to be.
/// </para>
/// <para>
/// <b>A yield rather than a delay, so this is not a race.</b> The double
/// answers on a continuation, so a loader that starts its requests before
/// awaiting them has all of them inside the handler at once and a loader that
/// awaits each in turn has exactly one. There is no clock in it - the repo's
/// rule against sleeping in tests is also what makes this deterministic.
/// </para>
/// </remarks>
public class TheBootDoesNotReadOneLogAtATimeTests
{
    private const int Flights = AConsolePlane.DefaultFlights;

    [Test]
    public async Task More_than_one_log_is_in_the_air_at_once()
    {
        var (data, plane) = AConsolePlane.Console(Flights, inTheAir: Flights);

        await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(plane.PeakLogs).IsGreaterThan(1)
            .Because($"a log per flight laid end to end is the delay. {Flights} flights, "
                   + $"and the most that were ever in the air together was {plane.PeakLogs}.");
    }

    [Test]
    public async Task Every_flight_still_gets_its_log()
    {
        // THE ANCHOR, and it is the half that must not move. Overlapping the
        // reads is free; fetching fewer of them would take the flight detail
        // back off the enter key, which is a different decision entirely.
        var (data, plane) = AConsolePlane.Console(Flights, inTheAir: Flights);

        var loaded = await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(loaded.Logs.Count).IsEqualTo(Flights)
            .Because("every flight's log is still fetched, and the modal reads it from here.");
        await Assert.That(plane.Paths.Count(p => p.EndsWith("/log", StringComparison.Ordinal)))
            .IsEqualTo(Flights)
            .Because("one request each, not one and a retry.");
    }

    [Test]
    public async Task The_reads_that_need_nothing_from_each_other_do_not_queue_up()
    {
        // THE FLIGHT LIST, THE RUNNERS, THE GATES, THE CREDENTIALS AND THE
        // IDENTITY. Five answers, none of which is an input to any of the
        // others, and the boot asked for them one at a time.
        var (data, plane) = AConsolePlane.Console(Flights, inTheAir: Flights);

        await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(plane.Peak).IsGreaterThan(1)
            .Because("nothing in the boot is serial by necessity except what reads the flight "
                   + $"list first. The most in the air at once was {plane.Peak}.");
    }
}
