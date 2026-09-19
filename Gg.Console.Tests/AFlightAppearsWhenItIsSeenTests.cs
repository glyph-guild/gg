using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight the console is waiting for becomes a row the moment a look finds it,
/// and a notification says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Looked for, not refreshed for.</b> The thirty-second tick re-reads the tab
/// in front of somebody and would find the flight eventually; a person who
/// just opened one is looking at the corner of the screen waiting for it. So
/// the flight the door named is asked about directly - one small read of one
/// flight - straight away, then less often, and never twice at once.
/// </para>
/// <para>
/// <b>Bounded, and the bound is said.</b> A flight not seen in thirty seconds is
/// given up on with a notification saying it was accepted and is not listed, rather
/// than the console going quiet about something a person is waiting for.
/// </para>
/// <para>
/// <b>Time is the test's.</b> The clock is moved by hand and every look answers
/// synchronously or is held open by the test, so nothing here sleeps.
/// </para>
/// </remarks>
public class AFlightAppearsWhenItIsSeenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);

    private const string Opened = "f-opened";

    private static AppState Waiting(params FlightSummary[] listed) => new()
    {
        Flights = new FlightList { Flights = listed },
        Expecting = [new Expectation { Kind = ExpectationKind.FlightAppears, Id = Opened }],
    };

    [Test]
    public async Task A_flight_found_on_the_first_look_becomes_a_row_and_a_notification()
    {
        var clock = new Clock();
        var plane = new Plane(AFlight(Opened, 1042));
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting(AFlight("f-older", 1041)));
        state = expectations.Advance(state);

        await Assert.That(state.Flights!.Flights.Select(f => f.FlightId)).Contains(Opened)
            .Because("the row appears without waiting for the tab's thirty-second tick.");
        await Assert.That(state.Expecting).IsEmpty()
            .Because("what was being looked for has been seen.");
        await Assert.That(state.Notifications).IsEquivalentTo(new[]
        {
            new Notification
            {
                Kind = NotificationKind.FlightOpened,
                FlightId = Opened,
                FlightNumber = "GG-1042",
                Name = "work 1042",
            },
        })
            .Because("and the number, which the door could not give, is the thing worth saying.");
    }

    [Test]
    public async Task A_row_already_listed_is_replaced_rather_than_repeated()
    {
        // The tab's own tick can bring the flight first; the look then finds it
        // too, and one flight must still be one row.
        var clock = new Clock();
        var plane = new Plane(AFlight(Opened, 1042));
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting(AFlight(Opened, 1042)));
        state = expectations.Advance(state);

        await Assert.That(state.Flights!.Flights.Count(f => f.FlightId == Opened)).IsEqualTo(1);
    }

    [Test]
    public async Task The_cursor_stays_on_the_flight_it_was_on()
    {
        // THE CURSOR IS AN INDEX INTO THE LIST AS SHOWN, newest first - so a new
        // flight arriving at the top moves every row down one, and a cursor left
        // where it was is on a different flight. Whatever somebody was about to
        // press enter on must still be under the cursor.
        var clock = new Clock();
        var plane = new Plane(AFlight(Opened, 1042) with { CreatedAt = T0.AddMinutes(5) });
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);
        var older = AFlight("f-older", 1041);
        var oldest = AFlight("f-oldest", 1040) with { CreatedAt = T0.AddMinutes(-5) };

        var before = Waiting(older, oldest) with { FlightSelected = 1 };
        await Assert.That(PaneText.Detailed(before)!.FlightId).IsEqualTo("f-oldest");

        var state = expectations.Advance(before);
        state = expectations.Advance(state);

        await Assert.That(PaneText.Detailed(state)!.FlightId)
            .IsEqualTo("f-oldest")
            .Because("a row arriving above the cursor is not a reason for the cursor to move "
                   + "to another flight.");
    }

    [Test]
    public async Task Not_there_yet_is_asked_again_later_and_not_at_once()
    {
        var clock = new Clock();
        var plane = new Plane();
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting());
        state = expectations.Advance(state);
        state = expectations.Advance(state);

        await Assert.That(plane.Asked).IsEqualTo(1)
            .Because("a look that found nothing is followed by a wait, not by another look on "
                   + "the next tick.");

        clock.Now += Expectations.FirstGap;
        state = expectations.Advance(state);

        await Assert.That(plane.Asked).IsEqualTo(2);
        await Assert.That(state.Expecting.Count).IsEqualTo(1);
    }

    [Test]
    public async Task The_wait_doubles_and_stops_at_two_seconds()
    {
        var clock = new Clock();
        var plane = new Plane();
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting());
        var gaps = new List<TimeSpan>();

        // BOUNDED, so a watcher that never looks again fails here rather than
        // spinning: ten seconds of ticks is five times the longest gap.
        for (var looked = plane.Asked; gaps.Count < 6;)
        {
            var from = clock.Now;

            while (plane.Asked == looked && clock.Now - from < TimeSpan.FromSeconds(10))
            {
                clock.Now += TimeSpan.FromMilliseconds(50);
                state = expectations.Advance(state);
            }

            gaps.Add(clock.Now - from);
            looked = plane.Asked;
        }

        await Assert.That(gaps.Select(g => (int)g.TotalMilliseconds)).IsEquivalentTo(
            new[] { 250, 500, 1000, 2000, 2000, 2000 })
            .Because("soon at first, when the flight is most likely to be a moment away, and "
                   + "no oftener than every two seconds after that.");
    }

    [Test]
    public async Task One_look_at_a_time()
    {
        var clock = new Clock();
        var plane = new Plane { Holds = true };
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting());

        for (var tick = 0; tick < 20; tick++)
        {
            clock.Now += TimeSpan.FromMilliseconds(250);
            state = expectations.Advance(state);
        }

        await Assert.That(plane.Asked).IsEqualTo(1)
            .Because("a control plane slower than the gap would otherwise have a pile of "
                   + "requests in the air, all asking the same question.");
    }

    [Test]
    public async Task A_look_that_fails_found_nothing()
    {
        var clock = new Clock();
        var plane = new Plane { Fails = true };
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting());
        state = expectations.Advance(state);
        clock.Now += Expectations.FirstGap;
        state = expectations.Advance(state);

        await Assert.That(plane.Asked).IsEqualTo(2)
            .Because("an unreachable control plane is a look that saw nothing, and the next "
                   + "one is still worth making.");
        await Assert.That(state.Expecting.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_flight_never_seen_is_given_up_on_and_said()
    {
        var clock = new Clock();
        var plane = new Plane();
        var expectations = new Expectations(Expectations.Looks(plane.Read), clock);

        var state = expectations.Advance(Waiting());

        while (clock.Now - T0 <= Expectations.Patience + TimeSpan.FromSeconds(1))
        {
            clock.Now += TimeSpan.FromMilliseconds(250);
            state = expectations.Advance(state);
        }

        var asked = plane.Asked;
        clock.Now += TimeSpan.FromSeconds(10);
        state = expectations.Advance(state);

        await Assert.That(state.Expecting).IsEmpty();
        await Assert.That(state.Notifications.Select(n => n.Kind)).IsEquivalentTo(
            new[] { NotificationKind.NotListedYet })
            .Because("going quiet about a flight somebody is waiting for is the one answer "
                   + "worse than saying it has not appeared.");
        await Assert.That(plane.Asked).IsEqualTo(asked)
            .Because("and it stops asking.");
    }

    [Test]
    public async Task Notifications_go_fifteen_seconds_after_the_last_arrived()
    {
        var clock = new Clock();
        var expectations = new Expectations(Expectations.Looks(new Plane(AFlight(Opened, 1042)).Read), clock);

        var state = expectations.Advance(Waiting());
        state = expectations.Advance(state);

        clock.Now += Expectations.NotificationsLast - TimeSpan.FromMilliseconds(250);
        state = expectations.Advance(state);
        await Assert.That(state.Notifications.Count).IsEqualTo(1);

        clock.Now += TimeSpan.FromMilliseconds(250);
        state = expectations.Advance(state);
        await Assert.That(state.Notifications).IsEmpty()
            .Because("a notice nobody looked at is gone when the corner of the screen has been "
                   + "quiet for long enough.");
    }

    [Test]
    public async Task Not_while_somebody_is_reading_them()
    {
        var clock = new Clock();
        var expectations = new Expectations(Expectations.Looks(new Plane(AFlight(Opened, 1042)).Read), clock);

        var state = expectations.Advance(Waiting());
        state = expectations.Advance(state) with { Mode = UiMode.Notifications };

        clock.Now += Expectations.NotificationsLast + TimeSpan.FromSeconds(30);
        state = expectations.Advance(state);
        await Assert.That(state.Notifications.Count).IsEqualTo(1)
            .Because("taking a notice away from under the person reading it is the popup's "
                   + "version of focus being taken away.");

        state = expectations.Advance(state with { Mode = UiMode.Normal });
        clock.Now += Expectations.NotificationsLast - TimeSpan.FromMilliseconds(250);
        state = expectations.Advance(state);
        await Assert.That(state.Notifications.Count).IsEqualTo(1)
            .Because("the clock starts again when they are put down, not when they arrived.");

        clock.Now += TimeSpan.FromMilliseconds(250);
        state = expectations.Advance(state);
        await Assert.That(state.Notifications).IsEmpty();
    }

    [Test]
    public async Task A_new_notification_shows_itself_unless_somebody_is_reading()
    {
        var clock = new Clock();
        var expectations = new Expectations(Expectations.Looks(new Plane(AFlight(Opened, 1042)).Read), clock);
        var earlier = new Notification { Kind = NotificationKind.FlightOpened, FlightId = "f-earlier" };

        var unfocused = expectations.Advance(Waiting() with { Notifications = [earlier] });
        unfocused = expectations.Advance(unfocused);

        await Assert.That(unfocused.NotificationAt).IsEqualTo(1)
            .Because("the newest is the one worth drawing when nobody is reading.");

        var reading = new Expectations(Expectations.Looks(new Plane(AFlight(Opened, 1042)).Read), clock);
        var focused = reading.Advance(Waiting() with { Notifications = [earlier], Mode = UiMode.Notifications });
        focused = reading.Advance(focused);

        await Assert.That(focused.Notifications.Count).IsEqualTo(2);
        await Assert.That(focused.NotificationAt).IsEqualTo(0)
            .Because("the page does not move under somebody reading it; only the count does.");
    }

    private static FlightSummary AFlight(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = $"work {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = T0;

        public DateTimeOffset UtcNow => Now;
    }

    /// <summary>One flight's read, answering what the test says it answers.</summary>
    private sealed class Plane(FlightSummary? has = null)
    {
        internal int Asked { get; private set; }

        /// <summary>Never answers, so a look stays in the air.</summary>
        internal bool Holds { get; init; }

        internal bool Fails { get; init; }

        internal Task<FlightSummary?> Read(string id)
        {
            Asked++;

            if (Holds)
            {
                return new TaskCompletionSource<FlightSummary?>().Task;
            }

            if (Fails)
            {
                return Task.FromException<FlightSummary?>(
                    new HttpRequestException("nobody is listening"));
            }

            return Task.FromResult(has?.FlightId == id ? has : null);
        }
    }
}
