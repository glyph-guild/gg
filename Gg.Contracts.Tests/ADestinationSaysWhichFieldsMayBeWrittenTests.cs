using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Which field paths a tracker destination permits, and the one matcher that
/// answers it everywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>S36.3-01 through -04, and -07 through -09.</b> Opening <c>field</c> to
/// arbitrary paths is a widening: an agent that can name any field can set
/// <c>System.AreaPath</c> and move work to another team, which is the example
/// the contract already uses for why <c>field</c> is separate from
/// <c>update</c>. So the destination carries a menu, on <c>Opens</c>,
/// <c>MaySelect</c> and <c>MayPerform</c>'s terms.
/// </para>
/// <para>
/// <b>A wildcard, and what it costs.</b> An open-ended rubric means an
/// operator scoring six fields today scores a seventh tomorrow, so a
/// trailing-<c>*</c> prefix earns its place. It also turns three SET
/// operations into MATCHING ones — composition, direction and admission all
/// ask <i>does this path match this menu</i>, and set difference gets one
/// direction backwards. One matcher, read by three.
/// </para>
/// </remarks>
public class ADestinationSaysWhichFieldsMayBeWrittenTests
{
    private static Envelope With(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Accepts = [SubjectKinds.Tracker, SubjectKinds.Repository],
        Produces = [FactKinds.WorkItemProposal],
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
                Id = "triage",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["looked"],
                Moves = [LoopMoves.Read, LoopMoves.ProposeWorkItem],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination Tracker(
        IReadOnlyList<string>? mayWrite,
        IReadOnlyList<string>? mayPerform = null) => new()
    {
        Id = "the-backlog",
        Kind = DestinationKinds.WorkItemTracker,
        Requires = ["looked"],
        MayPerform = mayPerform ?? [WorkItemOperations.Field, WorkItemOperations.Score],
        MayWrite = mayWrite,
    };

    // ---- the menu ----

    [Test]
    public async Task A_tracker_destination_names_the_fields_it_permits()
    {
        await Assert.That(Envelope.Validate(With(Tracker(
            ["Custom.RiceScore", "Custom.Impact"])))).IsNull();
    }

    [Test]
    public async Task Permitting_field_and_naming_no_path_is_refused()
    {
        // A destination that may set fields and says which none is a menu with
        // nothing on it - the unreachable-destination shape MayPerform already
        // refuses, one member down.
        foreach (var (what, menu) in ((string, IReadOnlyList<string>?)[])
            [("absent", null), ("empty", [])])
        {
            await Assert.That(Envelope.Validate(With(Tracker(menu)))).IsNotNull()
                .Because($"may-perform permits `field` and may-write is {what}, so the "
                       + "flight may set fields and no field.");
        }
    }

    [Test]
    public async Task Not_permitting_field_and_naming_paths_is_refused_too()
    {
        // The other direction, and the PreserveUnadmitted precedent: a list
        // that bounds nothing reads as a permission somebody granted.
        await Assert.That(Envelope.Validate(With(Tracker(
            ["Custom.RiceScore"], mayPerform: [WorkItemOperations.Link])))).IsNotNull()
            .Because("nothing here sets a field, so the paths bound nothing and somebody "
                   + "wrote them believing otherwise.");
    }

    [Test]
    public async Task It_bounds_nothing_on_any_other_kind_so_it_is_refused_there()
    {
        var onAPullRequest = With(new Destination
        {
            Id = "forge",
            Kind = DestinationKinds.PullRequest,
            Requires = ["looked"],
            MayWrite = ["Custom.RiceScore"],
        });

        await Assert.That(Envelope.Validate(onAPullRequest)).IsNotNull()
            .Because("only a tracker writes fields.");
    }

    // ---- the wildcard ----

    [Test]
    public async Task A_prefix_may_be_wildcarded_and_a_bare_star_may_not()
    {
        await Assert.That(Envelope.Validate(With(Tracker(["Custom.*"])))).IsNull()
            .Because("an open-ended rubric scores a seventh field tomorrow, and enumerating "
                   + "them is a document edit per field.");

        await Assert.That(Envelope.Validate(With(Tracker(["System.*"])))).IsNull()
            .Because("permitted, and deliberately. The safety is that a PERSON wrote it into "
                   + "a document whose widening a comparator shows them - not that this "
                   + "contract ranks namespaces by how frightening they are. A blocklist is "
                   + "the shape that passes on the third member nobody thought of.");

        await Assert.That(Envelope.Validate(With(Tracker(["*"])))).IsNotNull()
            .Because("a menu that permits everything is not a menu.");
    }

    // ---- one matcher, read by three ----

    [Test]
    public async Task The_matcher_answers_exactly_and_by_prefix()
    {
        await Assert.That(WorkItemFields.Matches("Custom.RiceScore", ["Custom.RiceScore"]))
            .IsTrue();
        await Assert.That(WorkItemFields.Matches("Custom.RiceScore", ["Custom.*"])).IsTrue();
        await Assert.That(WorkItemFields.Matches("System.AreaPath", ["Custom.*"])).IsFalse()
            .Because("a prefix is a prefix, and this is the field the whole menu exists for.");
        await Assert.That(WorkItemFields.Matches("Custom.RiceScore", [])).IsFalse()
            .Because("an empty menu permits nothing, which is why an empty one is refused at "
                   + "authoring rather than left to mean something here.");

        // NOT A SUBSTRING. `Custom.*` must not match `NotCustom.Thing`, and a
        // naive Contains would.
        await Assert.That(WorkItemFields.Matches("NotCustom.Thing", ["Custom.*"])).IsFalse();
    }

    [Test]
    public async Task Narrowing_a_wildcard_to_a_path_is_a_tightening_not_a_widening()
    {
        // THE DEFECT SET DIFFERENCE WOULD HAVE. `{Custom.Score}` except
        // `{Custom.*}` is `{Custom.Score}` and reads as a widening - a
        // governance rule crying wolf, which is worse than no rule because
        // people learn to approve past it.
        var tightening = EnvelopeDirection.Widening(
            With(Tracker(["Custom.*"])), With(Tracker(["Custom.RiceScore"])));

        await Assert.That(tightening).IsNull()
            .Because("permitting one path where a prefix was permitted is narrowing.");
    }

    [Test]
    public async Task Widening_a_path_to_a_wildcard_is_shown()
    {
        var widening = EnvelopeDirection.Widening(
            With(Tracker(["Custom.RiceScore"])), With(Tracker(["Custom.*"])));

        await Assert.That(widening).IsNotNull()
            .Because("a prefix reaches fields the path did not, and reaching further is what "
                   + "a narrowing may never do.");
        await Assert.That(widening!.Because).Contains("Custom.*", StringComparison.Ordinal);
    }
}
