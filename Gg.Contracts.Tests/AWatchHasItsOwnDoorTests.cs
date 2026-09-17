using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A watch applies through a door of its own, on the strategy's shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>The document existed and nothing outside the control plane could reach
/// it.</b> <c>EnvelopeStream.ApplyWatchAsync</c> landed in good-grief #445 with
/// no route in front of it — a port nothing calls, which is the shape this
/// codebase keeps finding after the fact. These three declarations are what a
/// person applies, reads and lists a watch through.
/// </para>
/// <para>
/// <b>A door of its own rather than the named-envelope door</b>, for the reason
/// a strategy has one: the body is a different document class, and
/// <c>NamedEnvelopes.Rendered</c> deliberately keeps both out of the envelope
/// listing. One door per class means the request type says what the body is,
/// rather than a union the server has to guess at.
/// </para>
/// <para>
/// <b>PUT declares 202, and that is a promise the control plane has to
/// keep.</b> A widening watch is diverted to the gate rather than refused — so
/// the side that pins this version must serve the diversion and its return
/// path, which good-grief #445 found missing and recorded rather than
/// pretending.
/// </para>
/// </remarks>
public class AWatchHasItsOwnDoorTests
{
    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e =>
            e.Method == method && e.Path == path);

    [Test]
    public async Task A_watch_is_applied_through_its_own_door()
    {
        var apply = Declared("PUT", "/v1/airspace/watches/{name}");

        await Assert.That(apply.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner performs a sweep; it never authors the watch that governs "
                   + "one - the strategy door's argument, one document over.");

        await Assert.That(apply.Request).IsEqualTo(typeof(WatchDocument));
        await Assert.That(apply.Response).IsEqualTo(typeof(EnvelopeApplied))
            .Because("the same answer every document on this stream gives: a version, "
                   + "whether it changed, and - when it diverted - the flight and who it "
                   + "awaits.");

        await Assert.That(apply.Statuses).Contains(202)
            .Because("a widening is diverted to the gate rather than refused, which is the "
                   + "stream's own rule and the one a watch has to honour to be the same "
                   + "kind of document.");

        await Assert.That(apply.Statuses).Contains(400);
    }

    [Test]
    public async Task One_watch_and_every_watch_can_be_read_back()
    {
        var one = Declared("GET", "/v1/airspace/watches/{name}");
        var all = Declared("GET", "/v1/airspace/watches");

        await Assert.That(one.Response).IsEqualTo(typeof(WatchState));
        await Assert.That(one.Statuses).Contains(404)
            .Because("a name with no watch in force is a different fact from a watch that "
                   + "sweeps nothing, which cannot exist - the document requires its filter.");

        await Assert.That(all.Response).IsEqualTo(typeof(WatchList));
        await Assert.That(all.Statuses).DoesNotContain(404)
            .Because("a tenant watching nothing is a state rather than an error, so the list "
                   + "answers 200 with nothing in it.");
    }

    [Test]
    public async Task What_comes_back_is_the_document_with_its_version()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchState)])
            .IsEquivalentTo((string[])["name", "version", "appliedAt", "watch"])
            .Because("the strategy state's shape, one member renamed - a reader holding one "
                   + "already knows how to hold the other.");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchList)])
            .IsEquivalentTo((string[])["watches"])
            .Because("an envelope rather than a bare array, for the reason `StrategyList` "
                   + "is one: a bare array has nowhere to put the paging this will grow.");
    }
}
