using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The flight checklist: what must hold before a flight's clock starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, never authored</b> - the third flight artifact. The plan says
/// what would happen, the log says what happened, the checklist says what must
/// hold first. It is computed from the envelope's selections and evaluated
/// through the lease matcher's own containment, which is why the satisfier
/// column here has exactly two values: in this slice a requirement is either
/// already true via matching, or nobody in the fleet can satisfy it. Strategy
/// actions and human assists arrive with the phases that own them; a rendered
/// placeholder for machinery that does not exist would be the checklist
/// containing a promise, which is the same defect as containing a procedure.
/// </para>
/// <para>
/// <b>The satisfier vocabulary is CLOSED at two, and that is the assertion.</b>
/// A third value is a design event - it means a strategy exists - and it must
/// arrive as a deliberate contract change, not leak in as a string.
/// </para>
/// </remarks>
public class ChecklistSurfaceTests
{
    [Test]
    public async Task The_satisfier_column_has_exactly_two_values()
    {
        await Assert.That(ChecklistSatisfiers.All)
            .IsEquivalentTo((string[])[ChecklistSatisfiers.MatchingRunner, ChecklistSatisfiers.Nobody]);
        await Assert.That(ChecklistSatisfiers.MatchingRunner).IsEqualTo("already-true-via-matching");
        await Assert.That(ChecklistSatisfiers.Nobody).IsEqualTo("nobody-declared-capability-gap");
    }

    [Test]
    public async Task The_checklist_types_are_in_the_vocabulary()
    {
        await Assert.That(Vocabulary.Types).Contains(typeof(Checklist));
        await Assert.That(Vocabulary.Types).Contains(typeof(ChecklistItem));
    }

    [Test]
    public async Task The_checklist_types_declare_their_members()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(Checklist)])
            .IsEquivalentTo((string[])["envelopeVersion", "flightNumber", "environment", "repository",
                             "requiredLabels", "items"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ChecklistItem)])
            .IsEquivalentTo((string[])["requirement", "verification", "satisfier", "whenUnmet",
                             "disposition"]);
    }

    [Test]
    public async Task A_satisfied_item_carries_no_unmet_sentence()
    {
        // WhenUnmet is the waiting sentence, and a satisfied row must not have
        // one - a sentence that is always present is a sentence nobody reads.
        var item = new ChecklistItem
        {
            Requirement = "environment=aspire-payments",
            Verification = "a live runner's advertised labels contain it",
            Satisfier = ChecklistSatisfiers.MatchingRunner,
            WhenUnmet = null,
            Disposition = LabelDispositions.Stated,
        };

        await Assert.That(item.WhenUnmet).IsNull();
    }

    [Test]
    public async Task The_checklist_has_its_read_endpoints()
    {
        // Both rides existing governed prefixes: the tenant-level plan under
        // /v1/envelope, the per-flight checklist under /v1/flights. No third
        // prefix arrives for this.
        var plan = ProtocolSurface.Endpoints.Single(
            e => e.Method == "GET" && e.Path == "/v1/envelope/plan");
        var flight = ProtocolSurface.Endpoints.Single(
            e => e.Method == "GET" && e.Path == "/v1/flights/{ref}/checklist");

        await Assert.That(plan.Response).IsEqualTo(typeof(Checklist));
        await Assert.That(plan.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(plan.Statuses).Contains(404)
            .Because("a tenant that has never applied an envelope has no plan to render, and "
                   + "that is a different answer from a plan with nothing on it.");

        await Assert.That(flight.Response).IsEqualTo(typeof(Checklist));
        await Assert.That(flight.Audience).IsEqualTo(Audience.Developer);
    }
}
