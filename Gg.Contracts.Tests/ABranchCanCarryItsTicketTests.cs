using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The branch a destination pushes to, named by whoever wrote the envelope.
/// </summary>
/// <remarks>
/// <para>
/// <b>One rule and no input.</b> <c>gg/GG-118</c> was every branch this platform
/// had ever made, because <c>For</c> took the flight number and nothing else.
/// A team whose convention puts the ticket in the ref had no way to say so, and
/// a branch a person cannot read back to a backlog item is one they match up by
/// hand.
/// </para>
/// <para>
/// <b>The template names the TAIL</b> - and, since 2026-09-19, the whole branch
/// when it starts with <c>refs/heads/</c>, which
/// <c>ATemplateMayNameTheWholeBranchTests</c> holds. This remark said
/// <c>IsOurs</c> is what branch cleanup reads; no product code calls it. The
/// prefix a runner reads is <c>IsHandoff</c>'s, and a handoff stays under it.
/// </para>
/// <para>
/// <b>The flight number is required in it, for a collision rather than for
/// tidiness.</b> Two flights on one ticket - a rerun, a second attempt after a
/// halt - would otherwise want the same ref, and the second push is then either
/// refused as an existing branch or overwrites the first. That is the same
/// argument <c>ForHandoff</c> already makes one case over.
/// </para>
/// </remarks>
public class ABranchCanCarryItsTicketTests
{
    private const string Flight = "GG-118";

    [Test]
    public async Task An_envelope_that_names_none_gets_what_it_always_got()
    {
        // THE DEFAULT IS THE OLD BEHAVIOUR, exactly. Every envelope in force
        // was written before this member existed and must keep meaning what it
        // meant, which is the rule PreserveUnadmitted's absence states.
        await Assert.That(DestinationBranch.For(Flight, template: null, ticket: "18490"))
            .IsEqualTo("gg/GG-118");

        await Assert.That(DestinationBranch.ForHandoff(Flight, template: null, ticket: "18490"))
            .IsEqualTo("gg/handoff/GG-118");
    }

    [Test]
    public async Task The_ticket_is_a_placeholder_the_template_may_name()
    {
        await Assert.That(DestinationBranch.For(Flight, "{ticket}-{flight}", "18490"))
            .IsEqualTo("gg/18490-GG-118");
    }

    [Test]
    public async Task A_template_may_carry_words_of_its_own()
    {
        await Assert.That(DestinationBranch.For(Flight, "feature/{ticket}/{flight}", "18490"))
            .IsEqualTo("gg/feature/18490/GG-118");
    }

    [Test]
    public async Task A_handoff_stays_a_handoff_whatever_the_template_says()
    {
        // The predicate that tells preserved work from offered work reads the
        // segment after the prefix. A template that displaced it would make
        // every kept branch look like one somebody is expected to review.
        var kept = DestinationBranch.ForHandoff(Flight, "{ticket}-{flight}", "18490");

        await Assert.That(kept).IsEqualTo("gg/handoff/18490-GG-118");
        await Assert.That(DestinationBranch.IsHandoff(kept)).IsTrue();
        await Assert.That(DestinationBranch.IsOurs(kept)).IsTrue();
    }

    [Test]
    public async Task A_flight_opened_from_a_sentence_loses_the_ticket_and_keeps_the_branch()
    {
        // A FLIGHT WITH NO TICKET IS ORDINARY, and refusing to land one because
        // the envelope's branch mentions a ticket would make a formatting choice
        // into a governance one. The part disappears and the separators it was
        // between go with it.
        await Assert.That(DestinationBranch.For(Flight, "{ticket}-{flight}", ticket: null))
            .IsEqualTo("gg/GG-118");

        await Assert.That(DestinationBranch.For(Flight, "feature/{ticket}/{flight}", ""))
            .IsEqualTo("gg/feature/GG-118");
    }

    [Test]
    public async Task A_ticket_shaped_like_a_path_cannot_steer_one()
    {
        // The id reaches here from a tracker, and a ref name is handed to git.
        // `..` is the reason the dot is not in the accepted set at all rather
        // than accepted-and-collapsed, which is what For's own rule already says
        // about the flight number.
        var branch = DestinationBranch.For(Flight, "{ticket}-{flight}", "../../etc/passwd");

        await Assert.That(branch).DoesNotContain("..");
        await Assert.That(DestinationBranch.IsOurs(branch)).IsTrue();
    }

    [Test]
    public async Task A_template_without_the_flight_number_is_refused_at_authoring()
    {
        // TWO FLIGHTS ON ONE TICKET is the ordinary case, not the exotic one: a
        // rerun after a halt is exactly that. Refused where the envelope is
        // written, because the alternative is discovering it on the second push.
        await Assert.That(DestinationBranch.Validate("{ticket}")).IsNotNull();
        await Assert.That(DestinationBranch.Validate("{ticket}-{flight}")).IsNull();
    }

    [Test]
    public async Task A_template_may_not_write_the_prefix_itself()
    {
        // Naming it would read as harmless and produce `gg/gg/GG-118`; the
        // refusal says which half of the name is the author's.
        await Assert.That(DestinationBranch.Validate("gg/{flight}")).IsNotNull();
    }

    [Test]
    public async Task A_template_naming_a_placeholder_nothing_fills_is_refused()
    {
        // `{issue}` is what somebody writes who has read another tool's
        // documentation. Rendering it literally would put braces in a ref name;
        // dropping it silently would give them a branch they did not ask for.
        await Assert.That(DestinationBranch.Validate("{issue}-{flight}")).IsNotNull();
    }

    /// <summary>An envelope with one destination, so Validate has something to read.</summary>
    private static Envelope Naming(string kind, string? branch) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "looked",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.LoopNotExhausted,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["looked"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "somewhere",
                Kind = kind,
                Requires = ["looked"],
                Branch = branch,
                MayPerform = string.Equals(kind, DestinationKinds.WorkItemTracker, StringComparison.Ordinal)
                    ? [WorkItemOperations.Field]
                    : null,
                MayWrite = string.Equals(kind, DestinationKinds.WorkItemTracker, StringComparison.Ordinal)
                    ? ["Custom.Score"]
                    : null,
            },
        ],
    };

    [Test]
    public async Task A_document_carrying_a_bad_template_is_refused_where_it_is_written()
    {
        // THE RULE REACHES THE DOCUMENT, which is the half that matters: a rule
        // nothing calls is a rule an author never meets. Validate is what a
        // person's `gg` runs against the file in front of them.
        await Assert.That(Envelope.Validate(Naming(DestinationKinds.PullRequest, "{ticket}")))
            .IsNotNull()
            .Because("no flight number in it, so a rerun on the same ticket collides.");

        await Assert.That(
                Envelope.Validate(Naming(DestinationKinds.PullRequest, "{ticket}-{flight}")))
            .IsNull();
    }

    [Test]
    public async Task A_kind_that_pushes_nothing_may_not_name_a_branch()
    {
        // ON PRESERVE-UNADMITTED'S TERMS, one arm down in the same loop: a
        // tracker destination lands by writing a field and never pushes, so
        // naming what to call its branch is a setting somebody makes and
        // believes they made.
        await Assert.That(
                Envelope.Validate(Naming(DestinationKinds.WorkItemTracker, "{ticket}-{flight}")))
            .IsNotNull();

        await Assert.That(Envelope.Validate(Naming(DestinationKinds.WorkItemTracker, null)))
            .IsNull();
    }

    [Test]
    public async Task A_destination_carries_the_template_it_was_given()
    {
        // The member exists on the record a person writes, which is what makes
        // this configuration rather than a constant somebody edits.
        var destination = new Destination
        {
            Id = "pull-request",
            Kind = DestinationKinds.PullRequest,
            Requires = ["in-scope"],
            Branch = "{ticket}-{flight}",
        };

        await Assert.That(destination.Branch).IsEqualTo("{ticket}-{flight}");
    }
}
