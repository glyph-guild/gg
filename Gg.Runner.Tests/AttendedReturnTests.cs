using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// What the person decided, and the disposition that matches it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A PERSON WHO WALKED AWAY READS AS `completed` TODAY.</b> The runner holds
/// the tree for its window and then releases the lease with
/// <c>RunnerDisposition.Completed</c>, unconditionally. That is right for an
/// agent, whose outcome was measured and shipped before the release — and on an
/// attended flight nothing was measured, so `completed` is not a report of what
/// happened, it is the only value the code had.
/// </para>
/// <para>
/// <b>The reader is HANDED to the runner, and the reference graph is why.</b>
/// S26.7-01 asks for the decision to be read through
/// <c>TakeoverReturnReader</c> rather than a fourth way of asking one question.
/// That reader lives in <c>Gg.Client</c>, and <c>Gg.Runner</c> cannot see it:
/// the runner is treated as hostile and the reference graph keeps them apart —
/// <c>Gg.Local</c> is no help either, since it does not reference
/// <c>Gg.Contracts</c> and the decision is a contract type. So <c>Gg.Cli</c>
/// passes it in, which is the same join it already makes for credentials and
/// says so: <i>"the two halves are joined here because this is the only project
/// that can see both."</i>
/// </para>
/// <para>
/// <b>Which is why this file asserts the MAPPING and not the parsing.</b> What
/// a return file is, how large it may be and when it is refused are
/// <c>TakeoverReturnReader</c>'s and are tested where it lives. What is here is
/// the runner's half: a decision becomes a disposition, a diagnosis becomes a
/// refusal that is not `completed`, and nothing at all becomes `abandoned`.
/// </para>
/// <para>
/// <b>And the fleet is untouched, guarded where it already was.</b> A flight an
/// agent flew has no person to decide and no return file, and it still releases
/// `completed` — its outcome was measured and shipped before the release.
/// Reading "no decision" as abandonment there would mark every agent flight in
/// the estate as abandoned, and <c>RunnerLoopTests</c> goes red if it does: it
/// asserts <c>release:7:completed</c> on an ordinary flight. Nothing is
/// duplicated here to say so.
/// </para>
/// </remarks>
public class AttendedReturnTests
{
    private static Func<string, string, (TakeoverReturn?, string?)> Left(
        string? outcome = null, string? diagnosis = null) =>
        (_, flightId) => (
            outcome is null
                ? null
                : new TakeoverReturn { FlightId = flightId, Outcome = outcome },
            diagnosis);

    /// <summary>What the runner told the control plane when it let go.</summary>
    private static LeaseReleaseRequest Released(FakeProtocol protocol) =>
        System.Text.Json.JsonSerializer.Deserialize<LeaseReleaseRequest>(
            protocol.Serialized.Last(line =>
                line.Contains("disposition", StringComparison.Ordinal)),
            System.Text.Json.JsonSerializerOptions.Web)!;

    /// <summary>The disposition the lease was released with.</summary>
    private static string ReleasedWith(FakeProtocol protocol) =>
        protocol.Calls.Last(call => call.StartsWith("release:", StringComparison.Ordinal))
            .Split(':')[2];

    // ---- S26.7-04 ----

    [Test]
    public async Task A_person_who_walked_away_does_not_read_as_completed()
    {
        // NOT A FAILURE, AND NOT A COMPLETION. Somebody handed a terminal who
        // wrote nothing is the ordinary end of an abandoned session, and it is
        // recorded as itself. `completed` would be a claim about work nobody
        // checked, on the one path where nothing else can correct it.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(returns: Left());

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Abandoned);
    }

    [Test]
    public async Task A_person_who_finished_reads_as_completed()
    {
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed));

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Completed);
    }

    [Test]
    public async Task A_person_who_gave_up_says_so()
    {
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Abandoned));

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Abandoned);
    }

    [Test]
    public async Task Handing_it_back_is_not_finishing_it()
    {
        // THE THIRD OUTCOME, and it is the one that must not read as either of
        // the others. Handing back is a person saying the flight should go on
        // without them - the work is not done and they did not give up on it.
        // What the fleet then does with it is step 9's; that it is not
        // `completed` is this step's.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.HandingBack));

        await Assert.That(ReleasedWith(protocol)).IsNotEqualTo(RunnerDisposition.Completed);
    }

    // ---- S26.7-03 ----

    [Test]
    public async Task A_refused_return_carries_its_diagnosis_and_does_not_complete_the_flight()
    {
        // NOT SILENTLY APPLIED. A decision file left over from a previous
        // takeover in the same tree names a flight that is not this one, and
        // applying it would close somebody's flight on a sentence they wrote
        // about a different one. The reader produces the diagnosis; what is
        // asserted here is that the runner CARRIES it rather than dropping it
        // and releasing something plausible.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(diagnosis: "The return file decides flight 'other' and the flight "
                                   + "taken was 'flight-1'."));

        await Assert.That(ReleasedWith(protocol)).IsNotEqualTo(RunnerDisposition.Completed);

        // THE VALUE, NOT THE ENCODING. The recorded body escapes apostrophes as
        // \u0027, so a substring match against the JSON asserts the serializer
        // rather than the diagnosis.
        await Assert.That(Released(protocol).Detail)
            .Contains("decides flight 'other'")
            .Because("a refusal that says only 'wrong flight' sends somebody looking for "
                   + "which, and the reader already composed the sentence that says.");
    }

    [Test]
    public async Task A_diagnosis_beats_a_decision_that_came_with_it()
    {
        // BOTH CANNOT BE TRUE. The reader answers a decision OR a diagnosis, and
        // a runner that preferred the decision would apply a file the reader had
        // already refused - which is the whole of what "not silently applied"
        // forbids, arriving through the caller instead of the parser.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed, diagnosis: "refused for a reason"));

        await Assert.That(ReleasedWith(protocol)).IsNotEqualTo(RunnerDisposition.Completed);
    }

    // ---- S26.8-09 and S26.8-10 ----

    [Test]
    public async Task A_flight_still_waiting_on_something_does_not_land_because_the_person_said_so()
    {
        // THE PERSON ANSWERS FOR THEIR WORK; THEY DO NOT ANSWER THE GATE. A
        // hand-flight can open one - the envelope decides that, not who flew it
        // - and the person at the terminal is not who may close it. Rule 8.
        //
        // `completed` is the one disposition that ENDS a flight:
        // LeaseDispositions.ExitFor maps it to `landed`, while `abandoned` and
        // `expired` map to no ending at all, which is what lets the sweep
        // re-offer work that is not finished. So releasing `completed` here
        // would record a landing while a gate nobody answered is still open -
        // and the exit claim is first-writer-wins, so the truthful write would
        // lose to the premature one and there is no second chance.
        //
        // The runner cannot see the gate, and does not need to: `settled` means
        // every fact this flight shipped has been evaluated, so unsettled is
        // "still waiting on something" whatever that something is.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed), settles: false);

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Abandoned)
            .Because("the person finished their work and the flight has not finished. "
                   + "Abandoned maps to no ending, so nothing is closed and the work is "
                   + "still there to be picked up once somebody answers.");

        await Assert.That(Released(protocol).Detail).IsNotNull()
            .Because("a release that says less than the person did is one they cannot "
                   + "reconcile with what they typed.");
    }

    [Test]
    public async Task A_settled_flight_still_lands_when_the_person_says_it_is_done()
    {
        // THE HALF THAT MUST NOT MOVE. Refusing to land whenever anything is
        // unsettled would be easy and would mean no hand-flight ever completes,
        // which is the same silence one condition further on.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed), settles: true);

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Completed);
    }

    // ---- S26.8-07, and the hole it found ----

    [Test]
    public async Task A_decision_still_outstanding_is_not_finished_because_the_person_says_so()
    {
        // CLEARED TO PUSH AND NOT ADMITTED IS A GATE SOMEBODY HAS NOT ANSWERED.
        // The two are read independently: the push is granted when no machine
        // obligation is violated, the proposal when every requirement is
        // satisfied. So a flight whose work reached the remote and whose pull
        // request was not opened is one waiting on a person - which is exactly
        // the state a hand-flight reaches when its own gate is open.
        //
        // AND `settled` DOES NOT COVER IT. Settled means every FACT has been
        // evaluated; a pending human decision is not a fact, so the flight is
        // settled and outstanding at the same time. Reading only `settled` here
        // - which is what this runner did until now - records `landed` on a
        // flight whose gate nobody answered, and the exit claim is
        // first-writer-wins so nothing corrects it afterwards.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed),
            settles: true,
            push: new BranchPush
            {
                Branch = "gg/GG-1042",
                BaseRef = "refs/heads/main",
                Slug = "acme/widgets",
                Reason = "cleared to push",
            },
            admission: null);

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Abandoned)
            .Because("the person finished their work and somebody else has not answered the "
                   + "gate it opened. Abandoned records no ending, so the flight stays open "
                   + "until they do.");
    }

    [Test]
    public async Task An_admitted_flight_the_person_finished_still_lands()
    {
        // THE HALF THAT MUST NOT MOVE, again. Admitted means every requirement
        // is satisfied - there is no gate outstanding - so a person saying they
        // finished is the last word, and refusing to land here would mean no
        // hand-flight with a destination ever completes.
        var (_, protocol) = await AttendedExecutorTests.FlownAsync(
            returns: Left(TakeoverOutcomes.Completed),
            settles: true,
            push: new BranchPush
            {
                Branch = "gg/GG-1042",
                BaseRef = "refs/heads/main",
                Slug = "acme/widgets",
                Reason = "cleared to push",
            },
            admission: new DestinationAdmission
            {
                DestinationId = "pr",
                Branch = "gg/GG-1042",
                BaseRef = "refs/heads/main",
                Slug = "acme/widgets",
                Reason = "every requirement satisfied",
            });

        await Assert.That(ReleasedWith(protocol)).IsEqualTo(RunnerDisposition.Completed);
    }
}
