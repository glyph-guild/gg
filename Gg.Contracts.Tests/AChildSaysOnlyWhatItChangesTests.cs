using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-02: a chain resolves into one effective work kind, top down.
/// </summary>
/// <remarks>
/// <para>
/// <b>An entry is replaced whole.</b> A loop, a destination or an obligation
/// the child names replaces its parent's entry of that id; nothing is merged
/// member by member, so a reader finds one loop in one document. A list is
/// replaced whole for the same reason, and a scalar the child names wins.
/// </para>
/// <para>
/// <b>Instructions are the one exception</b>: appended after the parent's,
/// because the child's guidance is written to be read after its parent's.
/// </para>
/// </remarks>
public class AChildSaysOnlyWhatItChangesTests
{
    [Test]
    public async Task The_child_wins_where_it_speaks_and_inherits_where_it_does_not()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child);

        await Assert.That(resolved.Refused).IsNull();
        var effective = resolved.Effective!;
        await Assert.That(effective.Brief).IsEqualTo("Investigate the work item and write the plan to its tracker.");
        await Assert.That(effective.Description).IsEqualTo("Analyse a work item.");
        await Assert.That(effective.Context.Scope).IsEqualTo("**");
        await Assert.That(effective.Accepts!).IsEquivalentTo(new[] { "repository" });
        await Assert.That(effective.Produces!).IsEquivalentTo(new[] { "destination.landed" })
            .Because("a list the child names replaces its parent's whole.");
    }

    [Test]
    public async Task A_named_entry_replaces_its_parents_entry_whole()
    {
        var effective = WorkKindFixtures.Resolve(WorkKindFixtures.Child).Effective!;

        await Assert.That(effective.Loops.Single().Moves)
            .IsEquivalentTo(new[] { "propose-work-item", "read" });
        await Assert.That(effective.Destinations.Select(d => d.Id)).IsEquivalentTo(new[] { "ado" });
        await Assert.That(effective.Obligations.Select(o => o.Id))
            .IsEquivalentTo(new[] { "investigate-finished" })
            .Because("what the child does not name it inherits.");
    }

    [Test]
    public async Task Instructions_append_after_the_parents()
    {
        var effective = WorkKindFixtures.Resolve(WorkKindFixtures.Child).Effective!;

        await Assert.That(effective.Instructions.Select(i => i.Text)).IsEquivalentTo(
            new[] { "Read the work item.", "Write the analysis to Custom.ClaudeCodePlan as html." },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_child_adds_an_obligation_and_replaces_one_by_id()
    {
        var added = WorkKindFixtures.Resolve(WorkKindFixtures.Child + """

            obligations:
              plan-in-scope:
                check: machine
                rule: no-file-outside-scope
            """).Effective!;

        await Assert.That(added.Obligations.Select(o => o.Id))
            .IsEquivalentTo(new[] { "investigate-finished", "plan-in-scope" });
    }

    [Test]
    public async Task Removing_what_the_parent_does_not_have_is_refused_by_name()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child.Replace(
            "  destinations:\n    - none", "  destinations:\n    - none\n  loops:\n    - triage"));

        await Assert.That(resolved.Effective).IsNull();
        await Assert.That(resolved.Refused!).Contains("triage")
            .Because("a removal that matches nothing is a parent that changed under its child.");
    }

    [Test]
    public async Task A_child_must_extend_the_document_above_it()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child.Replace(
            "extends: investigate", "extends: review"));

        await Assert.That(resolved.Refused!).Contains("review");
    }

    [Test]
    public async Task The_effective_document_is_held_to_every_envelope_rule()
    {
        // KEEPING THE PARENT'S DESTINATION and adding one is two destinations,
        // which an envelope may not carry. The leaf is validated, not each part.
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child.Replace(
            "  destinations:\n    - none\n", ""));

        await Assert.That(resolved.Effective).IsNull();
        await Assert.That(resolved.Refused!).Contains("investigate-instructions");
    }
}
