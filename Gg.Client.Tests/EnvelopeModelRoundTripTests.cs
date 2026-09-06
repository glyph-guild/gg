using Gg.Contracts.Authoring;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Model preservation: <c>parse(render(x)) == x</c>, member by member, on
/// both render paths.
/// </summary>
/// <remarks>
/// <para>
/// <b>Render-idempotence is not this property, and the difference already
/// cost a governance declaration.</b> The previous round-trip test asserted
/// that rendering a re-parse of a render is stable - which passes when both
/// sides drop the same member, and both sides dropped <c>evidence:</c> for
/// three contract versions. What <c>gg envelope apply</c> actually rests on
/// is that the MODEL survives: what a person was shown is what the store
/// holds, or the show is a lie a reviewer signs off on.
/// </para>
/// <para>
/// <b>The ratchet at the bottom is what makes this future-proof.</b> A new
/// member on any text-expressible type must be added to the accounted-for
/// list - covered by the fixture, or exempted with a written reason - so a
/// member cannot ship without somebody deciding its text form. That decision
/// point is exactly what <c>evidence:</c> and <c>attempts:</c> never got.
/// </para>
/// </remarks>
public class EnvelopeModelRoundTripTests
{
    /// <summary>Every text-expressible member, non-default, lists pre-sorted ordinal.</summary>
    private static Envelope Everything() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.10" },
        Environments = ["aspire-payments"],
        Repositories = ["payments"],
        Accepts = [SubjectKinds.Repository],
        Produces = [FactKinds.ChangeManifest],
        Obligations =
        [
            new Obligation
            {
                Id = "human-look",
                Check = ObligationChecks.Human,
                When = AttachmentConditions.TouchesPrefix + "db/**",
                Approver = "lead",
                Evidence = [EvidenceItems.AgentAccount, EvidenceItems.ChangeManifest],
            },
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
                Moves = [LoopMoves.Edit, LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m", Attempts = 3 },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["human-look", "in-scope"],
                PreserveUnadmitted = true,
            },
        ],
    };

    private static Envelope RoundTripped(Envelope envelope)
    {
        var parsed = EnvelopeYaml.Parse(EnvelopeText.Render(envelope));

        if (parsed.Envelope is null)
        {
            throw new InvalidOperationException(
                $"the canonical render did not parse: {parsed.Diagnosis}");
        }

        return parsed.Envelope;
    }

    [Test]
    public async Task An_evidence_declaration_survives_the_round_trip()
    {
        // THE red of the slice: fired live at step 0, where show -> apply
        // silently removed a gate's evidence requirement and minted the
        // weakening as an ordinary attributed change.
        var back = RoundTripped(Everything());

        await Assert.That(back.Obligations.Single(o => o.Id == "human-look").Evidence)
            .IsEquivalentTo((string[])[EvidenceItems.AgentAccount, EvidenceItems.ChangeManifest])
            .Because("a governance declaration that does not survive the text form is one "
                   + "that disappears the first time somebody edits the file it is not in.");
    }

    [Test]
    public async Task Every_expressible_member_survives_the_round_trip()
    {
        var original = Everything();
        var back = RoundTripped(original);

        await Assert.That(back.Context.Scope).IsEqualTo(original.Context.Scope);
        await Assert.That(back.Context.Constitution).IsEqualTo(original.Context.Constitution)
            .Because("1.10 staying 1.10 is the Norway problem this form exists to close.");
        await Assert.That(back.Environments).IsEquivalentTo(original.Environments!);
        await Assert.That(back.Repositories).IsEquivalentTo(original.Repositories!);

        var human = back.Obligations.Single(o => o.Id == "human-look");
        var expected = original.Obligations.Single(o => o.Id == "human-look");
        await Assert.That(human.Check).IsEqualTo(expected.Check);
        await Assert.That(human.When).IsEqualTo(expected.When);
        await Assert.That(human.Approver).IsEqualTo(expected.Approver);
        await Assert.That(human.Evidence).IsEquivalentTo(expected.Evidence);
        await Assert.That(human.Rule).IsNull();

        var machine = back.Obligations.Single(o => o.Id == "in-scope");
        await Assert.That(machine.Rule).IsEqualTo(ObligationPredicates.NoFileOutsideScope);
        await Assert.That(machine.When).IsNull()
            .Because("absent stays absent: a member the author never wrote must not "
                   + "materialize on the way back.");
        await Assert.That(machine.Evidence).IsEmpty();

        var loop = back.Loops.Single();
        await Assert.That(loop.Executor).IsEqualTo(ExecutorRungs.Frontier);
        await Assert.That(loop.Discharges).IsEquivalentTo(original.Loops[0].Discharges);
        await Assert.That(loop.Moves).IsEquivalentTo(original.Loops[0].Moves);
        await Assert.That(loop.Budget.WallClock).IsEqualTo("30m");
        await Assert.That(loop.Budget.Attempts).IsEqualTo(3)
            .Because("attempts: was stored via the wire, invisible in show, and refused on "
                   + "the way back in - the evidence defect through the other door.");
        await Assert.That(loop.OnExhaustion).IsEqualTo(ExhaustionPolicies.HandoffToHuman);

        var destination = back.Destinations.Single();
        await Assert.That(destination.Kind).IsEqualTo(DestinationKinds.PullRequest);
        await Assert.That(destination.Requires).IsEquivalentTo(original.Destinations[0].Requires);
        await Assert.That(destination.PreserveUnadmitted!.Value).IsTrue();
        await Assert.That(destination.Opens).IsNull()
            .Because("absent stays absent here too, and on this kind it must: `opens:` is "
                   + "refused on anything but a flight destination, so a member that "
                   + "materialized on the way back would turn every pull-request document "
                   + "into one apply refuses.");
    }

    [Test]
    public async Task What_a_destination_may_open_survives_the_round_trip()
    {
        // A SECOND FIXTURE, BECAUSE THE MEMBER CANNOT RIDE THE FIRST. `opens:`
        // is legal only on a flight destination and an envelope carries exactly
        // one destination - so covering it on the pull-request fixture would
        // mean pinning the text form of a document apply refuses, which teaches
        // the emitter to round-trip something nobody can write.
        var opening = Everything() with
        {
            Context = new ContextBinding
            {
                Scope = EnvelopeScopes.None, Constitution = "1.10",
            },
            Accepts = [],
            Produces = [],
            Destinations =
            [
                new Destination
                {
                    Id = "open-the-flight",
                    Kind = DestinationKinds.Flight,
                    Requires = ["human-look"],
                    // TWO ENTRIES. The renderer sorts sequences, so a
                    // single-entry list would pass whatever the ordering did.
                    Opens = ["implement", "research"],
                    // TWO IN EACH SET, for the reason the list above has two:
                    // the renderer sorts, so a single entry passes whatever the
                    // ordering does.
                    MaySelect = new DestinationSelection
                    {
                        Environments = ["dev", "staging"],
                        Repositories = ["ledger", "payments"],
                    },
                },
            ],
        };

        var back = RoundTripped(opening);
        var destination = back.Destinations.Single();

        await Assert.That(destination.Kind).IsEqualTo(DestinationKinds.Flight);
        await Assert.That(destination.Opens).IsNotNull();
        await Assert.That(destination.Opens)
            .IsEquivalentTo((string[])["implement", "research"])
            .Because("this list is the pre-approved menu a nomination is checked against, so a "
                   + "member that did not survive the text form is a governance bound that "
                   + "disappears the first time somebody edits the file it is not in - which "
                   + "is what happened to `evidence:` for three contract versions.");
        await Assert.That(destination.MaySelect).IsNotNull();
        await Assert.That(destination.MaySelect!.Environments)
            .IsEquivalentTo((string[])["dev", "staging"])
            .Because("this set is what stops a classifier naming production, so a member that "
                   + "did not survive the text form is a governance bound that disappears the "
                   + "first time somebody edits the file it is not in - the same failure the "
                   + "list above records for `evidence:`.");
        await Assert.That(destination.MaySelect!.Repositories)
            .IsEquivalentTo((string[])["ledger", "payments"]);

        await Assert.That(destination.PreserveUnadmitted).IsNull();

        // And the second render is the first, so `show` after `apply` is not a
        // diff nobody made.
        await Assert.That(EnvelopeText.Render(back)).IsEqualTo(EnvelopeText.Render(opening));
    }

    [Test]
    public async Task A_narrowing_round_trips_the_model_without_loss()
    {
        // The second render path carries the same property as the first, from
        // its first commit - one emitter with an unproven round trip already
        // stripped a declaration, and two would be that defect squared.
        var narrowing = new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "human-look",
                    Check = ObligationChecks.Human,
                    Approver = "lead",
                    Evidence = [EvidenceItems.ChangeManifest],
                },
            ],
        };

        var parsed = EnvelopeYaml.ParseNarrowing(EnvelopeText.Render(narrowing));

        await Assert.That(parsed.Diagnosis).IsNull();
        var back = parsed.Narrowing!.Obligations.Single();
        await Assert.That(back.Id).IsEqualTo("human-look");
        await Assert.That(back.Check).IsEqualTo(ObligationChecks.Human);
        await Assert.That(back.Approver).IsEqualTo("lead");
        await Assert.That(back.Evidence).IsEquivalentTo((string[])[EvidenceItems.ChangeManifest]);
    }

    [Test]
    public async Task Every_member_of_the_schema_is_accounted_for_by_this_suite()
    {
        // THE RATCHET. A new member on a text-expressible type fails here until
        // somebody decides its text form: covered by the fixture above, or
        // exempted below with the reason written down. 'Parsed and never
        // rendered' shipped twice because nothing forced that decision.
        string[] covered =
        [
            nameof(Envelope.Context), nameof(Envelope.Environments), nameof(Envelope.Repositories),
            // THE LEGACY SPELLINGS, exempted rather than covered: they are read
            // so a stored document keeps its bound and are deliberately NEVER
            // rendered, so no fixture can round-trip them. A test asserting they
            // do not appear in the text form is in TheBoundIsASetTests.
            nameof(Envelope.Environment), nameof(Envelope.Repository),
            nameof(Envelope.Accepts), nameof(Envelope.Produces),
            nameof(Envelope.Obligations), nameof(Envelope.Loops), nameof(Envelope.Destinations),
            nameof(Envelope.Instructions),
            nameof(ContextBinding.Scope), nameof(ContextBinding.Constitution),
            nameof(Obligation.Id), nameof(Obligation.Check), nameof(Obligation.When),
            nameof(Obligation.Rule), nameof(Obligation.Approver), nameof(Obligation.Evidence),
            nameof(Loop.Id), nameof(Loop.Executor), nameof(Loop.Discharges), nameof(Loop.Moves),
            nameof(Loop.Budget), nameof(Loop.OnExhaustion),
            nameof(LoopBudget.WallClock), nameof(LoopBudget.Attempts),
            nameof(Destination.Id), nameof(Destination.Kind), nameof(Destination.Requires),
            nameof(Destination.PreserveUnadmitted), nameof(Destination.Opens),
            nameof(Destination.MaySelect),
            nameof(DestinationSelection.Environments), nameof(DestinationSelection.Repositories),
            nameof(EnvelopeNarrowing.Obligations),
        ];

        // Derived, never declared: the parser refuses a document that says it,
        // and RenderComposed reports it as a comment. Deliberately not
        // round-trippable, and that is the exemption's whole content.
        string[] exempt = [nameof(Obligation.Provenance)];

        Type[] expressible =
        [
            typeof(Envelope), typeof(ContextBinding), typeof(Obligation),
            typeof(Loop), typeof(LoopBudget), typeof(Destination), typeof(EnvelopeNarrowing),
        ];

        var unaccounted = expressible
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(m => !covered.Contains(m.Split('.')[1], StringComparer.Ordinal)
                     && !exempt.Contains(m.Split('.')[1], StringComparer.Ordinal))
            .ToList();

        await Assert.That(unaccounted).IsEmpty()
            .Because("a member nobody decided a text form for is the evidence: defect waiting "
                   + "to ship again. Found: " + string.Join(", ", unaccounted));

        // Liveness: the scan can tell a real member from an invented one, and
        // the exemption is load-bearing rather than a stale spelling.
        await Assert.That(typeof(Obligation).GetProperties().Select(p => p.Name))
            .Contains(nameof(Obligation.Provenance));
        await Assert.That(covered).DoesNotContain(nameof(Obligation.Provenance));
    }
}
