using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The control plane tells a console that something changed, and the console
/// reads what changed through the doors it already uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> A console refreshes on a thirty-second timer, and the
/// flight a person just opened is not in the list the write re-read: the row is
/// projected a hop later, so it appears on the NEXT tick. Polling faster costs
/// every console a request a second for the rare moment something moved. A
/// stream held open costs nothing while nothing moves and answers within a beat
/// when something does.
/// </para>
/// <para>
/// <b>A doorbell, not a feed.</b> A notice names a topic and, where there is
/// one, an id - never the row. The row is read through the route that already
/// serves it, so what a console may see is decided in one place, by the route
/// that has always decided it, and a notice that is missed costs one refresh
/// interval rather than a console that disagrees with the control plane.
/// </para>
/// </remarks>
public class AConsoleHearsWhatChangedTests
{
    private const string Route = "/v1/changes";

    private static Endpoint? Declared() =>
        ProtocolSurface.Endpoints.SingleOrDefault(e => e.Method == "GET" && e.Path == Route);

    [Test]
    public async Task The_stream_is_a_read_a_person_makes()
    {
        var declared = Declared();

        await Assert.That(declared).IsNotNull()
            .Because("a console has to be able to hear that something changed without asking "
                   + "every few seconds whether it has.");
        await Assert.That(declared!.Audience).IsEqualTo(Audience.Developer)
            .Because("it says which of a TENANT's flights, gates and runners moved, which is "
                   + "what the flight list says - and a runner is not allowed that list.");
        await Assert.That(declared.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        await Assert.That(declared.Request).IsNull()
            .Because("a GET carries no body; the session says whose changes these are.");
    }

    [Test]
    public async Task It_answers_like_every_other_read()
    {
        await Assert.That(Declared()!.Statuses).IsEquivalentTo(
                (int[])[200, 401, 403, ProtocolSurface.ProtocolTooOld])
            .Because("200 opens the stream; an expired session, a runner token and a gg below "
                   + "the floor are refused the way every other developer read refuses them.");
    }

    [Test]
    public async Task Each_event_carries_a_notice()
    {
        await Assert.That(Declared()!.Response).IsEqualTo(typeof(ChangeNotice))
            .Because("the body is a stream of events, and each `changed' event's data is one "
                   + "notice - so the notice is what both sides serialize.");
    }

    [Test]
    public async Task It_is_governed_so_nothing_undeclared_can_grow_beside_it()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains(Route)
            .Because("a route under here says what moved in a tenant, and one the control plane "
                   + "served without declaring would be an unaudited way to learn that.");
    }

    [Test]
    public async Task A_notice_names_what_moved_and_carries_no_row()
    {
        var members = typeof(ChangeNotice)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(members).IsEquivalentTo((string[])["Id", "Topic"])
            .Because("a doorbell. The row is read through the route that serves it, so what a "
                   + "console may see is decided where it always was - a notice carrying the "
                   + "row would be a second read surface with its own idea of who may see it.");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ChangeNotice)])
            .IsEquivalentTo((string[])["topic", "id"]);
    }

    [Test]
    public async Task The_topics_are_the_panes_that_can_go_stale()
    {
        await Assert.That(ChangeTopics.All).IsEquivalentTo((string[])
            [ChangeTopics.Ready, ChangeTopics.Flights, ChangeTopics.Gates,
             ChangeTopics.Board, ChangeTopics.Runners]);
        await Assert.That(ChangeTopics.Flights).IsEqualTo("flights");
        await Assert.That(ChangeTopics.Gates).IsEqualTo("gates");
        await Assert.That(ChangeTopics.Board).IsEqualTo("board");
        await Assert.That(ChangeTopics.Runners).IsEqualTo("runners");
    }

    [Test]
    public async Task Ready_means_everything_may_have_changed()
    {
        // A CONNECTION'S FIRST WORD, and a topic because of what it asks a
        // console to do. Whatever moved while nobody was connected was never
        // announced, so the only safe reading of "you are connected now" is
        // "read everything once".
        await Assert.That(ChangeTopics.Ready).IsEqualTo("ready");
        await Assert.That(ChangeEvents.Ready).IsEqualTo(ChangeTopics.Ready);
    }

    [Test]
    public async Task The_events_are_ready_and_changed()
    {
        await Assert.That(ChangeEvents.All).IsEquivalentTo((string[])["ready", "changed"])
            .Because("the server says it is listening once, and then says what changed. An "
                   + "event name a client does not know is skipped, so these are the whole "
                   + "vocabulary a client acts on.");
        await Assert.That(ChangeEvents.Keepalive).IsEqualTo(TimeSpan.FromSeconds(15))
            .Because("a stream that says nothing for minutes is indistinguishable from one a "
                   + "proxy cut, and a client can only tell them apart if it knows how long "
                   + "silence is allowed to last.");
    }

    [Test]
    public async Task The_notice_is_a_pinned_wire_type()
    {
        await Assert.That(typeof(ChangeNotice).GetCustomAttribute<PinnedIdAttribute>()).IsNotNull();
        await Assert.That(Vocabulary.Types).Contains(typeof(ChangeNotice));
    }
}
