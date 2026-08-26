using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The door a named document applies through, declared before it is served.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice nine deferred this three times, and the working copy cannot exist
/// without it.</b> Only root and strategies had doors: a work kind or a narrowing
/// could not be read back, could not be applied, and its widening was refused
/// outright for want of a flight to ride. Two of the four directories ADR-0016
/// draws were unwritable, which is why this is slice thirteen's floor rather than
/// its opening convenience.
/// </para>
/// <para>
/// <b>Both sides fail closed on their own format.</b> Whether a body matches the
/// NAME's role is the control plane's to answer, because only the topology knows
/// the role. Whether it is a coherent body at all is answerable here, with no
/// lookup and no session - so it is answered here.
/// </para>
/// </remarks>
public class NamedEnvelopeTests
{
    private static Envelope Anything() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    private static EnvelopeNarrowing Constraints() => new()
    {
        Obligations =
        [
            new Obligation
            {
                Id = "two-approvers",
                Check = ObligationChecks.Human,
                Approver = "an-architect",
            },
        ],
    };

    [Test]
    public async Task An_apply_carrying_one_document_is_coherent()
    {
        await Assert.That(new NamedEnvelopeApply { Envelope = Anything() }.Validate()).IsNull();
        await Assert.That(new NamedEnvelopeApply { Narrowing = Constraints() }.Validate()).IsNull();
    }

    [Test]
    public async Task An_apply_carrying_neither_document_is_refused_rather_than_read_as_a_retirement()
    {
        // An empty body is the shape somebody reaches for to mean "delete this".
        // Retiring a name is a terminal version - gated, attributed and versioned
        // like any other change - so an empty body has to be a refusal here or it
        // becomes an ungated way to stop a document applying.
        var refusal = new NamedEnvelopeApply().Validate();

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("terminal version");
    }

    [Test]
    public async Task An_apply_carrying_both_documents_is_refused_rather_than_resolved()
    {
        // A name has one role, so only one of these could ever be applied.
        // Picking one would be the control plane choosing which policy a person
        // meant, which is the silent no-op class this product exists to name.
        var refusal = new NamedEnvelopeApply { Envelope = Anything(), Narrowing = Constraints() }
            .Validate();

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("ONE document");
    }

    [Test]
    public async Task The_precondition_is_not_a_member_of_the_body()
    {
        // based-on: is consumed by the parser and travels as a query parameter.
        // As a member it would be catastrophic three times over: the stored form
        // is the idempotence key, so every pull-and-reapply would mint a version
        // per document; the field-by-field comparison decides direction, so an
        // unordered scalar moving would divert every apply to a human gate; and
        // the composition digest hashes the same bytes, so every pin would move.
        var members = typeof(NamedEnvelopeApply).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[] { "Envelope", "Narrowing" });
    }

    [Test]
    public async Task The_three_doors_are_declared_where_both_sides_can_see_them()
    {
        var declared = ProtocolSurface.Endpoints
            .Where(e => e.Path.StartsWith("/v1/airspace/envelopes", StringComparison.Ordinal))
            .Select(e => $"{e.Method} {e.Path}")
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();

        await Assert.That(declared).IsEquivalentTo(new[]
        {
            "GET /v1/airspace/envelopes",
            "GET /v1/airspace/envelopes/{name}",
            "PUT /v1/airspace/envelopes/{name}",
        });
    }

    [Test]
    public async Task The_apply_door_can_answer_that_it_diverted()
    {
        // 202 is the widening riding a flight. Its absence is what made a named
        // widening a flat refusal, and its presence here is the whole point of
        // the door: the estate gains a path from "cannot be shown to tighten" to
        // "a person decides".
        var apply = ProtocolSurface.Endpoints
            .Single(e => e is { Method: "PUT", Path: "/v1/airspace/envelopes/{name}" });

        await Assert.That(apply.Statuses).Contains(202);
        await Assert.That(apply.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner never authors the policy that governs it");
    }
}
