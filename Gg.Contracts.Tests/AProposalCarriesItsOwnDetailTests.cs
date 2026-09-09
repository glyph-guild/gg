using System.Reflection;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The proposal fact: everything a person decides on is a named member, and
/// exactly one member is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.3-02, S35.3-03 and S35.3-04.</b> The shape is the whole design
/// argument. A condition cannot be written over what it cannot see, so the
/// operation, the target and the score are members with names - and the part
/// nobody can anticipate the questions for is ONE member, explicitly opaque,
/// so that "what is open-ended here" is answerable by reading the record
/// rather than by knowing which fields turned out to be free-form.
/// </para>
/// <para>
/// <b>Why not a shapeless fact.</b> A proposal that was all blob would be
/// unadmittable: the destination's menu says which operations may be performed,
/// and a menu cannot be matched against a field nothing declares. A proposal
/// that was all members would be a schema this week guesses at on behalf of
/// every rubric anybody writes later. It is both, and the seam between them is
/// the interesting part of the record.
/// </para>
/// <para>
/// <b>And the score is not a number.</b> What a score MEANS belongs to the
/// envelope and the skill - <c>P1</c>, <c>8</c>, <c>high, 2 of 3 reporters
/// blocked</c>. A contract that typed it would settle that here, once, for
/// every tracker and every rubric, in a place nobody would think to look.
/// </para>
/// </remarks>
public class AProposalCarriesItsOwnDetailTests
{
    private static PropertyInfo Member(string name) =>
        typeof(WorkItemProposal).GetProperty(name)
        ?? throw new InvalidOperationException(
            $"WorkItemProposal has no '{name}'. It has: " + string.Join(", ",
                typeof(WorkItemProposal).GetProperties().Select(p => p.Name)));

    [Test]
    public async Task Everything_a_person_decides_on_is_a_named_member()
    {
        // NAMED, so a destination can write a condition over it. The three that
        // admission reads are the three a person would say out loud when
        // refusing one: not that operation, not on that item, not at that score.
        await Assert.That(Member("Operation").PropertyType).IsEqualTo(typeof(string));
        await Assert.That(Member("Target").PropertyType).IsEqualTo(typeof(string));
        await Assert.That(Member("Score").PropertyType).IsEqualTo(typeof(string));
        await Assert.That(Member("Reason").PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task One_member_is_opaque_and_only_one()
    {
        var opaque = typeof(WorkItemProposal).GetProperties()
            .Where(p => p.PropertyType != typeof(string))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(opaque).IsEquivalentTo(new[] { "Detail" })
            .Because("one member nobody here interprets, and everything else a value a "
                   + "condition can be written over. A second opaque member would make "
                   + "'what is open-ended in this record' a question you answer by reading "
                   + "types. Found: " + string.Join(", ", opaque));
    }

    [Test]
    public async Task The_score_is_not_a_number()
    {
        await Assert.That(Member("Score").PropertyType).IsEqualTo(typeof(string))
            .Because("an int here would decide what a score is, for every rubric anybody "
                   + "writes, in a contract nobody consults when writing one.");
    }

    [Test]
    public async Task A_proposal_outside_the_menu_is_refused_with_a_diagnosis()
    {
        var refused = WorkItemProposal.Validate(new WorkItemProposal
        {
            Operation = "delete",
            Target = "1421",
            Reason = "it is a duplicate",
        });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("delete", StringComparison.Ordinal);

        // ARTICLE XI: a diagnosis names the menu, or whoever hit it goes to read
        // the contract to find out what they were allowed to say.
        foreach (var operation in WorkItemOperations.All)
        {
            await Assert.That(refused).Contains(operation, StringComparison.Ordinal);
        }
    }

    [Test]
    public async Task Half_a_proposal_is_refused_the_way_half_a_nomination_is()
    {
        foreach (var (what, proposal) in ((string, WorkItemProposal)[])
            [("no reason", new WorkItemProposal
                { Operation = WorkItemOperations.Update, Target = "1421", Reason = "  " }),
             ("an update with no target", new WorkItemProposal
                { Operation = WorkItemOperations.Update, Reason = "stale" }),
             ("a score with no score", new WorkItemProposal
                { Operation = WorkItemOperations.Score, Target = "1421", Reason = "stale" })])
        {
            await Assert.That(WorkItemProposal.Validate(proposal)).IsNotNull()
                .Because($"{what} is a value the reader would have to invent the rest of.");
        }

        await Assert.That(WorkItemProposal.Validate(new WorkItemProposal
        {
            Operation = WorkItemOperations.Create,
            Reason = "the crash has no item and three people hit it",
        })).IsNull()
            .Because("a create needs no target: the tracker has not issued an id yet, and "
                   + "requiring one would make create the operation nobody can propose.");
    }

    [Test]
    public async Task The_detail_travels_whole_and_nothing_here_reads_it()
    {
        var detail = JsonSerializer.Deserialize<JsonElement>(
            """{"rubric":"reach x severity","reach":{"reporters":3},"considered":["dup of 1189"]}""");

        var proposal = new WorkItemProposal
        {
            Operation = WorkItemOperations.Score,
            Target = "1421",
            Score = "high, 2 of 3 reporters blocked",
            Reason = "two independent repros",
            Detail = detail,
        };

        await Assert.That(WorkItemProposal.Validate(proposal)).IsNull()
            .Because("nothing here knows what a rubric is, which is the point: validating "
                   + "this would be today deciding what a later reader may ask.");

        // ROUND TRIPPED, because a member that survives validation and not
        // serialization is one that arrives empty at the only reader that
        // matters. Structured on the wire rather than a string of JSON, so the
        // control plane stores something it can query rather than something it
        // would have to parse twice.
        var written = JsonSerializer.Serialize(proposal, ProposalJson.Default.WorkItemProposal);
        var read = JsonSerializer.Deserialize(written, ProposalJson.Default.WorkItemProposal)!;

        await Assert.That(read.Detail!.Value.GetProperty("reach").GetProperty("reporters").GetInt32())
            .IsEqualTo(3);
        await Assert.That(read.Score).IsEqualTo("high, 2 of 3 reporters blocked");
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(WorkItemProposal))]
internal sealed partial class ProposalJson : System.Text.Json.Serialization.JsonSerializerContext;
