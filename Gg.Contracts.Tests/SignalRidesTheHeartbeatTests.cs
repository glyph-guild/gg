using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Signalling rides a poll the runner was already making.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013 wanted candidates to trickle "over the runner's existing poll
/// rather than needing a push channel", and the poll was already here.</b> So
/// was the cadence control that makes it responsive: <c>NextHeartbeatSeconds</c>
/// exists so "the client respects a cadence rather than inventing one". A
/// pending introduction shortens it. No new loop, no long poll, no listening
/// socket.
/// </para>
/// <para>
/// <b>Which makes that member load-bearing for two things</b>, and the floor
/// below is what stops the second one being usable against the first.
/// </para>
/// </remarks>
public class SignalRidesTheHeartbeatTests
{
    private static Gg.Contracts.Description.Endpoint Signal =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/signal" && e.Method == "POST");

    [Test]
    public async Task The_heartbeat_carries_what_is_waiting()
    {
        var members = typeof(HeartbeatAccepted).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains("Introductions");
        await Assert.That(members).Contains("NextHeartbeatSeconds")
            .Because("the cadence is what makes the poll responsive; carrying work on a poll "
                   + "nobody can hurry is carrying it slowly.");
    }

    [Test]
    public async Task An_idle_fleet_sends_exactly_what_it_always_did()
    {
        // ABSENT RATHER THAN AN EMPTY LIST. The two repositories are not
        // upgraded in step, and a heartbeat body that grew a `[]` would be a new
        // shape arriving at an old reader for no reason at all.
        //
        // EVERY OPTIONAL MEMBER, not the one this started with. `Offered` joined
        // it and inherits the rule rather than being trusted to have thought of
        // it - the whole point of the poll carrying things is that a fleet with
        // nothing waiting sends what it always sent.
        foreach (var member in (string[])["Introductions", "Offered"])
        {
            var declared = typeof(HeartbeatAccepted).GetProperty(member)!;

            await Assert.That(new NullabilityInfoContext().Create(declared).WriteState)
                .IsEqualTo(NullabilityState.Nullable)
                .Because($"{member} is absent when there is none, so an idle fleet's body "
                       + "does not grow a shape an older reader has to skip.");
        }

        await Assert.That(ProtocolSurface.JsonMembers[typeof(HeartbeatAccepted)])
            .IsEquivalentTo(new[] { "nextHeartbeatSeconds", "introductions", "offered" });
    }

    [Test]
    public async Task What_is_waiting_is_a_sealed_offer_and_an_id()
    {
        // NOT THE CAPABILITY AS THE ID. The capability is what the runner
        // CHECKS; the id is what the answer is filed under. One value for both
        // would make a runner disclose its authorisation to say which question
        // it was answering.
        var members = typeof(PendingIntroduction).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo(new[] { "IntroductionId", "Offer" })
            .Because("a member describing the offer would be the relay reading the thing it "
                   + "exists not to read, arriving as a convenience.");
    }

    [Test]
    public async Task The_runner_answers_by_posting_outward()
    {
        // THE POSTURE THE WHOLE TRANSPORT CHOICE RESTS ON. The offer arrived on
        // a poll the runner made; the answer goes back the same way round. A
        // machine that never listens is what makes ICE viable here at all.
        await Assert.That(Signal.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(Signal.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
        await Assert.That(Signal.Method).IsEqualTo("POST");
    }

    [Test]
    public async Task The_control_plane_files_the_answer_rather_than_waiting_for_it()
    {
        // 202 RATHER THAN 200. A route that waited for the far end would put the
        // relay inside the conversation, which is the one thing it must not be.
        await Assert.That(Signal.Statuses).Contains(202);
        await Assert.That(Signal.Statuses).DoesNotContain(200);
    }

    [Test]
    public async Task An_answer_carries_no_capability_back()
    {
        var members = typeof(RunnerSignalAnswer).GetProperties().Select(p => p.Name);

        await Assert.That(members).DoesNotContain("Capability")
            .Because("authorisation was checked when the offer was opened; a capability going "
                   + "back would be a grant travelling the wrong way.");
    }

    [Test]
    public async Task A_runner_will_not_be_hurried_past_the_floor()
    {
        // THE DANGER THIS MEMBER CREATES, BOUNDED. `NextHeartbeatSeconds` now
        // decides two things, and a control plane that asked for a hundredth of
        // a second would make a whole fleet chatty at the runner's expense.
        await Assert.That(HeartbeatCadence.Respecting(0)).IsEqualTo(HeartbeatCadence.Floor);
        await Assert.That(HeartbeatCadence.Respecting(-30)).IsEqualTo(HeartbeatCadence.Floor);

        // AND THE OTHER DIRECTION, which fails worse: an interval longer than
        // the staleness bound makes a healthy runner read as offline, taking it
        // out of the fleet with nothing having failed.
        await Assert.That(HeartbeatCadence.Respecting(86_400)).IsEqualTo(HeartbeatCadence.Ceiling);
    }

    [Test]
    public async Task An_ordinary_cadence_passes_through_unchanged()
    {
        // THE LIVENESS HALF. A clamp that flattened everything would satisfy the
        // assertions above and would take the cadence control away entirely -
        // which is the thing that makes a waiting introduction get picked up.
        await Assert.That(HeartbeatCadence.Respecting(30)).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(HeartbeatCadence.Respecting(2)).IsEqualTo(TimeSpan.FromSeconds(2))
            .Because("two seconds is a handshake's cadence and has to survive the floor.");
    }
}
