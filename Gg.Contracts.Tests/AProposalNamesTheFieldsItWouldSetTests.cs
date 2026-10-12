using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A proposal that would set fields says which, in members admission can read.
/// </summary>
/// <remarks>
/// <para>
/// <b>S36.2-01, and the reason is rule 2 rather than tidiness.</b> Admission
/// admits on NAMED members. If the fields an agent would set lived in the
/// opaque detail — where the adapter reads `title` and `state` today — the
/// control plane could not hold them against the destination's menu, and the
/// check would fall to the runner. The runner is not an authority, and a rule
/// in the runner is one the runner can be talked out of.
/// </para>
/// <para>
/// <b>A list of a named pair, not a dictionary.</b> A wire type a person
/// audits is a list of things with names; a map is a shape whose keys nobody
/// declared, and its keys here are the whole security question.
/// </para>
/// <para>
/// <b>The opaque detail keeps its meaning.</b> What moved out of it is only
/// the part somebody must check. What stays is what nobody interprets — the
/// rubric, the confidence, what was ruled out — which is what it was for.
/// </para>
/// </remarks>
public class AProposalNamesTheFieldsItWouldSetTests
{
    private static WorkItemProposal Setting(params WorkItemFieldEdit[] edits) => new()
    {
        Operation = WorkItemOperations.Field,
        Target = "1421",
        Reason = "the rubric scores reach times severity and this is an eight",
        Fields = edits,
    };

    [Test]
    public async Task The_fields_are_named_members_so_admission_can_read_them()
    {
        var proposal = Setting(
            new WorkItemFieldEdit { Path = "Custom.RiceScore", Value = "8" },
            new WorkItemFieldEdit { Path = "Custom.Impact", Value = "high" });

        await Assert.That(WorkItemProposal.Validate(proposal)).IsNull();
        await Assert.That(proposal.Fields!.Select(f => f.Path))
            .IsEquivalentTo(new[] { "Custom.RiceScore", "Custom.Impact" })
            .Because("a control plane cannot hold against a menu what it cannot see, and the "
                   + "alternative is the runner checking - which is not an authority.");
    }

    [Test]
    public async Task A_field_proposal_that_names_no_edit_is_refused()
    {
        // HALF A PROPOSAL, the rule the missing reason and the missing target
        // are already refused under: an operation with no subject is a value
        // the reader would have to invent the rest of.
        await Assert.That(WorkItemProposal.Validate(new WorkItemProposal
        {
            Operation = WorkItemOperations.Field,
            Target = "1421",
            Reason = "it is mis-filed",
        })).IsNotNull()
            .Because("`field` with nothing to set says an item should change without saying "
                   + "how, which is the score-with-no-score case one operation over.");
    }

    [Test]
    public async Task An_edit_is_refused_when_either_half_is_missing()
    {
        foreach (var (what, edit) in ((string, WorkItemFieldEdit)[])
            [("no path", new WorkItemFieldEdit { Path = "  ", Value = "8" }),
             ("no value", new WorkItemFieldEdit { Path = "Custom.RiceScore", Value = "  " })])
        {
            await Assert.That(WorkItemProposal.Validate(Setting(edit))).IsNotNull()
                .Because($"an edit with {what} names half a change. A blank VALUE is the "
                       + "subtle one: it reads as clearing a field, and clearing is a change "
                       + "nobody proposed.");
        }
    }

    [Test]
    public async Task Only_a_field_proposal_carries_edits()
    {
        // A LINK THAT CARRIES FIELD EDITS is a proposal whose operation and
        // whose content disagree, and admission would hold the edits against a
        // menu while performing something else entirely.
        await Assert.That(WorkItemProposal.Validate(new WorkItemProposal
        {
            Operation = WorkItemOperations.Link,
            Target = "1421",
            Reason = "duplicate",
            Fields = [new WorkItemFieldEdit { Path = "Custom.RiceScore", Value = "8" }],
        })).IsNotNull()
            .Because("the operation says what happens and the edits say what changes; a "
                   + "proposal where they disagree is one whose meaning depends on which "
                   + "half the reader believes.");
    }

    [Test]
    public async Task The_opaque_detail_still_means_what_it_meant()
    {
        // What moved out is the part somebody must CHECK. What stays is the
        // part nobody interprets, which is what it was for.
        var proposal = Setting(new WorkItemFieldEdit { Path = "Custom.RiceScore", Value = "8" })
            with
            {
                Detail = JsonSerializer.Deserialize<JsonElement>(
                    """{"rubric":"reach x severity","confidence":0.7}"""),
            };

        await Assert.That(WorkItemProposal.Validate(proposal)).IsNull()
            .Because("nothing here reads a rubric, and a later reader still can.");
    }

    [Test]
    public async Task A_rich_text_field_holds_an_analysis()
    {
        // THE OWNER'S ASK, 2026-10-12: investigate writes its analysis into the
        // item's Claude Code Plan field. GG-1086's landing was 3,489 characters
        // of markdown after it had cut a 5,518-character draft, and the field
        // is html, which is longer again. At 2,048 every real analysis was
        // refused and the only way through was a shorter one.
        var analysis = "<h2>Approach</h2>" + string.Concat(
            Enumerable.Repeat("<p>Move the tab stop off the label onto the real button.</p>", 160));

        await Assert.That(analysis.Length).IsGreaterThan(9_000)
            .Because("it is the size an analysis really is, or this proves nothing.");
        await Assert.That(WorkItemProposal.Validate(
            Setting(new WorkItemFieldEdit { Path = "Custom.ClaudeCodePlan", Value = analysis })))
            .IsNull()
            .Because("a rich-text field a person reads in the form is where a plan belongs, "
                   + "and a bound that refuses every plan is a field nothing can write.");
    }

    [Test]
    public async Task The_values_together_hold_at_most_one_document()
    {
        // THIRTY-TWO FIELDS TIMES THE PER-VALUE BOUND would be half a megabyte
        // in one fact, and the facts of one flight travel in one batch - the
        // reason the detail is bounded. Raising one value's room must not raise
        // the proposal's.
        var half = new string('x', WorkItemFieldEdit.MaxValue / 2 + 1);

        await Assert.That(WorkItemProposal.Validate(Setting(
                new WorkItemFieldEdit { Path = "Custom.ClaudeCodePlan", Value = half },
                new WorkItemFieldEdit { Path = "System.Description", Value = half })))
            .IsNotNull()
            .Because("two values that each fit can still sum past what one proposal carries.");
        await Assert.That(WorkItemProposal.Validate(Setting(new WorkItemFieldEdit
            {
                Path = "Custom.ClaudeCodePlan",
                Value = new string('x', WorkItemFieldEdit.MaxValue + 1),
            })))
            .IsNotNull()
            .Because("one value past the bound is refused, not shortened.");
    }
}
