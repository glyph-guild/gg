using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight that cannot start says so, by name, on the summary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusal at apply, waiting at flight.</b> Apply has an actor at the
/// keyboard to inform; a queued flight has nobody, so it waits loudly instead
/// of dying quietly. The waiting sentence lands on <see cref="FlightSummary"/>
/// - the one document <c>gg flights</c>, <c>gg show</c> and <c>--json</c> all
/// read - rather than behind a route no verb uses.
/// </para>
/// <para>
/// <b>Null means not waiting,</b> the same absence rule as
/// <c>LeaseClaimStatus.Lease</c>. A flight matched or flying carries no
/// sentence, and "waiting: nothing" is not a state this member can express.
/// </para>
/// </remarks>
public class FlightWaitingSurfaceTests
{
    private static FlightSummary Summary(Reason? waiting, IReadOnlyList<string>? labels = null) => new()
    {
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = "GG-1042",
        Name = "payments",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
        CreatedAt = DateTimeOffset.UnixEpoch,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "1.0.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "3",
        Attempts = 0,
        Facts = [],
        RequiredLabels = labels ?? [],
        Waiting = waiting,
    };

    [Test]
    public async Task A_flight_nobody_can_lease_names_what_it_waits_for()
    {
        var waiting = Summary(
            Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=aspire-payments"]),
            ["environment=aspire-payments"]);

        await Assert.That(waiting.Waiting!.Params).Contains("environment=aspire-payments")
            .Because("a name is what somebody can act on; a count says only that something "
                   + "is wrong.");
        await Assert.That(Reason.Sentence(waiting.Waiting.Kind, waiting.Waiting.Params))
            .Contains("environment=aspire-payments");
        await Assert.That(waiting.RequiredLabels).Contains("environment=aspire-payments");
    }

    [Test]
    public async Task A_flight_that_is_not_waiting_says_nothing()
    {
        var summary = Summary(waiting: null);

        await Assert.That(summary.Waiting).IsNull();
        await Assert.That(summary.RequiredLabels).IsEmpty()
            .Because("every flight created before selections existed requires no label, which "
                   + "is what those flights meant.");
    }

    [Test]
    public async Task The_waiting_members_are_on_the_declared_wire_surface()
    {
        var members = ProtocolSurface.JsonMembers[typeof(FlightSummary)];

        await Assert.That(members).Contains("requiredLabels");
        await Assert.That(members).Contains("waiting");
    }
}
