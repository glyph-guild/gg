using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// The environments and repositories an envelope's flights may be about.
/// </summary>
/// <remarks>
/// <para>
/// <b>SINGLE-VALUED WAS DISCOVERED TO BE UNIMPLEMENTABLE, and the discovery is
/// the reason this changed.</b> Part C of the selection design has a
/// destination permit a nomination to choose between <c>dev</c> and
/// <c>staging</c>. It cannot: the bound was one name, root-only, and the door
/// below required the flight's declared environment to EQUAL it - so a flight
/// could only ever be in root's one environment, and every selection but that
/// one was refused. All the refusals worked. The permission was unreachable.
/// </para>
/// <para>
/// <b>The flight still names one.</b> A flight runs in one environment; what
/// becomes a set is what the ENVELOPE permits, and the check becomes membership
/// rather than equality. That is the shape <c>Destination.Opens</c> already
/// has for work kinds, which is the argument for it: a menu a person wrote, an
/// exact match against it, and nothing outside it admitted.
/// </para>
/// <para>
/// <b>One name still reads and renders as a scalar.</b> Every envelope written
/// before this declares one, and a document that round-tripped into a sequence
/// would be a diff nobody made on the next <c>show</c>. So a single permitted
/// name renders as <c>environments: dev</c> and the legacy <c>environment:</c>
/// key still parses - both spellings exist in stored documents, which is the
/// same reason the intent read COALESCEs two.
/// </para>
/// </remarks>
public class TheBoundIsASetTests
{
    private static Envelope Bounding(
        IReadOnlyList<string>? environments = null,
        IReadOnlyList<string>? repositories = null) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Environments = environments,
        Repositories = repositories,
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
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "20m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task An_envelope_may_permit_more_than_one_environment()
    {
        var envelope = Bounding(environments: ["dev", "staging"]);

        await Assert.That(Envelope.Validate(envelope)).IsNull();
        await Assert.That(envelope.Environments).IsEquivalentTo((string[])["dev", "staging"]);
    }

    [Test]
    public async Task Permitting_none_is_the_ordinary_state()
    {
        // Null, not empty. Most tenants bound no environment at all, and their
        // flights inherit nothing - which is what unselected has always meant.
        await Assert.That(Bounding().Environments).IsNull();
        await Assert.That(Envelope.Validate(Bounding())).IsNull();
    }

    [Test]
    public async Task A_declared_but_empty_set_is_refused()
    {
        // An empty bound would permit no environment and forbid declaring one,
        // which is a document that can only ever refuse its own flights. Null
        // says "unbounded"; there is no useful thing "bounded to nothing" means.
        await Assert.That(Envelope.Validate(Bounding(environments: []))).IsNotNull();
        await Assert.That(Envelope.Validate(Bounding(repositories: []))).IsNotNull();
    }

    [Test]
    public async Task A_blank_name_in_either_set_is_refused()
    {
        await Assert.That(Envelope.Validate(Bounding(environments: ["dev", "  "]))).IsNotNull();
        await Assert.That(Envelope.Validate(Bounding(repositories: [""]))).IsNotNull();
    }

    [Test]
    public async Task One_permitted_name_still_renders_as_a_scalar()
    {
        // BYTE-FOR-BYTE FOR EVERY DOCUMENT THAT EXISTS. A tenant with one
        // environment must not find their file rewritten into a sequence by the
        // next show - a diff nobody made is how a review practice is abandoned.
        var rendered = EnvelopeText.Render(Bounding(environments: ["dev"]));

        await Assert.That(rendered).Contains("environments: dev");
        await Assert.That(rendered.Contains("- dev", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Two_permitted_names_render_as_a_sequence_and_come_back()
    {
        var back = EnvelopeYaml.Parse(
            EnvelopeText.Render(Bounding(environments: ["dev", "staging"])));

        await Assert.That(back.Diagnosis).IsNull();
        await Assert.That(back.Envelope!.Environments)
            .IsEquivalentTo((string[])["dev", "staging"]);
    }

    [Test]
    public async Task The_legacy_singular_key_still_parses()
    {
        // Documents on disk and in storage were written with `environment:` and
        // `repository:`, and refusing them would make this change break every
        // envelope already applied. Two spellings, one value, for the reason the
        // intent read COALESCEs two: both exist in real documents.
        var parsed = EnvelopeYaml.Parse("""
            context:
              scope: "src/**"
              constitution: "1.0.0"
            environment: dev
            repository: "acme/payments"
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              implement:
                executor: frontier
                discharges: [in-scope]
                moves: [read, edit]
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              forge:
                kind: pull-request
                requires: [in-scope]
            """);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Environments).IsEquivalentTo((string[])["dev"]);
        await Assert.That(parsed.Envelope!.Repositories)
            .IsEquivalentTo((string[])["acme/payments"]);
    }

    [Test]
    public async Task Both_spellings_at_once_are_refused()
    {
        // One value, two keys, and no way to tell which the author meant. A
        // silent precedence would be the document saying one thing and the
        // engine reading another.
        var parsed = EnvelopeYaml.Parse("""
            context:
              scope: "src/**"
              constitution: "1.0.0"
            environment: dev
            environments: [dev, staging]
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              implement:
                executor: frontier
                discharges: [in-scope]
                moves: [read]
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              forge:
                kind: pull-request
                requires: [in-scope]
            """);

        await Assert.That(parsed.Diagnosis).IsNotNull();
    }
}
